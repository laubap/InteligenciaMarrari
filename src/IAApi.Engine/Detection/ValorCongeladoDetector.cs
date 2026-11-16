using IAApi.IntelligenceLab.Observability;
using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection.Runtime.Regime;
using IAApi.Services.AnomalyDetection.Runtime.Regime.Evidence;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// BACKLOG 33.C (passo 2): valor congelado como evidência independente
/// (<see cref="DetectorEvidenceRole.IndependentEvidence"/>). Não entra na
/// média ponderada; quando confirma, o ensemble decide anomalia por "OU".
///
/// Reaproveita o <see cref="FrozenSensorEvidenceCalculator"/> sem alterá-lo,
/// com três diferenças, todas aqui:
/// 1. tipo do sinal: aceita <c>Continuous</c> e <c>Unknown</c> (a TAG24 do
///    CP300 sai <c>Unknown</c>) e recusa <c>Discrete</c>, <c>Counter</c> e
///    <c>Setpoint</c> em qualquer confiança;
/// 2. histórico: usa o último trecho contíguo
///    (<see cref="DetectorInputContiguousTail"/>), porque o histórico do
///    regime é concatenado; se a leitura atual estiver separada desse trecho
///    por um gap crítico, não se aplica;
/// 3. guarda de "repouso conhecido": se o mesmo valor já ficou parado por
///    pelo menos <see cref="ValorCongeladoOptions.MinutosRepouso"/> antes da
///    sequência atual, não alarma (bomba parada, válvula fechada).
///
/// Só é registrado com <c>AnomalyDetection:ValorCongelado:Habilitado = true</c>;
/// cliente fora de <c>ClientesHabilitados</c> ⇒ não aplicável.
/// </summary>
public sealed class ValorCongeladoDetector : IAnomalyDetector
{
    public const string NomeDetector = "Valor congelado";

    // Mesma tolerância do FrozenSensorEvidenceCalculator (igualdade exata).
    private const double ToleranciaIgualdade = 1e-12;

    private readonly ValorCongeladoOptions _options;
    private readonly TagBehaviorProfileResolver _resolver;
    private readonly RegimeShadowOptions _calculatorOptions;
    private readonly FrozenSensorEvidenceCalculator _calculator = new();

    public ValorCongeladoDetector(
        ValorCongeladoOptions options,
        TagBehaviorProfileResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);

