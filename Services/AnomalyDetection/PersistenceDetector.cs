namespace IAApi.Services.AnomalyDetection;

public class PersistenceDetector : IAnomalyDetector
{
    private const double DefaultGapMultiplier = 5.0;

    private readonly AnomalyDetectionOptions _options;

    public PersistenceDetector(
        AnomalyDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    public string Nome => "Persistência";
    public double Peso => _options.PersistenceWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var contexto = input.Contexto;
        var historico = contexto.Historico;

        if (historico.Count < 10)
            return NaoAplicavel(
                "Histórico insuficiente para avaliar persistência.");

        var mediana = contexto.Mediana;
        var mad = contexto.Mad;
        var protecao =
            BaselineProtectionProfile.Build(input, mediana);

        /*
         * Persistência responde a uma pergunta diferente do Z-score:
         * "o processo permaneceu materialmente fora do baseline?"
         *
         * Se existe MAD, usamos evidência estatística robusta.
         * Se MAD=0 (baseline praticamente plano), NÃO usamos o desvio
         * padrão global contaminado pela própria sequência persistente.
         * Nesse caso, um deadband confiável é a fronteira física.
         */
        var usaMad =
            mad > 0 &&
            double.IsFinite(mad);

        var usaDeadband =
            !usaMad &&
            protecao.Deadband > 0 &&
            double.IsFinite(protecao.Deadband);

        var escalaFallback =
            !usaMad && !usaDeadband
                ? contexto.DesvioPadrao
                : 0;

        var underlyingEvidenceKind =
            usaMad
                ? UnderlyingEvidenceKind.Mad
                : usaDeadband
                    ? UnderlyingEvidenceKind.DeadbandDeviation
                    : UnderlyingEvidenceKind.StandardDeviationFallback;

        if (!usaMad &&
            !usaDeadband &&
            (escalaFallback <= 0 ||
             !double.IsFinite(escalaFallback)))
        {
            return NaoAplicavel(
                "Não existe escala ou deadband suficiente para avaliar persistência.");
        }

        bool EstaFora(double valor)
        {
            if (!double.IsFinite(valor))
                return false;

            var delta =
                Math.Abs(valor - mediana);

            if (protecao.Deadband > 0 &&
                delta <= protecao.Deadband)
            {
                return false;
            }

            if (usaMad)
            {
                var modifiedZ =
                    0.6745 * delta / mad;

                return modifiedZ >= _options.MadThreshold;
            }

            if (usaDeadband)
            {
                return delta > protecao.Deadband;
            }

            var standardZ =
                delta / escalaFallback;

            return standardZ >= _options.MadThreshold;
        }

        var intervaloMediano =
            contexto.IntervaloMedianoMinutos;

        if (intervaloMediano <= 0 ||
            !double.IsFinite(intervaloMediano))
        {
            return NaoAplicavel(
                "Não foi possível determinar a cadência para avaliar persistência.");
        }

        var quality = input.QualidadeHistorico;

        var limiteGap =
            quality.CriticalGapThresholdMinutes > 0
                ? quality.CriticalGapThresholdMinutes
                : intervaloMediano * DefaultGapMultiplier;

        if (limiteGap <= 0 || !double.IsFinite(limiteGap))
            limiteGap = intervaloMediano * DefaultGapMultiplier;

        var consecutivos = 0;
        DateTime? inicioPersistencia = null;
        var sequenciaQuebradaPorGap = false;
        var maiorGapNaSequencia = 0.0;

        if (EstaFora(input.ValorAtual))
        {
            consecutivos = 1;
            inicioPersistencia = input.DataHoraAtual;

            var proximoTimestamp =
                input.DataHoraAtual;

            for (var index = historico.Count - 1;
                 index >= 0;
                 index--)
            {
                var ponto = historico[index];

                var gap =
                    (proximoTimestamp - ponto.DataHora)
                    .TotalMinutes;

                if (!double.IsFinite(gap) || gap <= 0)
                    break;

                maiorGapNaSequencia =
                    Math.Max(
                        maiorGapNaSequencia,
                        gap);

                /*
                 * O gap é verificado ANTES de incorporar o ponto
                 * anterior à sequência. Assim, tempo sem telemetria
                 * nunca conta como persistência observada.
                 */
                if (gap > limiteGap)
                {
                    sequenciaQuebradaPorGap = true;
                    break;
                }

                if (!EstaFora(ponto.Valor))
                    break;

                consecutivos++;
                inicioPersistencia = ponto.DataHora;
                proximoTimestamp = ponto.DataHora;
            }
        }

        var duracaoMinutos =
            inicioPersistencia.HasValue
                ? Math.Max(
                    0,
                    (input.DataHoraAtual -
                     inicioPersistencia.Value)
                    .TotalMinutes)
                : 0;

        var duracaoMinima =
            Math.Max(
                _options.PersistenceMinimumMinutes,
                intervaloMediano *
                Math.Max(
                    1,
                    _options.PersistenceMinimumPoints - 1));

        var componentePontos =
            Math.Clamp(
                consecutivos /
                (double)Math.Max(
                    1,
                    _options.PersistenceMinimumPoints),
                0,
                1);

        var componenteDuracao =
            duracaoMinima > 0
                ? Math.Clamp(
                    duracaoMinutos / duracaoMinima,
                    0,
                    1)
                : 0;

        var scoreNormalizado =
            ((componentePontos * 0.50) +
             (componenteDuracao * 0.50)) *
            100;

        var ehAnomalia =
            consecutivos >=
                _options.PersistenceMinimumPoints &&
            duracaoMinutos >= duracaoMinima &&
            !sequenciaQuebradaPorGap;

        var confirmationStatus =
            sequenciaQuebradaPorGap
                ? TemporalConfirmationStatus.InterruptedByGap
                : consecutivos <= 0
                    ? TemporalConfirmationStatus.NotObserved
                    : ehAnomalia
                        ? TemporalConfirmationStatus.Confirmed
                        : TemporalConfirmationStatus.Pending;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = scoreNormalizado / 20.0,
            ScoreNormalizado = scoreNormalizado,
            Peso = Peso,
            Detector = Nome,
            EvidenceRole = usaMad
                ? DetectorEvidenceRole.TemporalConfirmation
                : DetectorEvidenceRole.StatisticalEvidence,
            UnderlyingEvidenceKind = underlyingEvidenceKind,
            TemporalConfirmation = new TemporalConfirmationSnapshot
            {
                Status = confirmationStatus,
                UnderlyingEvidenceKind = underlyingEvidenceKind,
                ConsecutivePoints = consecutivos,
                DurationMinutes = duracaoMinutos,
                RequiredDurationMinutes = duracaoMinima,
                RequiredPoints = _options.PersistenceMinimumPoints,
                StartedAt = inicioPersistencia,
                ObservedThrough = consecutivos > 0
                    ? input.DataHoraAtual
                    : null,
                InterruptedByGap = sequenciaQuebradaPorGap,
                LargestObservedGapMinutes = maiorGapNaSequencia
            },
            Explicacao =
                sequenciaQuebradaPorGap
                    ? $"A sequência anômala foi interrompida por um gap de aquisição maior que {limiteGap:F2} minutos."
                    : ehAnomalia
                        ? $"O desvio permaneceu por {consecutivos} leituras contíguas e aproximadamente {duracaoMinutos:F1} minutos."
                        : $"Foram identificadas {consecutivos} leituras contíguas fora do padrão.",
            Metricas = new Dictionary<string, double>
            {
                ["pontosConsecutivos"] = consecutivos,
                ["duracaoMinutos"] = duracaoMinutos,
                ["duracaoMinimaMinutos"] = duracaoMinima,
                ["cadenciaMedianaMinutos"] = intervaloMediano,
                ["limiteGapMinutos"] = limiteGap,
                ["maiorGapNaSequenciaMinutos"] =
                    maiorGapNaSequencia,
                ["sequenciaQuebradaPorGap"] =
                    sequenciaQuebradaPorGap ? 1 : 0,
                ["deadband"] = protecao.Deadband,
                ["madBaseline"] = mad,
                ["usaMad"] = usaMad ? 1 : 0,
                ["usaDeadbandComoFronteira"] =
                    usaDeadband ? 1 : 0,
                ["escalaFallback"] = escalaFallback
            }
        };
    }

    private DetectorResult NaoAplicavel(
        string explicacao) => new()
    {
        Aplicavel = false,
        EhAnomalia = false,
        Score = 0,
        ScoreNormalizado = 0,
        Detector = Nome,
        Peso = Peso,
        Explicacao = explicacao
    };
}
