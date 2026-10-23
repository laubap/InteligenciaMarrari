using System.Diagnostics;
using System.Text;
using IAApi.IntelligenceLab.Observability;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Executa os detectores registrados e combina os resultados
/// por média ponderada.
///
/// Esta versão mantém a mesma matemática do ensemble e adiciona
/// um profiler interno para medir o tempo acumulado de cada detector.
///
/// O relatório é exibido periodicamente no terminal durante o benchmark.
/// </summary>
public class EnsembleDetectionService
{
    private const long IntervaloRelatorioProfiler = 100_000;

    private readonly IAnomalyDetector[] _detectores;
    private readonly AnomalyDetectionOptions _options;
    private readonly TagBehaviorProfileResolver _tagBehaviorProfileResolver;
    private readonly DetectorApplicabilityPolicy _detectorApplicabilityPolicy;

    private readonly long[] _ticksPorDetector;
    private readonly long[] _chamadasPorDetector;
    private readonly long[] _errosPorDetector;

    private long _totalExecucoesEnsemble;
    private long _ticksTotalDetectores;

    public EnsembleDetectionService(
        IEnumerable<IAnomalyDetector> detectores,
        IOptions<AnomalyDetectionOptions> options,
        TagBehaviorProfileResolver tagBehaviorProfileResolver,
        DetectorApplicabilityPolicy detectorApplicabilityPolicy)
    {
        ArgumentNullException.ThrowIfNull(detectores);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tagBehaviorProfileResolver);
        ArgumentNullException.ThrowIfNull(detectorApplicabilityPolicy);

        _detectores = detectores.ToArray();
        _options = options.Value;
        _tagBehaviorProfileResolver = tagBehaviorProfileResolver;
        _detectorApplicabilityPolicy = detectorApplicabilityPolicy;

        _ticksPorDetector =
            new long[_detectores.Length];

        _chamadasPorDetector =
            new long[_detectores.Length];

