using IAApi.Benchmark.BehaviorProfiles.V2.Maturity;
using IAApi.Benchmark.BehaviorProfiles.V2.Structural;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

/// <summary>
/// Composicao observacional: nao recalcula score, estrutura, resolucao ou
/// maturidade. A maturidade existente e a unica evidencia temporal usada para
/// declarar uma conclusao observada como estabelecida.
/// </summary>
public sealed class BehaviorProfileDecisionReadinessEvaluator
{
    public BehaviorProfileReadinessEvaluation Evaluate(
        BehaviorProfileReadinessState previous,
        DeclaredTagType declaration,
        BehaviorProfileV2Result raw,
        StableBehaviorProfileObservation maturity,
        BehaviorProfileStructuralAssessment structural,
        StructuralAmbiguityResolution resolution,
        DateTime timestamp,
        int minuteIndex)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var changed = previous.PreviousResolutionStatus.HasValue &&
            (previous.PreviousResolutionStatus != resolution.ResolutionStatus ||
             previous.PreviousPreferredClass != resolution.PreferredClass);
        var resolvedToAmbiguous = previous.PreviousResolutionStatus == StructuralResolutionStatus.Resolved &&
                                  resolution.ResolutionStatus == StructuralResolutionStatus.Ambiguous;
        var ambiguousToResolved = previous.PreviousResolutionStatus == StructuralResolutionStatus.Ambiguous &&
                                  resolution.ResolutionStatus == StructuralResolutionStatus.Resolved;
        var resolvedClassChange = previous.PreviousResolutionStatus == StructuralResolutionStatus.Resolved &&
                                  resolution.ResolutionStatus == StructuralResolutionStatus.Resolved &&
                                  previous.PreviousPreferredClass != resolution.PreferredClass;
        var lastTimestamp = changed ? timestamp : previous.LastResolutionChangeTimestamp;
        var lastMinute = changed ? minuteIndex : previous.LastResolutionChangeMinuteIndex;
        var maturityStable = maturity.MaturityState == BehaviorProfileMaturityLifecycle.Stable;

