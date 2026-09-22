using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural;

/// <summary>
/// Resolucao Shadow pura. Nao promove perfil: explica se as estruturas
/// observadas distinguem classes ou se a resposta correta permanece ambigua.
/// </summary>
public sealed class StructuralAmbiguityResolver
{
    public StructuralAmbiguityResolution Resolve(
        BehaviorProfileV2Result raw,
        BehaviorProfileStructuralAssessment structural,
        IReadOnlyList<HistoricoDetectorPoint> history,
        DeclaredTagType declaration)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(structural);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(declaration);

        var points = history
            .Where(p => double.IsFinite(p.Valor))
            .OrderBy(p => p.DataHora)
            .ToArray();

        var valid = structural.Classes
            .Where(x => x.StructuralStatus == StructuralEvidenceStatus.Valid)
            .Select(x => x.Class)
            .OrderBy(x => x)
            .ToArray();

        var observed =
            Observe(raw.Features);

        var declared =
            DeclaredContext(
                declaration,
                structural,
                observed,
                raw.Features,
                points);

        var evidence =
            new List<string>();

        var ambiguities =
            new List<string>();

        if (
            observed is
                ObservedStructureKind.StepwiseLevels or
                ObservedStructureKind.SparseStepwiseLevels
            &&
            structural.Classes
                .Single(x => x.Class == TagBehaviorType.Setpoint)
                .StructuralStatus ==
            StructuralEvidenceStatus.Insufficient)
        {
            evidence.Add(
                $"ObservedStructure={observed}");

            ambiguities.Add(
                "A serie isolada nao distingue estado discreto de valor-alvo configurado.");

            var declaredPreference =
                declaration.IsRecognized
                    ? declaration.Kind
                    : TagBehaviorType.Unknown;

            return Result(
                valid,
                declaredPreference,
                StructuralResolutionStatus.SemanticallyUnresolved,
                evidence,
                ambiguities,
                declared,
                observed,
                declaration.IsRecognized
                    ? $"Declaracao {declaration.Kind} preservada como contexto; observacao stepwise e semanticamente insuficiente."
                    : "Estrutura stepwise observada; Discrete versus Setpoint permanece semanticamente nao resolvido.");
        }

        var counterAndContinuous =
            valid.Contains(TagBehaviorType.Counter) &&
            valid.Contains(TagBehaviorType.Continuous);

        if (counterAndContinuous)
        {
            var discrimination =
                CounterContinuous(
                    raw.Features,
                    points);

            evidence.AddRange(
                discrimination.Evidence);

            ambiguities.AddRange(
                discrimination.Ambiguities);

            if (
                discrimination.Preferred !=
                TagBehaviorType.Unknown)
            {
                return Result(
                    valid,
                    discrimination.Preferred,
                    StructuralResolutionStatus.Resolved,
                    evidence,
                    ambiguities,
                    declared,
                    observed,
                    $"Evidencia discriminante favorece {discrimination.Preferred} entre estruturas validas.");
            }

            return Result(
                valid,
                TagBehaviorType.Unknown,
                StructuralResolutionStatus.Ambiguous,
                evidence,
                ambiguities,
                declared,
                observed,
                "Counter e Continuous permanecem estruturalmente plausiveis; nenhuma escolha foi forcada.");
        }

        if (valid.Length == 0)
        {
            var preferred =
                declaration.IsRecognized &&
                declared.Relation ==
                DeclaredTypeObservationRelation.ObservationallyInsufficient
                    ? declaration.Kind
                    : TagBehaviorType.Unknown;

            return Result(
                valid,
                preferred,
                StructuralResolutionStatus.NoStructurallyValidClass,
                evidence,
                ambiguities,
                declared,
                observed,
                preferred == TagBehaviorType.Unknown
                    ? "Nenhuma classe possui todos os requisitos estruturais."
                    : $"Nenhuma classe observada valida; declaracao {preferred} preservada sem confirmacao.");
        }

        if (valid.Length == 1)
        {
            evidence.Add(
                $"Unica classe estruturalmente valida: {valid[0]}");

            return Result(
                valid,
                valid[0],
                StructuralResolutionStatus.Resolved,
                evidence,
                ambiguities,
                declared,
                observed,
                "A observacao possui exatamente uma classe estruturalmente valida.");
        }

