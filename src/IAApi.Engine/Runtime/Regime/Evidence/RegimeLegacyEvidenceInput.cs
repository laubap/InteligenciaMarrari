namespace IAApi.Services.AnomalyDetection.Runtime.Regime.Evidence;

public sealed record RegimeLegacyEvidenceInput(
    bool RocApplicable,
    bool RocVotedAnomaly,
    bool LevelApplicable,
    bool LevelVotedAnomaly,
    bool LevelHasCriticalGap,
    bool PersistenceApplicable,
    bool PersistenceVotedAnomaly,
    double PersistenceDurationMinutes,
    bool PersistenceUsesMad);