namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Separa explicitamente evidências estatísticas de confirmações temporais.
/// Não altera o resultado individual nem o peso configurado do detector.
/// </summary>
public static class EnsembleEvidenceParticipationPolicy
{
    public static bool ParticipatesInStatisticalAggregation(
        DetectorResult result,
        bool experimentalTemporalConfirmationEnabled,
        bool underlyingMadIsPositive)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Aplicavel)
            return false;

        if (!experimentalTemporalConfirmationEnabled)
            return true;

        return !(underlyingMadIsPositive &&
                 result.EvidenceRole == DetectorEvidenceRole.TemporalConfirmation &&
                 result.UnderlyingEvidenceKind == UnderlyingEvidenceKind.Mad &&
                 result.TemporalConfirmation?.Status ==
                    TemporalConfirmationStatus.Confirmed);
    }
}
