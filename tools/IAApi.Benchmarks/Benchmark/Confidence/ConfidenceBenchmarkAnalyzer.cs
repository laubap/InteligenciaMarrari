using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark;

/// <summary>
/// Converte a topologia dos eventos estruturais em evidências proxy e
/// executa o Confidence Engine.
///
/// Limitação:
/// o CSV estrutural não contém os scores V2 reais de cada detector.
/// Portanto esta classe usa forças/confianças proxy e serve para testar
/// a arquitetura de níveis, não para aprovar thresholds finais.
/// </summary>
public sealed class ConfidenceBenchmarkAnalyzer
{
    private readonly ConfidenceEngine _confidenceEngine;
    private readonly EvidenceBuilder _evidenceBuilder;

    public ConfidenceBenchmarkAnalyzer(
        ConfidenceEngine confidenceEngine,
        EvidenceBuilder evidenceBuilder)
    {
        _confidenceEngine = confidenceEngine;
        _evidenceBuilder = evidenceBuilder;
    }

    public ConfidenceBenchmarkReport Analyze(
        string sourceFile,
        IReadOnlyCollection<ConfidenceBenchmarkEvent> events)
    {
        var evaluated =
            events
                .Select(EvaluateEvent)
                .ToList();

        var total =
            evaluated.Count;

        var suspicious =
            evaluated.LongCount(
                item =>
                    item.Level ==
                    ConfidenceLevel.Suspicious.ToString());

        var probable =
            evaluated.LongCount(
                item =>
                    item.Level ==
                    ConfidenceLevel.Probable.ToString());

        var confirmed =
            evaluated.LongCount(
                item =>
                    item.Level ==
                    ConfidenceLevel.Confirmed.ToString());

        return new ConfidenceBenchmarkReport
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            SourceFile =
                sourceFile,

            TotalEvents =
                total,

            TagCount =
                events
                    .Select(item => item.TagName)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count(),

            CategoryCount =
                events
                    .Select(item => item.Category)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count(),

            SuspiciousEvents =
                suspicious,

            ProbableEvents =
                probable,

            ConfirmedEvents =
                confirmed,

            SuspiciousRate =
                total > 0
                    ? suspicious / (double)total
                    : 0,

            ProbableRate =
                total > 0
                    ? probable / (double)total
                    : 0,

            ConfirmedRate =
                total > 0
                    ? confirmed / (double)total
                    : 0,

            ImportantWarning =
                "Este benchmark usa evidências proxy derivadas apenas dos " +
                "detectores participantes no CSV estrutural. Ele valida a " +
                "arquitetura Suspeito/Provável/Confirmado, mas não substitui " +
                "um benchmark com scores V2 reais nem a avaliação Oracle.",

            Levels =
                BuildLevelSummaries(
                    evaluated,
                    total),

            ByCategory =
                BuildCategorySummaries(
                    evaluated),

            IsolatedDetectors =
                BuildIsolatedDetectorSummaries(
                    evaluated),

            ByFamilyCount =
                BuildFamilyCountSummaries(
                    evaluated)
        };
    }

    private ConfidenceBenchmarkEvaluatedEvent EvaluateEvent(
        ConfidenceBenchmarkEvent item)
    {
        var evidences =
            item.Detectors
                .Select(
                    detector =>
                        CreateProxyEvidence(
                            detector,
                            item))
                .ToList();

        var collection =
            new EvidenceCollection(
                evidences);

        var result =
            _confidenceEngine.Evaluate(
                collection);

        return new ConfidenceBenchmarkEvaluatedEvent
        {
            Source =
                item,

            Level =
                result.Level.ToString(),

            Score =
                result.Score,

            PositiveFamilyCount =
                result.PositiveFamilyCount,

            HasTemporalConfirmation =
                result.HasTemporalConfirmation,

            HasMultivariateEvidence =
                result.HasMultivariateEvidence,

            Explanation =
                result.Explanation
        };
    }

    private Evidence CreateProxyEvidence(
        string detector,
        ConfidenceBenchmarkEvent item)
    {
        var family =
            ResolveFamily(
                detector);

        if (family == DetectorFamily.Unknown)
        {
            throw new InvalidOperationException(
                $"Não foi possível mapear a família do detector '{detector}'.");
        }

        var confidence =
            item.StructuralClassificationConfidence > 0
                ? Math.Clamp(
                    item.StructuralClassificationConfidence / 100.0,
                    0.55,
                    0.98)
                : 0.85;

        var strength =
            ResolveProxyStrength(
                detector,
                item);

        var severity =
            ResolveProxySeverity(
                item);

        return _evidenceBuilder.Applicable(
            detector:
                detector,

            family:
                family,

            positiveVote:
                true,

            strength:
                strength,

            confidence:
                confidence,

            severity:
                severity,

            baseWeight:
                1.0,

            explanation:
                "Evidência proxy criada a partir da participação do detector " +
                "no evento estrutural.");
    }

    private static double ResolveProxyStrength(
        string detector,
        ConfidenceBenchmarkEvent item)
    {
        var baseStrength =
            item.DurationMinutes switch
            {
                <= 1 => 0.68,
                <= 5 => 0.72,
                <= 30 => 0.78,
                <= 180 => 0.82,
                _ => 0.86
            };

        if (item.Detectors.Count == 1)
        {
            baseStrength -= 0.08;
        }

        if (detector.Equals(
                "Persistência",
                StringComparison.OrdinalIgnoreCase))
        {
            baseStrength += 0.04;
        }

        if (detector.StartsWith(
                "Multivariado/",
                StringComparison.OrdinalIgnoreCase))
        {
            baseStrength += 0.03;
        }

        return Math.Clamp(
            baseStrength,
            0.55,
            0.95);
    }

    private static double ResolveProxySeverity(
        ConfidenceBenchmarkEvent item)
    {
        if (item.Category.Contains(
                "sustentada",
                StringComparison.OrdinalIgnoreCase))
        {
            return 0.85;
        }

        if (item.Category.Contains(
                "zero",
                StringComparison.OrdinalIgnoreCase))
        {
            return 0.80;
        }

        if (item.DurationMinutes <= 1)
        {
            return 0.58;
        }

        return 0.72;
    }

    private static IReadOnlyList<ConfidenceBenchmarkLevelSummary>
        BuildLevelSummaries(
            IReadOnlyCollection<ConfidenceBenchmarkEvaluatedEvent> evaluated,
            int total)
    {
        return evaluated
            .GroupBy(
                item => item.Level,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                    new ConfidenceBenchmarkLevelSummary
                    {
                        Level =
                            group.Key,

                        EventCount =
                            group.LongCount(),

                        EventRate =
                            total > 0
                                ? group.LongCount() /
                                  (double)total
                                : 0
                    })
            .OrderBy(item => item.Level)
            .ToList();
    }

    private static IReadOnlyList<ConfidenceBenchmarkCategorySummary>
        BuildCategorySummaries(
            IReadOnlyCollection<ConfidenceBenchmarkEvaluatedEvent> evaluated)
    {
        return evaluated
            .GroupBy(
                item => item.Source.Category,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                    new ConfidenceBenchmarkCategorySummary
                    {
                        Category =
                            group.Key,

                        InputEvents =
                            group.LongCount(),

                        SuspiciousEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Suspicious.ToString()),

                        ProbableEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Probable.ToString()),

                        ConfirmedEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Confirmed.ToString()),

                        AverageScore =
                            group.Average(
                                item => item.Score),

                        AverageFamilyCount =
                            group.Average(
                                item => item.PositiveFamilyCount)
                    })
            .OrderByDescending(item => item.InputEvents)
            .ToList();
    }

    private static IReadOnlyList<
        ConfidenceBenchmarkIsolatedDetectorSummary>
        BuildIsolatedDetectorSummaries(
            IReadOnlyCollection<ConfidenceBenchmarkEvaluatedEvent> evaluated)
    {
        return evaluated
            .Where(
                item =>
                    item.Source.Detectors.Count == 1)
            .GroupBy(
                item => item.Source.Detectors[0],
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                    new ConfidenceBenchmarkIsolatedDetectorSummary
                    {
                        Detector =
                            group.Key,

                        InputEvents =
                            group.LongCount(),

                        SuspiciousEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Suspicious.ToString()),

                        ProbableEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Probable.ToString()),

                        ConfirmedEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Confirmed.ToString()),

                        PrincipalCategory =
                            group
                                .GroupBy(
                                    item => item.Source.Category,
                                    StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(
                                    categoryGroup =>
                                        categoryGroup.Count())
                                .First()
                                .Key
                    })
            .OrderByDescending(item => item.InputEvents)
            .ToList();
    }

    private static IReadOnlyList<ConfidenceBenchmarkFamilyCountSummary>
        BuildFamilyCountSummaries(
            IReadOnlyCollection<ConfidenceBenchmarkEvaluatedEvent> evaluated)
    {
        return evaluated
            .GroupBy(item => item.PositiveFamilyCount)
            .Select(
                group =>
                    new ConfidenceBenchmarkFamilyCountSummary
                    {
                        FamilyCount =
                            group.Key,

                        InputEvents =
                            group.LongCount(),

                        SuspiciousEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Suspicious.ToString()),

                        ProbableEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Probable.ToString()),

                        ConfirmedEvents =
                            group.LongCount(
                                item =>
                                    item.Level ==
                                    ConfidenceLevel.Confirmed.ToString()),

                        AverageScore =
                            group.Average(
                                item => item.Score)
                    })
            .OrderBy(item => item.FamilyCount)
            .ToList();
    }

    private static DetectorFamily ResolveFamily(
        string detectorName)
    {
        if (EqualsName(detectorName, "Z-Score") ||
            EqualsName(detectorName, "Mediana e MAD"))
        {
            return DetectorFamily.StatisticalDeviation;
        }

        if (EqualsName(detectorName, "EWMA"))
        {
            return DetectorFamily.TrendAndSmoothing;
        }

        if (EqualsName(detectorName, "Taxa de variação") ||
            EqualsName(detectorName, "Mudança de patamar"))
        {
            return DetectorFamily.Transition;
        }

        if (EqualsName(detectorName, "Persistência"))
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

    private static bool EqualsName(
        string left,
        string right)
    {
        return string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);
    }
}