        ambiguities.Add(
            "Multiplas classes estruturalmente validas sem regra discriminante suficiente.");

        return Result(
            valid,
            TagBehaviorType.Unknown,
            StructuralResolutionStatus.Ambiguous,
            evidence,
            ambiguities,
            declared,
            observed,
            "Multiplas classes validas; resolucao conservadora.");
    }

    private static Discrimination CounterContinuous(
        BehaviorProfileFeatures f,
        IReadOnlyList<HistoricoDetectorPoint> points)
    {
        var evidence =
            new List<string>();

        var ambiguity =
            new List<string>();

        var deltas = points
            .Zip(
                points.Skip(1),
                (a, b) =>
                    b.Valor - a.Valor)
            .ToArray();

        var positive =
            deltas
                .Where(x => x > 0)
                .ToArray();

        var mean =
            positive.Length == 0
                ? 0
                : positive.Average();

        var incrementCv =
            mean <= 0
                ? double.PositiveInfinity
                : Math.Sqrt(
                      positive
                          .Select(
                              x =>
                                  Math.Pow(
                                      x - mean,
                                      2))
                          .Average()) /
                  mean;

        var net =
            points.Count < 2
                ? 0
                : points[^1].Valor -
                  points[0].Valor;

        var normalizedNet =
            f.ValueRange <= 0
                ? 0
                : Math.Abs(net) /
                  f.ValueRange;

        var directionalEvents =
            f.PositiveDeltaRatio +
            f.NegativeDeltaRatio;

        var positiveDirection =
            directionalEvents <= 0
                ? 0
                : f.PositiveDeltaRatio /
                  directionalEvents;

        var negativeDirection =
            directionalEvents <= 0
                ? 0
                : f.NegativeDeltaRatio /
                  directionalEvents;

        var recurrentReturn =
            normalizedNet <= .50 &&
            positiveDirection >= .15 &&
            negativeDirection >= .15;

        var counterDirection =
            positiveDirection >= .95 &&
            negativeDirection <= .05;

        var stableIncrements =
            incrementCv <= .20;

        var resetCounter =
            f.ResetCount > 0 &&
            f.ResetRatio <= .05 &&
            counterDirection;

        var recurrentAnalogCycle =
            f.DownwardStepCount >= 3 &&
            f.DownwardStepIntervalRegularityRatio >= .80 &&
            f.IntegerLikeRatio < .20 &&
            f.DistinctValueCount >= 12 &&
            (
                f.AdjacentLevelTransitionRatio >= .65 ||
                f.SmoothTransitionRatio >= .95
            );

        evidence.Add(
            $"normalizedNetProgress={normalizedNet:F3}");

        evidence.Add(
            $"positiveDirectionRatio={f.PositiveDeltaRatio:F3}");

        evidence.Add(
            $"negativeDirectionRatio={f.NegativeDeltaRatio:F3}");

        evidence.Add(
            $"positiveAmongChanges={positiveDirection:F3}");

        evidence.Add(
            $"negativeAmongChanges={negativeDirection:F3}");

        evidence.Add(
            $"positiveIncrementCv={(double.IsFinite(incrementCv) ? incrementCv.ToString("F3") : "n/a")}");

        evidence.Add(
            $"reversalRatio={f.ReversalRatio:F3}");

        if (recurrentAnalogCycle)
        {
            evidence.Add(
                "quedas regulares recorrentes com trajetoria analogica favorecem Continuous");

            evidence.Add(
                "retorno ciclico enfraquece acumulacao Counter");

            return new(
                TagBehaviorType.Continuous,
                evidence,
                ambiguity);
        }

        if (recurrentReturn)
        {
            evidence.Add(
                "retorno recorrente/reversoes com progresso liquido pequeno favorecem Continuous");

            return new(
                TagBehaviorType.Continuous,
                evidence,
                ambiguity);
        }

        if (
            (counterDirection &&
             stableIncrements)
            ||
            resetCounter)
        {
            evidence.Add(
                resetCounter
                    ? "incrementos dominantes com reset raro e coerente favorecem Counter"
                    : "direcao acumulativa dominante com incrementos consistentes favorece Counter");

            return new(
                TagBehaviorType.Counter,
                evidence,
                ambiguity);
        }

        ambiguity.Add(
            "Monotonicidade local nao demonstra acumulacao sem consistencia dos incrementos.");

        return new(
            TagBehaviorType.Unknown,
            evidence,
            ambiguity);
    }

    private static ObservedStructureKind Observe(
        BehaviorProfileFeatures f)
    {
        if (f.DistinctValueCount <= 1)
        {
            return ObservedStructureKind.Constant;
        }

        if (
            f.DistinctValueCount <= 12 &&
            f.ZeroDeltaRatio >= .75)
        {
            return f.ChangeRatio < .01
                ? ObservedStructureKind.SparseStepwiseLevels
                : ObservedStructureKind.StepwiseLevels;
        }

        var recurrentAnalogCycle =
            f.DownwardStepCount >= 3 &&
            f.DownwardStepIntervalRegularityRatio >= .80 &&
            f.IntegerLikeRatio < .20;

        if (
            !recurrentAnalogCycle &&
            f.NonDecreasingRatio >= .96 &&
            f.PositiveDeltaRatio >= .15)
        {
            return ObservedStructureKind.AccumulativeProgression;
        }

        if (
            f.DistinctValueCount >= 5 &&
            (
                f.AdjacentLevelTransitionRatio >= .65 ||
                f.SmoothTransitionRatio >= .80
            ))
        {
            return ObservedStructureKind.NumericTrajectory;
        }

        return ObservedStructureKind.Unknown;
    }

    private static DeclaredTypeStructuralContext DeclaredContext(
        DeclaredTagType d,
        BehaviorProfileStructuralAssessment structural,
        ObservedStructureKind observed,
        BehaviorProfileFeatures features,
        IReadOnlyList<HistoricoDetectorPoint> points)
    {
        if (!d.IsRecognized)
        {
            return new DeclaredTypeStructuralContext
            {
                Type = d.Kind,
                Status = d.Status,
                RawValue = d.RawValue,
                Source = d.Source,

                Relation =
                    DeclaredTypeObservationRelation.NotDeclared,

                Reason =
                    "Nenhum tipo declarado reconhecido disponivel."
            };
        }

        var assessment =
            structural.Classes.Single(
                x => x.Class == d.Kind);

        var contradictionReason =
            "Evidencia observada contradiz requisitos estruturais da classe declarada.";

        var contradicted =
            assessment.StructuralStatus ==
            StructuralEvidenceStatus.Invalid
            &&
            HasPositiveContradictoryEvidence(
                d.Kind,
                structural,
                observed,
                features,
                points,
                out contradictionReason);

        var relation =
            assessment.StructuralStatus switch
            {
                StructuralEvidenceStatus.Valid =>
                    DeclaredTypeObservationRelation.Compatible,

                StructuralEvidenceStatus.Insufficient =>
                    DeclaredTypeObservationRelation.ObservationallyInsufficient,

                _ when contradicted =>
                    DeclaredTypeObservationRelation.Contradictory,

                _ =>
                    DeclaredTypeObservationRelation.ObservationallyInsufficient
            };

        return new DeclaredTypeStructuralContext
        {
            Type = d.Kind,
            Status = d.Status,
            RawValue = d.RawValue,
            Source = d.Source,
            Relation = relation,

            Reason =
                relation switch
                {
                    DeclaredTypeObservationRelation.Compatible =>
                        "Observacao compativel com a declaracao.",

                    DeclaredTypeObservationRelation.ObservationallyInsufficient =>
                        "Estrutura declarada nao foi observada, sem evidencia positiva material de uma classe incompatível.",

                    _ =>
                        contradictionReason
                }
        };
    }

    private static bool HasPositiveContradictoryEvidence(
        TagBehaviorType declared,
        BehaviorProfileStructuralAssessment structural,
        ObservedStructureKind observed,
        BehaviorProfileFeatures features,
        IReadOnlyList<HistoricoDetectorPoint> points,
        out string reason)
    {
        reason = "";

        var continuousValid =
            IsValid(
                structural,
                TagBehaviorType.Continuous);

        var discreteValid =
            IsValid(
                structural,
                TagBehaviorType.Discrete);

        var counterValid =
            IsValid(
                structural,
                TagBehaviorType.Counter);

        if (
            declared == TagBehaviorType.Counter &&
            continuousValid &&
            observed ==
            ObservedStructureKind.NumericTrajectory &&
            HasMaterialBidirectionalReturn(
                features,
                points))
        {
            reason =
                "Evidencia positiva material de trajetoria bidirecional/recorrente incompatível com acumulacao Counter.";

            return true;
        }

        if (
            declared == TagBehaviorType.Setpoint &&
            continuousValid &&
            observed ==
            ObservedStructureKind.NumericTrajectory)
        {
            reason =
                "Evidencia positiva material de trajetoria numerica continua e distribuida, sem estrutura de patamares, incompatível com Setpoint.";

            return true;
        }

        if (
            declared == TagBehaviorType.Continuous &&
            (
                counterValid &&
                observed ==
                ObservedStructureKind.AccumulativeProgression
                ||
                discreteValid &&
                observed is
                    ObservedStructureKind.StepwiseLevels or
                    ObservedStructureKind.SparseStepwiseLevels
            ))
        {
            reason =
                "Evidencia positiva material de estrutura alternativa incompatível com Continuous.";

            return true;
        }

        if (
            declared == TagBehaviorType.Discrete &&
            continuousValid &&
            observed ==
            ObservedStructureKind.NumericTrajectory)
        {
            reason =
                "Evidencia positiva material de trajetoria numerica continua incompatível com Discrete.";

            return true;
        }

        return false;
    }

    private static bool HasMaterialBidirectionalReturn(
        BehaviorProfileFeatures features,
        IReadOnlyList<HistoricoDetectorPoint> points)
    {
        if (
            points.Count < 2 ||
            features.ValueRange <= 0)
        {
            return false;
        }

        var directionalEvents =
            features.PositiveDeltaRatio +
            features.NegativeDeltaRatio;

        if (directionalEvents <= 0)
        {
            return false;
        }

        var positiveAmongChanges =
            features.PositiveDeltaRatio /
            directionalEvents;

        var negativeAmongChanges =
            features.NegativeDeltaRatio /
            directionalEvents;

        var normalizedNet =
            Math.Abs(
                points[^1].Valor -
                points[0].Valor) /
            features.ValueRange;

        var recurrentReturn =
            normalizedNet <= .50 &&
            positiveAmongChanges >= .15 &&
            negativeAmongChanges >= .15;

        var recurrentAnalogCycle =
            features.DownwardStepCount >= 3 &&
            features.DownwardStepIntervalRegularityRatio >= .80 &&
            features.IntegerLikeRatio < .20 &&
            features.DistinctValueCount >= 12 &&
            (
                features.AdjacentLevelTransitionRatio >= .65 ||
                features.SmoothTransitionRatio >= .95
            );

        return recurrentReturn ||
               recurrentAnalogCycle;
    }

    private static bool IsValid(
        BehaviorProfileStructuralAssessment structural,
        TagBehaviorType type)
    {
        return structural.Classes
                   .Single(
                       x => x.Class == type)
                   .StructuralStatus ==
               StructuralEvidenceStatus.Valid;
    }

    private static StructuralAmbiguityResolution Result(
        IReadOnlyList<TagBehaviorType> valid,
        TagBehaviorType preferred,
        StructuralResolutionStatus status,
        IReadOnlyList<string> evidence,
        IReadOnlyList<string> ambiguity,
        DeclaredTypeStructuralContext declared,
        ObservedStructureKind observed,
        string reason)
    {
        return new StructuralAmbiguityResolution
        {
            ValidClasses = valid,
            PreferredClass = preferred,
            ResolutionStatus = status,
            DiscriminatingEvidence = evidence,
            RemainingAmbiguities = ambiguity,
            DeclaredTypeContext = declared,
            ObservedStructure = observed,
            Reason = reason
        };
    }

    private sealed record Discrimination(
        TagBehaviorType Preferred,
        IReadOnlyList<string> Evidence,
        IReadOnlyList<string> Ambiguities);
}