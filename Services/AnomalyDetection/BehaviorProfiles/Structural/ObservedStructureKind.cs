namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural;

public enum ObservedStructureKind
{
    Unknown,
    Constant,
    NumericTrajectory,
    AccumulativeProgression,
    StepwiseLevels,
    SparseStepwiseLevels
}