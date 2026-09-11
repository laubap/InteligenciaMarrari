using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Simula regras alternativas de ensemble sem alterar a decisão atual.
///
/// Entrada:
/// - DetectorInput usado na leitura;
/// - DetectorResult produzido pelo EnsembleDetectionService atual.
///
/// Saída:
/// - decisão atual;
/// - simulação baseada em votos;
/// - simulação por famílias;
/// - detalhes convertidos para DetectorResultV2.
///
/// IMPORTANTE:
/// esta classe não substitui o EnsembleDetectionService e não modifica
/// o comportamento da API.
/// </summary>
public sealed class EnsembleV2Simulator
{
    private readonly AnomalyDetectionOptions _options;

    public EnsembleV2Simulator(
        IOptions<AnomalyDetectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public EnsembleSimulationResult Simular(
        DetectorInput input,
        DetectorResult resultadoAtual)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resultadoAtual);

        var resultadosV2 =
            ConverterResultados(
                input,
                resultadoAtual.DetalhesDetectores);

        var simulacaoVotos =
            SimularPorVotos(
                resultadosV2);

        var simulacaoFamilias =
            SimularPorFamilias(
                resultadosV2);

        return new EnsembleSimulationResult
        {
            DetectorInputTagName =
                input.TagName,

            DataHoraAtual =
                input.DataHoraAtual,

            DecisaoAtual =
                new CurrentEnsembleSnapshot
                {
                    Aplicavel =
                        resultadoAtual.Aplicavel,

                    EhAnomalia =
                        resultadoAtual.EhAnomalia,

                    ScoreNormalizado =
                        resultadoAtual.ScoreNormalizado,

                    Threshold =
                        _options.EnsembleThreshold,

                    QuantidadeDetectoresAplicaveis =
                        GetMetric(
                            resultadoAtual,
                            "quantidadeDetectoresAplicaveis"),

                    QuantidadeAlertas =
                        GetMetric(
                            resultadoAtual,
                            "quantidadeAlertas")
                },

            SimulacaoPorVotos =
                simulacaoVotos,

            SimulacaoPorFamilias =
                simulacaoFamilias,

            ResultadosV2 =
                resultadosV2,

            HouveDivergenciaComRegraDeVotos =
                resultadoAtual.EhAnomalia !=
                simulacaoVotos.EhAnomalia,

            HouveDivergenciaComRegraPorFamilias =
                resultadoAtual.EhAnomalia !=
                simulacaoFamilias.EhAnomalia
        };
    }

    private List<DetectorResultV2> ConverterResultados(
        DetectorInput input,
        IReadOnlyCollection<DetectorDebugItem> detalhes)
    {
        var result =
            new List<DetectorResultV2>(
                detalhes.Count);

        foreach (var detail in detalhes)
        {
            var family =
                ResolveFamily(
                    detail.Nome);

            var baseWeight =
                Clamp01(
                    detail.Peso);

            if (!detail.Aplicavel)
            {
                result.Add(
                    DetectorResultV2.NaoAplicavel(
                        detail.Nome,
                        family,
                        baseWeight,
                        string.IsNullOrWhiteSpace(
                            detail.Explicacao)
                            ? "Detector não aplicável na execução atual."
                            : detail.Explicacao));

                continue;
            }

            var rawThreshold =
                ResolveRawThreshold(
                    detail.Nome);

            var evidence =
                ConvertRawScoreToEvidence(
                    detail.ScoreOriginal,
                    rawThreshold);

            var confidence =
                CalculateConfidence(
                    input,
                    detail);

            /*
             * Nesta fase a severidade é somente um proxy estatístico.
             * Ela NÃO representa ainda impacto físico ou operacional.
             */
            var severity =
                CalculateStatisticalSeverityProxy(
                    detail.ScoreOriginal,
                    rawThreshold);

            var limits =
                CreateLimits(
                    detail,
                    rawThreshold);

            result.Add(
                DetectorResultV2.AplicavelComResultado(
                    detector:
                        detail.Nome,

                    family:
                        family,

                    votouAnomalia:
                        detail.VotouAnomalia,

                    evidencia:
                        evidence,

                    severidade:
                        severity,

                    confianca:
                        confidence,

                    pesoBase:
                        baseWeight,

                    explicacao:
                        detail.Explicacao,

                    limites:
                        limits,

                    metricas:
                        detail.Metricas));
        }

        return result;
    }

    /// <summary>
    /// Regra experimental simples:
    /// - somente detectores que realmente votaram anomalia contribuem;
    /// - detector não aplicável ou que não votou não soma evidência;
    /// - um único detector produz suspeita, não confirmação;
    /// - dois ou mais votos podem confirmar, conforme score.
    /// </summary>
    private static EnsembleStrategyResult SimularPorVotos(
        IReadOnlyCollection<DetectorResultV2> results)
    {
        var applicable =
            results
                .Where(item => item.Aplicavel)
                .ToList();

        var positive =
            applicable
                .Where(item => item.VotouAnomalia)
                .ToList();

        if (applicable.Count == 0)
        {
            return EnsembleStrategyResult.NotApplicable(
                "Nenhum detector foi aplicável.");
        }

        if (positive.Count == 0)
        {
            return EnsembleStrategyResult.Normal(
                "Nenhum detector aplicável votou anomalia.");
        }

        var weightedEvidence =
            WeightedAverage(
                positive,
                item =>
                    item.Evidencia *
                    item.Confianca,
                item =>
                    Math.Max(
                        item.PesoBase,
                        0.01));

        var status =
            positive.Count >= 2 &&
            weightedEvidence >= 0.55
                ? EnsembleDecisionStatus.ConfirmedAnomaly
                : EnsembleDecisionStatus.PendingSuspicion;

        return new EnsembleStrategyResult
        {
            Aplicavel = true,
            EhAnomalia =
                status ==
                EnsembleDecisionStatus.ConfirmedAnomaly,

            Status =
                status,

            Score =
                weightedEvidence,

            QuantidadeDetectoresAplicaveis =
                applicable.Count,

            QuantidadeVotosPositivos =
                positive.Count,

            QuantidadeFamiliasPositivas =
                positive
                    .Select(item => item.Family)
                    .Distinct()
                    .Count(),

            Explicacao =
                status ==
                EnsembleDecisionStatus.ConfirmedAnomaly
                    ? "Dois ou mais detectores votaram anomalia com evidência ponderada suficiente."
                    : "Existe evidência anômala, mas ainda sem confirmação mínima por quantidade de votos."
        };
    }

    /// <summary>
    /// Regra experimental por famílias:
    /// - somente votos positivos participam;
    /// - detectores da mesma família são consolidados antes do score final;
    /// - uma família isolada gera suspeita pendente;
    /// - duas famílias independentes podem confirmar;
    /// - não existe renormalização que dê autoridade total automática
    ///   a um detector isolado.
    /// </summary>
    private static EnsembleStrategyResult SimularPorFamilias(
        IReadOnlyCollection<DetectorResultV2> results)
    {
        var applicable =
            results
                .Where(item => item.Aplicavel)
                .ToList();

        var positive =
            applicable
                .Where(item => item.VotouAnomalia)
                .ToList();

        if (applicable.Count == 0)
        {
            return EnsembleStrategyResult.NotApplicable(
                "Nenhum detector foi aplicável.");
        }

        if (positive.Count == 0)
        {
            return EnsembleStrategyResult.Normal(
                "Nenhuma família apresentou voto anômalo.");
        }

        var families =
            positive
                .GroupBy(item => item.Family)
                .Select(
                    group =>
                        CreateFamilyResult(
                            group.Key,
                            group.ToList()))
                .OrderByDescending(
                    item => item.Score)
                .ToList();

        var familyScore =
            families.Count > 0
                ? families.Average(
                    item => item.Score)
                : 0;

        var hasTwoIndependentFamilies =
            families.Count >= 2;

        var hasStrongIndependentAgreement =
            families.Count(
                item => item.Score >= 0.55) >= 2;

        var confirmed =
            hasTwoIndependentFamilies &&
            hasStrongIndependentAgreement &&
            familyScore >= 0.58;

        var status =
            confirmed
                ? EnsembleDecisionStatus.ConfirmedAnomaly
                : EnsembleDecisionStatus.PendingSuspicion;

        return new EnsembleStrategyResult
        {
            Aplicavel = true,
            EhAnomalia =
                confirmed,

            Status =
                status,

            Score =
                familyScore,

            QuantidadeDetectoresAplicaveis =
                applicable.Count,

            QuantidadeVotosPositivos =
                positive.Count,

            QuantidadeFamiliasPositivas =
                families.Count,

            Familias =
                families,

            Explicacao =
                confirmed
                    ? "A anomalia foi confirmada por pelo menos duas famílias independentes com evidência suficiente."
                    : "Há evidência anômala, mas ela está concentrada em uma única família ou ainda é insuficiente."
        };
    }

    private static FamilySimulationResult CreateFamilyResult(
        DetectorFamily family,
        IReadOnlyCollection<DetectorResultV2> results)
    {
        /*
         * Dentro da família usamos uma combinação limitada:
         * 70% do maior resultado + 30% da média.
         *
         * Isso permite reforço interno sem tratar detectores correlacionados
         * como evidências totalmente independentes.
         */
        var individualScores =
            results
                .Select(
                    item =>
                        Clamp01(
                            item.Evidencia *
                            item.Confianca))
                .ToList();

        var maximum =
            individualScores.Max();

        var average =
            individualScores.Average();

        var familyScore =
            Clamp01(
                (maximum * 0.70) +
                (average * 0.30));

        return new FamilySimulationResult
        {
            Family =
                family,

            Score =
                familyScore,

            DetectorCount =
                results.Count,

            Detectors =
                results
                    .Select(item => item.Detector)
                    .OrderBy(
                        item => item,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
    }

    private double ResolveRawThreshold(
        string detectorName)
    {
        if (EqualsName(
                detectorName,
                "Z-Score"))
        {
            return _options.ZScoreThreshold;
        }

        if (EqualsName(
                detectorName,
                "Mediana e MAD"))
        {
            return _options.MadThreshold;
        }

        if (EqualsName(
                detectorName,
                "EWMA"))
        {
            return _options.EwmaSigmaLimit;
        }

        if (EqualsName(
                detectorName,
                "Taxa de variação"))
        {
            return _options.RateOfChangeThreshold;
        }

        if (EqualsName(
                detectorName,
                "Mudança de patamar"))
        {
            return _options.LevelShiftThreshold;
        }

        /*
         * Persistência usa um score interno diferente.
         * No código atual, anomalia completa corresponde aproximadamente
         * a Score = 5.
         */
        if (EqualsName(
                detectorName,
                "Persistência"))
        {
            return 5.0;
        }

        /*
         * Para detectores sem threshold conhecido nesta etapa,
         * tratamos ScoreNormalizado/100 como aproximação por meio do
         * CreateLimits e do conversor de evidência.
         */
        return 1.0;
    }

    private static DetectorFamily ResolveFamily(
        string detectorName)
    {
        if (EqualsName(
                detectorName,
                "Z-Score") ||
            EqualsName(
                detectorName,
                "Mediana e MAD"))
        {
            return DetectorFamily.StatisticalDeviation;
        }

        if (EqualsName(
                detectorName,
                "EWMA"))
        {
            return DetectorFamily.TrendAndSmoothing;
        }

        if (EqualsName(
                detectorName,
                "Taxa de variação") ||
            EqualsName(
                detectorName,
                "Mudança de patamar"))
        {
            return DetectorFamily.Transition;
        }

        if (EqualsName(
                detectorName,
                "Persistência"))
        {
            return DetectorFamily.TemporalConfirmation;
        }

        if (detectorName.StartsWith(
                "Multivariado/",
                StringComparison.OrdinalIgnoreCase))
        {
            return DetectorFamily.Multivariate;
        }

        if (detectorName.Contains(
                "Isolation",
                StringComparison.OrdinalIgnoreCase))
        {
            return DetectorFamily.MachineLearning;
        }

        return DetectorFamily.Unknown;
    }

    private static double ConvertRawScoreToEvidence(
        double rawScore,
        double threshold)
    {
        if (!double.IsFinite(rawScore) ||
            rawScore <= 0)
        {
            return 0;
        }

        if (!double.IsFinite(threshold) ||
            threshold <= 0)
        {
            return 0;
        }

        var ratio =
            rawScore /
            threshold;

        /*
         * Curva suave que:
         * - não satura ao cruzar o threshold;
         * - retorna 0.5 exatamente no threshold;
         * - cresce gradualmente acima dele.
         */
        var evidence =
            1.0 -
            Math.Pow(
                0.5,
                Math.Max(
                    ratio,
                    0));

        return Clamp01(
            evidence);
    }

    private double CalculateConfidence(
        DetectorInput input,
        DetectorDebugItem detail)
    {
        var historyCount =
            input.Historico.Count;

        var targetHistory =
            Math.Max(
                _options.MinHistoryPoints,
                1);

        var historyComponent =
            Math.Clamp(
                historyCount /
                (double)(targetHistory * 4),
                0.25,
                1.0);

        var validMetricCount =
            detail.Metricas
                .Values
                .Count(double.IsFinite);

        var metricComponent =
            detail.Metricas.Count == 0
                ? 0.65
                : Math.Clamp(
                    validMetricCount /
                    (double)detail.Metricas.Count,
                    0,
                    1);

        return Clamp01(
            (historyComponent * 0.75) +
            (metricComponent * 0.25));
    }

    private static double CalculateStatisticalSeverityProxy(
        double rawScore,
        double threshold)
    {
        /*
         * Proxy temporário. O ADR001 exige que severidade física seja
         * calculada posteriormente com perfil da tag e limites operacionais.
         */
        return ConvertRawScoreToEvidence(
            rawScore,
            threshold);
    }

    private static LimitesDetector? CreateLimits(
        DetectorDebugItem detail,
        double rawThreshold)
    {
        if (!double.IsFinite(rawThreshold))
        {
            return null;
        }

        return new LimitesDetector
        {
            Threshold =
                rawThreshold
        };
    }

    private static double WeightedAverage(
        IReadOnlyCollection<DetectorResultV2> items,
        Func<DetectorResultV2, double> valueSelector,
        Func<DetectorResultV2, double> weightSelector)
    {
        var weightedSum =
            0.0;

        var weightSum =
            0.0;

        foreach (var item in items)
        {
            var weight =
                weightSelector(item);

            if (!double.IsFinite(weight) ||
                weight <= 0)
            {
                continue;
            }

            var value =
                valueSelector(item);

            if (!double.IsFinite(value))
            {
                continue;
            }

            weightedSum +=
                value * weight;

            weightSum +=
                weight;
        }

        return weightSum > 0
            ? Clamp01(
                weightedSum /
                weightSum)
            : 0;
    }

    private static bool EqualsName(
        string left,
        string right)
    {
        return string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);
    }

    private static double GetMetric(
        DetectorResult result,
        string name)
    {
        return result.Metricas.TryGetValue(
                name,
                out var value)
            ? value
            : 0;
    }

    private static double Clamp01(
        double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Clamp(
            value,
            0,
            1);
    }
}
