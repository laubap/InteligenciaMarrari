using System.Globalization;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

using ServiceBehaviorProfileInferenceV2 =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.BehaviorProfileInferenceV2;

using ServiceBehaviorProfileStructuralEvidenceEvaluator =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural.BehaviorProfileStructuralEvidenceEvaluator;

using ServiceStructuralAmbiguityResolver =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural.StructuralAmbiguityResolver;

namespace IAApi.Benchmark.BehaviorProfiles;

/// <summary>
/// Constroi a visao semantica paralela do benchmark. O DetectorInput legado
/// nao e mutado e continua sendo a unica entrada do ensemble.
/// </summary>
public sealed class BenchmarkBehaviorProfileAdapter
{
    public const string InferenceAlgorithmVersion =
        "TagBehaviorProfileResolver-legacy-v1";

    private readonly DeclaredTagTypeParser _parser = new();

    private readonly TagBehaviorProfileResolver _resolver = new();

    private readonly ServiceBehaviorProfileInferenceV2 _inferenceV2 = new();

    private readonly ServiceBehaviorProfileStructuralEvidenceEvaluator _structural = new();

    private readonly ServiceStructuralAmbiguityResolver _ambiguityResolver = new();

    public BenchmarkBehaviorProfileObservation Adapt(
        int fastWindowIndex,
        int minuteIndex,
        DetectorInput legacyInput,
        DeclaredTagType? observedDeclaration = null)
    {
        ArgumentNullException.ThrowIfNull(legacyInput);

        var declaration = observedDeclaration ?? _parser.Parse(
            rawValue: null,
            source: "Benchmark");

        var inferenceInput = new DetectorInput
        {
            ClienteId = legacyInput.ClienteId,
            TagName = legacyInput.TagName,
            TipoTag = "",
            ValorAtual = legacyInput.ValorAtual,
            DataHoraAtual = legacyInput.DataHoraAtual,
            MediaHistorica = legacyInput.MediaHistorica,
            DesvioPadraoHistorico = legacyInput.DesvioPadraoHistorico,
            Historico = legacyInput.Historico
        };

        var legacyInference =
            _resolver.Resolve(
                inferenceInput);

        var inferenceV2 =
            _inferenceV2.Infer(
                legacyInput.Historico);

        var structural =
            _structural.Evaluate(
                inferenceV2,
                legacyInput.Historico,
                wouldMatureUnderCurrentPolicy: false);

        var ambiguity =
            _ambiguityResolver.Resolve(
                inferenceV2,
                structural,
                legacyInput.Historico,
                declaration);

        var history =
            legacyInput.Historico;

        var windowStart =
            history.Count == 0
                ? (DateTimeOffset?)null
                : new DateTimeOffset(
                    history[0].DataHora);

        var windowEnd =
            history.Count == 0
                ? (DateTimeOffset?)null
                : new DateTimeOffset(
                    history[^1].DataHora);

        var inference =
            new InferredBehaviorProfile
            {
                Type =
                    legacyInference.Type,

                Confidence =
                    legacyInference.Confidence,

                Reason =
                    legacyInference.Reason,

                Evidence =
                [
                    $"distinctValueCount={legacyInference.DistinctValueCount}",
                    $"distinctValueRatio={N(legacyInference.DistinctValueRatio)}",
                    $"integerLikeRatio={N(legacyInference.IntegerLikeRatio)}",
                    $"nonDecreasingRatio={N(legacyInference.NonDecreasingRatio)}",
                    $"changeRatio={N(legacyInference.ChangeRatio)}"
                ],

                SampleCount =
                    legacyInference.SampleCount,

                ObservedDuration =
                    windowStart.HasValue &&
                    windowEnd.HasValue
                        ? windowEnd.Value -
                          windowStart.Value
                        : TimeSpan.Zero,

                WindowStart =
                    windowStart,

                WindowEnd =
                    windowEnd,

                AlgorithmVersion =
                    InferenceAlgorithmVersion
            };

        var hasInference =
            inference.Type !=
            TagBehaviorType.Unknown;

        var resolved =
            new ResolvedBehaviorProfile
            {
                Type =
                    inference.Type,

                Confidence =
                    inference.Confidence,

                Origin =
                    hasInference
                        ? BehaviorProfileOrigin.Inferred
                        : BehaviorProfileOrigin.Unknown,

                Reason =
                    hasInference
                        ? "Sem declaracao; perfil final paralelo proveniente da inferencia atual."
                        : "Sem declaracao e inferencia atual inconclusiva.",

                UnknownCause =
                    hasInference
                        ? BehaviorProfileUnknownCause.None
                        : BehaviorProfileUnknownCause.InferenceInconclusive,

                HasConflict =
                    false,

                IsStable =
                    false,

                Declaration =
                    declaration,

                Inference =
                    inference
            };

        return new BenchmarkBehaviorProfileObservation
        {
            FastWindowIndex =
                fastWindowIndex,

            MinuteIndex =
                minuteIndex,

            Timestamp =
                legacyInput.DataHoraAtual,

            Tag =
                legacyInput.TagName,

            CurrentValue =
                legacyInput.ValorAtual,

            LegacyTipoTag =
                legacyInput.TipoTag,

            DeclaredTagType =
                declaration,

            InferredBehaviorProfile =
                inference,

            ResolvedBehaviorProfile =
                resolved,

            InferredBehaviorProfileV2 =
                inferenceV2,

            StructuralEvidence =
                structural,

            StructuralAmbiguityResolution =
                ambiguity
        };
    }

    private static string N(
        double value)
    {
        return value.ToString(
            "R",
            CultureInfo.InvariantCulture);
    }
}