        _errosPorDetector =
            new long[_detectores.Length];
    }

    public DetectorResult Detectar(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_detectores.Length == 0)
        {
            throw new InvalidOperationException(
                "Nenhum detector de anomalias foi registrado.");
        }

        var validacaoEntrada =
            DetectionInputValidator.Validate(
                input);

        if (!validacaoEntrada.IsValid)
        {
            return CriarResultadoEntradaInvalida(
                input,
                validacaoEntrada);
        }

        var perfilComportamental =
            input.ObterPerfilComportamental(
                _tagBehaviorProfileResolver);

        _totalExecucoesEnsemble++;

        var todosResultados =
            new DetectorResult[_detectores.Length];

        var statusExecucao =
            new DetectorDiagnosticStatus[_detectores.Length];

        var quantidadeAplicaveis = 0;
        var quantidadeAlertas = 0;

        var somaPesos = 0.0;
        var somaScoresPonderados = 0.0;

        DetectorResult? resultadoReferencia = null;
        DetectorResult? resultadoZScore = null;

        for (var index = 0;
             index < _detectores.Length;
             index++)
        {
            var detector =
                _detectores[index];

            DetectorResult resultado;

            var aplicabilidade =
                _detectorApplicabilityPolicy.Evaluate(
                    detector,
                    perfilComportamental);

            if (!aplicabilidade.IsAllowed)
            {
                resultado =
                    CriarResultadoDetectorBloqueado(
                        detector,
                        perfilComportamental,
                        aplicabilidade);

                statusExecucao[index] =
                    DetectorDiagnosticStatus.NotApplicable;

                todosResultados[index] =
                    resultado;

                continue;
            }

            var inicioDetector =
                Stopwatch.GetTimestamp();

            try
            {
                resultado =
                    detector.Detectar(input);

                statusExecucao[index] =
                    resultado.Aplicavel
                        ? DetectorDiagnosticStatus.Executed
                        : DetectorDiagnosticStatus.NotApplicable;
            }
            catch (Exception ex)
            {
                _errosPorDetector[index]++;

                statusExecucao[index] =
                    DetectorDiagnosticStatus.Failed;

                resultado = new DetectorResult
                {
                    Aplicavel = false,
                    EhAnomalia = false,
                    Score = 0,
                    ScoreNormalizado = 0,
                    Peso = detector.Peso,
                    Detector = detector.Nome,
                    Explicacao =
                        $"Erro ao executar o detector: {ex.Message}"
                };
            }
            finally
            {
                var ticksConsumidos =
                    Stopwatch.GetTimestamp() -
                    inicioDetector;

                _ticksPorDetector[index] +=
                    ticksConsumidos;

                _chamadasPorDetector[index]++;

                _ticksTotalDetectores +=
                    ticksConsumidos;
            }

            todosResultados[index] =
                resultado;

            if (!resultado.Aplicavel)
            {
                continue;
            }

            quantidadeAplicaveis++;

            somaPesos +=
                resultado.Peso;

            somaScoresPonderados +=
                resultado.ScoreNormalizado *
                resultado.Peso;

            if (resultado.EhAnomalia)
            {
                quantidadeAlertas++;
            }

            resultadoReferencia ??=
                resultado;

            if (string.Equals(
                    resultado.Detector,
                    "Z-Score",
                    StringComparison.Ordinal))
            {
                resultadoZScore =
                    resultado;
            }
        }

        if (_totalExecucoesEnsemble %
            IntervaloRelatorioProfiler == 0)
        {
            ExibirRelatorioProfiler();
        }

        var temporalConfirmations = todosResultados
            .Where(x => x.Aplicavel &&
                        x.EvidenceRole == DetectorEvidenceRole.TemporalConfirmation)
            .ToArray();

        if (_options.ExperimentalPersistenceTemporalConfirmationEnabled)
        {
            var madIsPositive = todosResultados.Any(x =>
                x.Aplicavel &&
                x.EhAnomalia &&
                string.Equals(x.Detector, "Mediana e MAD", StringComparison.Ordinal));

            if (madIsPositive)
            {
                var included = todosResultados.Where(x =>
                    EnsembleEvidenceParticipationPolicy
                        .ParticipatesInStatisticalAggregation(
                            x,
                            experimentalTemporalConfirmationEnabled: true,
                            underlyingMadIsPositive: madIsPositive)).ToArray();

                quantidadeAplicaveis = included.Length;
                quantidadeAlertas = included.Count(x => x.EhAnomalia);
                somaPesos = included.Sum(x => x.Peso);
                somaScoresPonderados = included.Sum(x => x.ScoreNormalizado * x.Peso);
            }
        }

        var detalhes =
            CriarDetalhes(
                todosResultados,
                statusExecucao);

        if (quantidadeAplicaveis == 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                EhAnomalia = false,
                Score = 0,
                ScoreNormalizado = 0,
                Peso = 0,
                LimiteInferior = 0,
                LimiteSuperior = 0,
                Media = input.MediaHistorica,
                DesvioPadrao =
                    input.DesvioPadraoHistorico,
                Detector =
                    "Ensemble ponderado",
                Explicacao =
                    "Nenhum detector foi aplicável para esta leitura.",
                Metricas =
                    new Dictionary<string, double>(12)
                    {
                        ["scoreFinalNormalizado"] = 0,
                        ["limiteEnsemble"] =
                            _options.EnsembleThreshold,
                        ["quantidadeDetectoresRegistrados"] =
                            todosResultados.Length,
                        ["quantidadeDetectoresAplicaveis"] = 0,
                        ["quantidadeAlertas"] = 0,
                        ["somaPesosAplicaveis"] = 0,
                        ["tipoComportamental"] =
                            (double)perfilComportamental.Type,
                        ["confiancaTipoComportamental"] =
                            perfilComportamental.Confidence,
                        ["roteamentoTipoAtivo"] =
                            perfilComportamental.IsHighConfidence(
                                DetectorApplicabilityPolicy.MinimumRoutingConfidence)
                                ? 1
                                : 0,
                        ["persistenceTemporalExperimentalAtivo"] =
                            _options.ExperimentalPersistenceTemporalConfirmationEnabled ? 1 : 0,
                        ["quantidadeConfirmacoesTemporais"] = temporalConfirmations.Length,
                        ["quantidadeConfirmacoesTemporaisConfirmadas"] =
                            temporalConfirmations.Count(x =>
                                x.TemporalConfirmation?.Status == TemporalConfirmationStatus.Confirmed)
                    },
                DetalhesDetectores =
                    detalhes
            };
        }

        if (somaPesos <= 0 ||
            !double.IsFinite(somaPesos))
        {
            throw new InvalidOperationException(
                "A soma dos pesos dos detectores aplicáveis deve ser maior que zero.");
        }

        var scoreFinalNormalizado =
            somaScoresPonderados /
            somaPesos;

        var ehAnomaliaFinal =
            scoreFinalNormalizado >=
            _options.EnsembleThreshold;

        resultadoReferencia =
            resultadoZScore ??
            resultadoReferencia ??
            throw new InvalidOperationException(
                "Não foi possível determinar um detector de referência.");

        return new DetectorResult
        {
            Aplicavel = true,

            EhAnomalia =
                ehAnomaliaFinal,

            Score =
                scoreFinalNormalizado / 20.0,

            ScoreNormalizado =
                scoreFinalNormalizado,

            Peso = 1,

            LimiteInferior =
                resultadoReferencia.LimiteInferior,

            LimiteSuperior =
                resultadoReferencia.LimiteSuperior,

            Media =
                input.MediaHistorica,

            DesvioPadrao =
                input.DesvioPadraoHistorico,

            Detector =
                "Ensemble ponderado",

            Explicacao =
                CriarExplicacao(
                    todosResultados),

            Metricas =
                new Dictionary<string, double>(12)
                {
                    ["scoreFinalNormalizado"] =
                        scoreFinalNormalizado,

                    ["limiteEnsemble"] =
                        _options.EnsembleThreshold,

                    ["quantidadeDetectoresRegistrados"] =
                        todosResultados.Length,

                    ["quantidadeDetectoresAplicaveis"] =
                        quantidadeAplicaveis,

                    ["quantidadeAlertas"] =
                        quantidadeAlertas,

                    ["somaPesosAplicaveis"] =
                        somaPesos,

                    ["tipoComportamental"] =
                        (double)perfilComportamental.Type,

                    ["confiancaTipoComportamental"] =
                        perfilComportamental.Confidence,

                    ["roteamentoTipoAtivo"] =
                        perfilComportamental.IsHighConfidence(
                            DetectorApplicabilityPolicy.MinimumRoutingConfidence)
                            ? 1
                            : 0,

                    ["persistenceTemporalExperimentalAtivo"] =
                        _options.ExperimentalPersistenceTemporalConfirmationEnabled ? 1 : 0,

                    ["quantidadeConfirmacoesTemporais"] =
                        temporalConfirmations.Length,

                    ["quantidadeConfirmacoesTemporaisConfirmadas"] =
                        temporalConfirmations.Count(x =>
                            x.TemporalConfirmation?.Status == TemporalConfirmationStatus.Confirmed)
                },

            DetalhesDetectores =
                detalhes
        };
    }


    private static DetectorResult CriarResultadoDetectorBloqueado(
        IAnomalyDetector detector,
        TagBehaviorProfile profile,
        DetectorApplicabilityDecision applicability)
    {
        return new DetectorResult
        {
            Aplicavel = false,
            EhAnomalia = false,
            Score = 0,
            ScoreNormalizado = 0,
            Peso = detector.Peso,
            LimiteInferior = 0,
            LimiteSuperior = 0,
            Media = 0,
            DesvioPadrao = 0,
            Detector = detector.Nome,
            Explicacao =
                $"{applicability.Reason} " +
                $"Perfil={profile.Type}; " +
                $"confiança={profile.Confidence:F3}. " +
                $"{profile.Reason}",
            Metricas =
                new Dictionary<string, double>
                {
                    ["tipoComportamental"] =
                        (double)profile.Type,
                    ["confiancaTipoComportamental"] =
                        profile.Confidence,
                    ["bloqueadoPorRoteamento"] = 1,
                    ["amostrasClassificacao"] =
                        profile.SampleCount,
                    ["valoresDistintos"] =
                        profile.DistinctValueCount,
                    ["proporcaoValoresDistintos"] =
                        profile.DistinctValueRatio,
                    ["proporcaoValoresInteiros"] =
                        profile.IntegerLikeRatio,
                    ["proporcaoNaoDecrescente"] =
                        profile.NonDecreasingRatio
                }
        };
    }

    private DetectorResult CriarResultadoEntradaInvalida(
        DetectorInput input,
        DetectionInputValidationResult validation)
    {
        var quality =
            input.QualidadeHistorico;

        return new DetectorResult
        {
            Aplicavel = false,
            EhAnomalia = false,
            Score = 0,
            ScoreNormalizado = 0,
            Peso = 0,
            LimiteInferior = 0,
            LimiteSuperior = 0,
            Media =
                double.IsFinite(input.MediaHistorica)
                    ? input.MediaHistorica
                    : 0,
            DesvioPadrao =
                double.IsFinite(input.DesvioPadraoHistorico)
                    ? input.DesvioPadraoHistorico
                    : 0,
            Detector =
                "Validação de entrada",
            Explicacao =
                "Entrada inválida para detecção: " +
                string.Join(
                    " | ",
                    validation.Errors),
            Metricas =
                new Dictionary<string, double>
                {
                    ["historicoOriginal"] =
                        quality.OriginalPoints,
                    ["historicoValido"] =
                        quality.ValidPoints,
                    ["valoresInvalidos"] =
                        quality.InvalidValues,
                    ["timestampsInvalidos"] =
                        quality.InvalidTimestamps,
                    ["timestampsDuplicados"] =
                        quality.DuplicateTimestamps,
                    ["transicoesForaDeOrdem"] =
                        quality.OutOfOrderTransitions,
                    ["intervaloMedianoMinutos"] =
                        quality.MedianIntervalMinutes,
                    ["maiorGapMinutos"] =
                        quality.LargestGapMinutes,
                    ["possuiGapCritico"] =
                        quality.HasCriticalGap
                            ? 1
                            : 0
                }
        };
    }

    /// <summary>
    /// Exibe o tempo acumulado e médio de cada detector.
    ///
    /// O relatório é ordenado do detector mais caro para o mais barato.
    /// </summary>
    public void ExibirRelatorioProfiler()
    {
        if (_detectores.Length == 0)
        {
            return;
        }

        var linhas =
            new List<DetectorPerformanceLine>(
                _detectores.Length);

        for (var index = 0;
             index < _detectores.Length;
             index++)
        {
            var chamadas =
                _chamadasPorDetector[index];

            var ticks =
                _ticksPorDetector[index];

            var segundos =
                TicksParaSegundos(
                    ticks);

            var percentual =
                _ticksTotalDetectores > 0
                    ? ticks * 100.0 /
                      _ticksTotalDetectores
                    : 0;

            var microssegundosMedios =
                chamadas > 0
                    ? segundos *
                      1_000_000.0 /
                      chamadas
                    : 0;

            linhas.Add(
                new DetectorPerformanceLine
                {
                    Nome =
                        _detectores[index].Nome,

                    Chamadas =
                        chamadas,

                    Segundos =
                        segundos,

                    Percentual =
                        percentual,

                    MicrossegundosMedios =
                        microssegundosMedios,

                    Erros =
                        _errosPorDetector[index]
                });
        }

        linhas.Sort(
            static (left, right) =>
                right.Segundos.CompareTo(
                    left.Segundos));

        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "       PROFILER DOS DETECTORES");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Execuções do ensemble: " +
            $"{_totalExecucoesEnsemble:N0}");

        Console.WriteLine(
            $"Tempo total nos detectores: " +
            $"{TicksParaSegundos(_ticksTotalDetectores):N2}s");

        Console.WriteLine(
            "----------------------------------------");

        foreach (var linha in linhas)
        {
            Console.WriteLine(
                $"{linha.Nome,-24} | " +
                $"{linha.Percentual,6:F2}% | " +
                $"{linha.Segundos,10:F2}s | " +
                $"{linha.MicrossegundosMedios,10:F2} µs/chamada | " +
                $"chamadas {linha.Chamadas,10:N0} | " +
                $"erros {linha.Erros:N0}");
        }

        Console.WriteLine(
            "========================================");
        Console.WriteLine();
    }

    private static List<DetectorDebugItem>
        CriarDetalhes(
            IReadOnlyList<DetectorResult> resultados,
            IReadOnlyList<DetectorDiagnosticStatus> statusExecucao)
    {
        var detalhes =
            new List<DetectorDebugItem>(
                resultados.Count);

        for (var index = 0;
             index < resultados.Count;
             index++)
        {
            var resultado =
                resultados[index];

            detalhes.Add(
                new DetectorDebugItem
                {
                    Nome =
                        resultado.Detector,

                    Aplicavel =
                        resultado.Aplicavel,

                    VotouAnomalia =
                        resultado.Aplicavel &&
                        resultado.EhAnomalia,

                    ScoreNormalizado =
                        resultado.ScoreNormalizado,

                    ScoreOriginal =
                        resultado.Score,

                    Peso =
                        resultado.Peso,

                    Explicacao =
                        resultado.Explicacao,

                    Metricas =
                        resultado.Metricas,

                    EvidenceRole =
                        resultado.EvidenceRole,

                    UnderlyingEvidenceKind =
                        resultado.UnderlyingEvidenceKind,

                    TemporalConfirmation =
                        resultado.TemporalConfirmation,

                    ExecutionStatus =
                        statusExecucao[index]
                });
        }

        return detalhes;
    }

    private static string CriarExplicacao(
        IReadOnlyList<DetectorResult> resultados)
    {
        var builder =
            new StringBuilder(
                resultados.Count * 96);

        for (var index = 0;
             index < resultados.Count;
             index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
            }

            var resultado =
                resultados[index];

            builder.Append(
                resultado.Detector);

            builder.Append(": ");

            builder.Append(
                resultado.Explicacao);
        }

        return builder.ToString();
    }

    private static double TicksParaSegundos(
        long ticks)
    {
        return ticks /
            (double)Stopwatch.Frequency;
    }

    private sealed class DetectorPerformanceLine
    {
        public string Nome { get; init; } = "";

        public long Chamadas { get; init; }

        public double Segundos { get; init; }

        public double Percentual { get; init; }

        public double MicrossegundosMedios { get; init; }

        public long Erros { get; init; }
    }
}