        _options = options;
        _resolver = resolver;
        _calculatorOptions = new RegimeShadowOptions
        {
            FrozenMinimumPoints = options.MinimoPontos,
            FrozenMinimumMinutes = options.MinimoMinutos,
            FrozenPriorWindowPoints = options.JanelaAnteriorPontos,
            FrozenMaximumPriorZeroRate = options.TaxaMaximaPassosIguaisAntes
        };
    }

    public string Nome => NomeDetector;

    // Evidência independente: fica fora da média, o peso não é usado.
    public double Peso => 0;

    public DetectorResult Detectar(DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!_options.EstaHabilitadoPara(input.ClienteId))
        {
            return NaoAplicavel(
                "Detector de valor congelado não habilitado para este cliente.",
                DetectorDiagnosticReasonCodes.ClientNotEnabledForDetector);
        }

        var perfil =
            input.ObterPerfilComportamental(_resolver);

        if (perfil.Type is not (TagBehaviorType.Continuous or TagBehaviorType.Unknown))
        {
            return NaoAplicavel(
                $"Tipo de sinal {perfil.Type}: estados, contadores e set-points " +
                "podem ficar constantes legitimamente.",
                DetectorDiagnosticReasonCodes.SignalTypeNotSupported,
                perfil);
        }

        var contigua =
            DetectorInputContiguousTail.Create(input);

        var limiteGap =
            LimiteGapCritico(input);

        if (contigua.Historico.Count > 0 &&
            (input.DataHoraAtual - contigua.Historico[^1].DataHora).TotalMinutes > limiteGap)
        {
            return NaoAplicavel(
                "A leitura atual está separada do histórico por um gap crítico.",
                DetectorDiagnosticReasonCodes.NoContiguousHistory,
                perfil);
        }

        // O tipo já foi filtrado acima (Unknown aceito); o calculador só
        // aplica a regra a sinais contínuos.
        var calculo =
            _calculator.Calculate(
                contigua,
                RegimeSignalKind.Continuous,
                _calculatorOptions);

        if (!calculo.Applicable)
        {
            return NaoAplicavel(
                $"Avaliação de congelamento não aplicável ({calculo.Reason}).",
                calculo.Reason == "critical-gap"
                    ? DetectorDiagnosticReasonCodes.NoContiguousHistory
                    : DetectorDiagnosticReasonCodes.FrozenValueNotApplicable,
                perfil);
        }

        var inicioSequencia =
            input.DataHoraAtual.AddMinutes(-calculo.DurationMinutes);

        var maiorRepouso =
            calculo.HasEvidence
                ? MaiorRepousoAnterior(
                    input.Historico,
                    calculo.FrozenValue,
                    inicioSequencia,
                    limiteGap)
                : 0;

        var repousoConhecido =
            calculo.HasEvidence &&
            maiorRepouso >= _options.MinutosRepouso;

        var confirmado =
            calculo.HasEvidence &&
            !repousoConhecido;

        var scoreNormalizado =
            Nota(calculo, confirmado);

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = confirmado,
            Score = scoreNormalizado / 20.0,
            ScoreNormalizado = scoreNormalizado,
            Peso = Peso,
            Detector = Nome,
            EvidenceRole = DetectorEvidenceRole.IndependentEvidence,
            Explicacao = Explicar(calculo, confirmado, repousoConhecido, maiorRepouso),
            Metricas = new Dictionary<string, double>
            {
                ["pontosIguaisSeguidos"] = calculo.ConsecutivePoints,
                ["duracaoCongeladaMinutos"] = calculo.DurationMinutes,
                ["valorCongelado"] = calculo.FrozenValue,
                ["taxaPassosIguaisAntes"] = calculo.PriorZeroRate,
                ["passosAvaliadosAntes"] = calculo.PriorVariationCount,
                ["maiorRepousoAnteriorMinutos"] = maiorRepouso,
                ["repousoConhecido"] = repousoConhecido ? 1 : 0,
                ["pontosHistoricoContiguo"] = contigua.Historico.Count,
                ["minimoMinutos"] = _options.MinimoMinutos,
                ["tipoComportamental"] = (double)perfil.Type
            }
        };
    }

    /// <summary>
    /// 100 quando confirma. Enquanto a sequência ainda não atingiu o mínimo,
    /// mostra o progresso (no máximo 99, para nunca parecer confirmação).
    /// Nos outros casos (variação anterior baixa, repouso conhecido), 0.
    /// </summary>
    private double Nota(
        FrozenSensorEvidenceResult calculo,
        bool confirmado)
    {
        if (confirmado)
            return 100;

        if (calculo.Reason is not ("insufficient-frozen-points" or "insufficient-frozen-duration"))
            return 0;

        var progressoMinutos =
            calculo.DurationMinutes / _options.MinimoMinutos;

        var progressoPontos =
            calculo.ConsecutivePoints / (double)Math.Max(3, _options.MinimoPontos);

        var progresso =
            Math.Min(progressoMinutos, progressoPontos);

        return double.IsFinite(progresso)
            ? Math.Clamp(progresso * 100, 0, 99)
            : 0;
    }

    private string Explicar(
        FrozenSensorEvidenceResult calculo,
        bool confirmado,
        bool repousoConhecido,
        double maiorRepouso)
    {
        if (confirmado)
        {
            return $"Valor parado em {calculo.FrozenValue:G} há {calculo.DurationMinutes:F0} min " +
                   $"({calculo.ConsecutivePoints} leituras iguais), e a tag variava antes " +
                   $"(passos iguais: {calculo.PriorZeroRate:P0}).";
        }

        if (repousoConhecido)
        {
            return $"Valor parado em {calculo.FrozenValue:G} há {calculo.DurationMinutes:F0} min, " +
                   $"mas a tag já ficou parada nesse valor por {maiorRepouso:F0} min no histórico " +
                   "(repouso conhecido).";
        }

        return calculo.Reason switch
        {
            "historically-low-variation" =>
                $"Valor parado, mas a tag já era quase constante antes " +
                $"(passos iguais: {calculo.PriorZeroRate:P0}).",
            "insufficient-frozen-points" or "insufficient-frozen-duration" =>
                $"{calculo.ConsecutivePoints} leituras iguais em {calculo.DurationMinutes:F0} min " +
                $"(mínimo: {_options.MinimoPontos} leituras e {_options.MinimoMinutos:G} min).",
            _ => $"Sem valor congelado ({calculo.Reason})."
        };
    }

    /// <summary>
    /// Maior duração (minutos) em que o histórico ficou parado exatamente em
    /// <paramref name="valor"/>, só antes de <paramref name="antesDe"/> (o
    /// início da sequência atual). Uma sequência termina quando o valor muda
    /// ou quando há gap crítico entre duas leituras (o histórico do regime é
    /// concatenado).
    /// </summary>
    private static double MaiorRepousoAnterior(
        IReadOnlyList<HistoricoDetectorPoint> historico,
        double valor,
        DateTime antesDe,
        double limiteGap)
    {
        var maior = 0.0;
        DateTime? inicio = null;
        DateTime? ultimo = null;

        foreach (var ponto in historico)
        {
            if (ponto.DataHora >= antesDe)
                break;

            var igual =
                double.IsFinite(ponto.Valor) &&
                Math.Abs(ponto.Valor - valor) <= ToleranciaIgualdade;

            var continua =
                igual &&
                ultimo.HasValue &&
                (ponto.DataHora - ultimo.Value).TotalMinutes <= limiteGap;

            if (!igual)
            {
                inicio = null;
                ultimo = null;
                continue;
            }

            if (!continua)
                inicio = ponto.DataHora;

            ultimo = ponto.DataHora;

            maior = Math.Max(
                maior,
                (ultimo.Value - inicio!.Value).TotalMinutes);
        }

        return maior;
    }

    private static double LimiteGapCritico(
        DetectorInput input)
    {
        var qualidade = input.QualidadeHistorico;

        if (double.IsFinite(qualidade.CriticalGapThresholdMinutes) &&
            qualidade.CriticalGapThresholdMinutes > 0)
        {
            return qualidade.CriticalGapThresholdMinutes;
        }

        if (double.IsFinite(qualidade.MedianIntervalMinutes) &&
            qualidade.MedianIntervalMinutes > 0)
        {
            return qualidade.MedianIntervalMinutes * 5.0;
        }

        return double.PositiveInfinity;
    }

    private DetectorResult NaoAplicavel(
        string explicacao,
        string reasonCode,
        TagBehaviorProfile? perfil = null) => new()
    {
        Aplicavel = false,
        EhAnomalia = false,
        Score = 0,
        ScoreNormalizado = 0,
        Peso = Peso,
        Detector = Nome,
        EvidenceRole = DetectorEvidenceRole.IndependentEvidence,
        Explicacao = explicacao,
        NonApplicabilityReasonCode = reasonCode,
        Metricas = perfil is null
            ? new Dictionary<string, double>()
            : new Dictionary<string, double>
            {
                ["tipoComportamental"] = (double)perfil.Type
            }
    };
}
