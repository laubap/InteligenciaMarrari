namespace IAApi.Services.AnomalyDetection;

public class LevelShiftDetector : IAnomalyDetector
{
    private const int MinimumPointsPerWindow = 3;

    private readonly AnomalyDetectionOptions _options;

    public LevelShiftDetector(
        AnomalyDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    public string Nome => "Mudança de patamar";
    public double Peso => _options.LevelShiftWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var cadenciaMediana =
            input.Contexto.IntervaloMedianoMinutos;

        if (cadenciaMediana <= 0 || !double.IsFinite(cadenciaMediana))
        {
            return NaoAplicavel(
                "Não foi possível determinar a cadência da tag.");
        }

        var previousMinutes =
            Math.Max(1, _options.LevelShiftPreviousWindow);

        var recentMinutes =
            Math.Max(1, _options.LevelShiftRecentWindow);

        var fimRecente = input.DataHoraAtual;
        var inicioRecente = fimRecente.AddMinutes(-recentMinutes);
        var inicioAnterior = inicioRecente.AddMinutes(-previousMinutes);

        var anteriores =
            input.Historico
                .Where(p =>
                    p.DataHora >= inicioAnterior &&
                    p.DataHora < inicioRecente)
                .Select(p => p.Valor)
                .ToArray();

        var recentes =
            input.Historico
                .Where(p =>
                    p.DataHora >= inicioRecente &&
                    p.DataHora <= fimRecente)
                .Select(p => p.Valor)
                .ToList();

        recentes.Add(input.ValorAtual);

        if (anteriores.Length < MinimumPointsPerWindow ||
            recentes.Count < MinimumPointsPerWindow)
        {
            return NaoAplicavel(
                $"Janelas temporais insuficientes. " +
                $"Anterior={anteriores.Length} ponto(s), " +
                $"recente={recentes.Count} ponto(s).");
        }

        var medianaAnterior =
            DetectorMath.Median(anteriores);

        var medianaRecente =
            DetectorMath.Median(recentes.ToArray());

        var escalaAnteriorOriginal =
            DetectorMath.EscalaRobusta(anteriores);

        var protecao =
            BaselineProtectionProfile.Build(input, medianaAnterior);

        var escalaAnterior =
            protecao.ResolveEffectiveScale(
                escalaAnteriorOriginal,
                _options.LevelShiftThreshold);

        if (escalaAnterior <= 0 || !double.IsFinite(escalaAnterior))
        {
            return NaoAplicavel(
                "A janela anterior não possui escala suficiente.");
        }

        var diferenca = medianaRecente - medianaAnterior;
        var diferencaAbsoluta = Math.Abs(diferenca);

        var dentroDeadband =
            protecao.Deadband > 0 &&
            diferencaAbsoluta <= protecao.Deadband;

        var scoreMudancaBruto =
            diferencaAbsoluta / escalaAnterior;

        var scoreMudanca =
            dentroDeadband ? 0 : scoreMudancaBruto;

        var direcao = Math.Sign(diferenca);
        var consistentes = 0;

        if (direcao != 0)
        {
            foreach (var valor in recentes)
            {
                if (direcao > 0
                        ? valor > medianaAnterior
                        : valor < medianaAnterior)
                {
                    consistentes++;
                }
            }
        }

        var consistencia =
            consistentes / (double)recentes.Count;

        var componenteDeslocamento =
            Math.Clamp(
                scoreMudanca /
                Math.Max(_options.LevelShiftThreshold, 1e-9),
                0,
                1);

        var scoreNormalizado =
            dentroDeadband
                ? 0
                : ((componenteDeslocamento * 0.70) +
                   (consistencia * 0.30)) *
                  100;

        var possuiGapCriticoNasJanelas =
            HasCriticalGap(input, inicioAnterior, fimRecente);

        var ehAnomalia =
            !dentroDeadband &&
            !possuiGapCriticoNasJanelas &&
            scoreMudanca >= _options.LevelShiftThreshold &&
            consistencia >= _options.LevelShiftMinimumConsistency;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = scoreMudanca,
            ScoreNormalizado = scoreNormalizado,
            Peso = Peso,
            Detector = Nome,
            Explicacao = possuiGapCriticoNasJanelas
                ? "As janelas temporais contêm um gap crítico de aquisição; a mudança de patamar não foi confirmada."
                : dentroDeadband
                    ? $"A diferença entre patamares ({diferencaAbsoluta:F6}) está dentro do deadband ({protecao.Deadband:F6})."
                    : ehAnomalia
                        ? $"Mudança temporal de patamar confirmada: mediana anterior={medianaAnterior:F4}, recente={medianaRecente:F4}."
                        : "Não foi identificada mudança temporal consistente de patamar.",
            Metricas = new Dictionary<string, double>
            {
                ["medianaAnterior"] = medianaAnterior,
                ["medianaRecente"] = medianaRecente,
                ["diferencaPatamar"] = diferenca,
                ["scoreMudanca"] = scoreMudanca,
                ["scoreMudancaAntesDeadband"] = scoreMudancaBruto,
                ["consistencia"] = consistencia,
                ["janelaAnteriorMinutos"] = previousMinutes,
                ["janelaRecenteMinutos"] = recentMinutes,
                ["pontosJanelaAnterior"] = anteriores.Length,
                ["pontosJanelaRecente"] = recentes.Count,
                ["cadenciaMedianaMinutos"] = cadenciaMediana,
                ["deadband"] = protecao.Deadband,
                ["possuiGapCriticoNasJanelas"] =
                    possuiGapCriticoNasJanelas ? 1 : 0
            }
        };
    }

    private static bool HasCriticalGap(
        DetectorInput input,
        DateTime start,
        DateTime end)
    {
        var medianInterval =
            input.QualidadeHistorico.MedianIntervalMinutes;

        if (medianInterval <= 0 || !double.IsFinite(medianInterval))
            return false;

        var gapLimit =
            input.QualidadeHistorico.CriticalGapThresholdMinutes > 0
                ? input.QualidadeHistorico.CriticalGapThresholdMinutes
                : medianInterval * 5.0;

        DateTime? previous = null;

        foreach (var point in input.Historico)
        {
            if (point.DataHora < start || point.DataHora > end)
                continue;

            if (previous.HasValue)
            {
                var gap =
                    (point.DataHora - previous.Value).TotalMinutes;

                if (gap > gapLimit)
                    return true;
            }

            previous = point.DataHora;
        }

        if (previous.HasValue)
        {
            var gapToCurrent =
                (input.DataHoraAtual - previous.Value).TotalMinutes;

            if (gapToCurrent > gapLimit)
                return true;
        }

        return false;
    }

    private DetectorResult NaoAplicavel(string explicacao) => new()
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