        var (status, profile, reason) = Decide(
            declaration, raw, maturity, structural, resolution, maturityStable);
        var wasReady = previous.PreviousReadinessStatus is
            BehaviorProfileReadinessStatus.ReadyObserved or
            BehaviorProfileReadinessStatus.ReadyDeclaredAndObserved;
        var isReady = status is BehaviorProfileReadinessStatus.ReadyObserved or
            BehaviorProfileReadinessStatus.ReadyDeclaredAndObserved;
        var next = previous with
        {
            PreviousResolutionStatus = resolution.ResolutionStatus,
            PreviousPreferredClass = resolution.PreferredClass,
            PreviousReadinessStatus = status,
            ResolutionChangeCount = previous.ResolutionChangeCount + (changed ? 1 : 0),
            ResolvedToAmbiguousCount = previous.ResolvedToAmbiguousCount + (resolvedToAmbiguous ? 1 : 0),
            AmbiguousToResolvedCount = previous.AmbiguousToResolvedCount + (ambiguousToResolved ? 1 : 0),
            ResolvedClassChangeCount = previous.ResolvedClassChangeCount + (resolvedClassChange ? 1 : 0),
            LastResolutionChangeTimestamp = lastTimestamp,
            LastResolutionChangeMinuteIndex = lastMinute
        };
        return new BehaviorProfileReadinessEvaluation(next, new BehaviorProfileDecisionReadiness
        {
            Status = status,
            ProspectiveProfile = profile,
            Reason = reason,
            CurrentMaturityIsStable = maturityStable,
            ResolutionChangeCount = next.ResolutionChangeCount,
            ResolvedToAmbiguousCount = next.ResolvedToAmbiguousCount,
            AmbiguousToResolvedCount = next.AmbiguousToResolvedCount,
            ResolvedClassChangeCount = next.ResolvedClassChangeCount,
            LastResolutionChangeTimestamp = lastTimestamp,
            LastResolutionChangeMinuteIndex = lastMinute,
            MinutesSinceLastResolutionChange = lastTimestamp.HasValue
                ? Math.Max(0, (timestamp - lastTimestamp.Value).TotalMinutes) : 0,
            ResolutionChangedThisObservation = changed,
            StableAfterReady = wasReady && isReady && !changed
        });
    }

    private static (BehaviorProfileReadinessStatus, TagBehaviorType, string) Decide(
        DeclaredTagType declaration,
        BehaviorProfileV2Result raw,
        StableBehaviorProfileObservation maturity,
        BehaviorProfileStructuralAssessment structural,
        StructuralAmbiguityResolution resolution,
        bool maturityStable)
    {
        var relation = resolution.DeclaredTypeContext.Relation;
        if (declaration.IsRecognized && relation == DeclaredTypeObservationRelation.Contradictory)
            return (BehaviorProfileReadinessStatus.DeclarationContradiction,
                TagBehaviorType.Unknown,
                "Declaracao reconhecida contradita por evidencia estrutural forte de outra classe.");

        if (resolution.ResolutionStatus == StructuralResolutionStatus.Ambiguous)
            return (BehaviorProfileReadinessStatus.StructurallyAmbiguous,
                TagBehaviorType.Unknown,
                "Multiplas classes continuam plausiveis apos evidencia discriminante.");

        if (resolution.ResolutionStatus == StructuralResolutionStatus.SemanticallyUnresolved)
        {
            if (!declaration.IsRecognized)
                return (BehaviorProfileReadinessStatus.SemanticallyUnresolved,
                    TagBehaviorType.Unknown,
                    "Estrutura observada nao identifica a classe sem metadado semantico reconhecido.");
            if (relation == DeclaredTypeObservationRelation.ObservationallyInsufficient)
            {
                if (maturityStable && declaration.Kind == raw.Type &&
                    maturity.StableProfile == declaration.Kind &&
                    resolution.ObservedStructure is ObservedStructureKind.StepwiseLevels or
                        ObservedStructureKind.SparseStepwiseLevels)
                    return (BehaviorProfileReadinessStatus.ReadyDeclaredAndObserved,
                        declaration.Kind,
                        "Declaracao fornece a semantica nao observavel; estrutura stepwise e maturidade sao compativeis.");
                return (BehaviorProfileReadinessStatus.MetadataDependent,
                    declaration.Kind,
                    "Declaracao preservada, mas a janela e observacionalmente insuficiente para confirma-la.");
            }
            if (!maturityStable)
                return (BehaviorProfileReadinessStatus.InsufficientObservation,
                    declaration.Kind,
                    "Declaracao e estrutura compativeis, mas a conclusao temporal ainda nao amadureceu.");
            return (BehaviorProfileReadinessStatus.ReadyDeclaredAndObserved,
                declaration.Kind,
                "Declaracao reconhecida resolve a semantica stepwise e a observacao madura e compativel.");
        }

        if (resolution.ResolutionStatus == StructuralResolutionStatus.NoStructurallyValidClass)
        {
            if (declaration.IsRecognized &&
                relation == DeclaredTypeObservationRelation.ObservationallyInsufficient)
                return (BehaviorProfileReadinessStatus.MetadataDependent,
                    declaration.Kind,
                    "Declaracao preservada; a janela nao observa sua estrutura e nao contem evidencia positiva material contraria.");
            var rawAssessment = structural.Classes.Single(x => x.Class == raw.Winner);
            return rawAssessment.StructuralStatus == StructuralEvidenceStatus.Insufficient
                ? (BehaviorProfileReadinessStatus.InsufficientObservation, TagBehaviorType.Unknown,
                    "Nenhuma classe valida porque a cobertura estrutural ainda e insuficiente.")
                : (BehaviorProfileReadinessStatus.NotReady, TagBehaviorType.Unknown,
                    "Inferencia atual pode estar madura, mas o vencedor nao satisfaz requisitos estruturais.");
        }

        if (!maturityStable)
            return (BehaviorProfileReadinessStatus.InsufficientObservation,
                resolution.PreferredClass,
                "Resolucao estrutural existe, mas a evidencia temporal atual ainda nao esta madura.");

        if (maturity.StableProfile != resolution.PreferredClass)
            return (BehaviorProfileReadinessStatus.NotReady,
                resolution.PreferredClass,
                "Resolucao estrutural e perfil temporal estavel ainda nao convergiram.");

        if (declaration.IsRecognized && relation == DeclaredTypeObservationRelation.Compatible &&
            declaration.Kind == resolution.PreferredClass)
            return (BehaviorProfileReadinessStatus.ReadyDeclaredAndObserved,
                resolution.PreferredClass,
                "Declaracao reconhecida, estrutura, resolucao e maturidade sao compativeis.");

        return (BehaviorProfileReadinessStatus.ReadyObserved,
            resolution.PreferredClass,
            declaration.Status == DeclaredTagTypeStatus.InvalidOrUnrecognized
                ? "Observacao madura e resolvida; metadado invalido foi preservado e nao criou informacao."
                : "Observacao estruturalmente resolvida e confirmada pela maturidade existente.");
    }
}
