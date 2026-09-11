namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

public enum BehaviorProfileReadinessStatus
{
    ReadyObserved,
    ReadyDeclaredAndObserved,
    MetadataDependent,
    StructurallyAmbiguous,
    SemanticallyUnresolved,
    InsufficientObservation,
    DeclarationContradiction,
    NotReady
}
