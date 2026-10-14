using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.Runtime.Regime.Evidence;

public static class RegimeLegacyEvidenceMapper
{
    public static RegimeLegacyEvidenceInput Map(
        DetectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var roc =
            Find(
                result,
                "Taxa de varia");

        var level =
            Find(
                result,
                "patamar");

        var persistence =
            Find(
                result,
                "Persist");

        return new RegimeLegacyEvidenceInput(
            RocApplicable:
                roc?.Aplicavel == true,

            RocVotedAnomaly:
                roc?.VotouAnomalia == true,

            LevelApplicable:
                level?.Aplicavel == true,

            LevelVotedAnomaly:
                level?.VotouAnomalia == true,

            LevelHasCriticalGap:
                Metric(
                    level,
                    "possuiGapCriticoNasJanelas") >= .5,

            PersistenceApplicable:
                persistence?.Aplicavel == true,

            PersistenceVotedAnomaly:
                persistence?.VotouAnomalia == true,

            PersistenceDurationMinutes:
                Metric(
                    persistence,
                    "duracaoMinutos"),

            PersistenceUsesMad:
                Metric(
                    persistence,
                    "usaMad") >= .5);
    }

    private static DetectorDebugItem?
        Find(
            DetectorResult result,
            string fragment) =>
        result.DetalhesDetectores
            .FirstOrDefault(
                detector =>
                    detector.Nome.Contains(
                        fragment,
                        StringComparison.OrdinalIgnoreCase));

    private static double Metric(
        DetectorDebugItem? item,
        string name) =>
        item is not null &&
        item.Metricas.TryGetValue(
            name,
            out var value)
            ? value
            : 0;
}