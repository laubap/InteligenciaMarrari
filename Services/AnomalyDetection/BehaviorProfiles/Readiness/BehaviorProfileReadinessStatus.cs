namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness;

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