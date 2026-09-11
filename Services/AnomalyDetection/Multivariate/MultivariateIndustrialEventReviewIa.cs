namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.4D1
///
/// Registro persistido da revisão industrial humana de um evento
/// multivariado.
///
/// Este registro é exclusivamente de governança/auditoria.
/// Por si só NÃO:
/// - habilita promoção;
/// - altera ApprovedGroups;
/// - altera readiness;
/// - altera RuntimeDecision.
/// </summary>
public sealed class MultivariateIndustrialEventReviewIa
{
    public long Id { get; set; }

    public string ClienteId { get; set; } =
        "";

    public string GroupName { get; set; } =
        "";

    public long MinuteIndex { get; set; }

    public string SourceWindow { get; set; } =
        "";

    public int ModelVersion { get; set; }

    public DateTime ModelTrainedAtUtc { get; set; }

    public double Threshold { get; set; }

    public string ReviewStatus { get; set; } =
        "";

    public string Reviewer { get; set; } =
        "";

    public string? Notes { get; set; }

    public string? EvidenceReference { get; set; }

    public DateTime ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public static class MultivariateIndustrialReviewStatuses
{
    public const string ConfirmedIndustrialEvent =
        "ConfirmedIndustrialEvent";

    public const string NormalOperation =
        "NormalOperation";

    public const string Inconclusive =
        "Inconclusive";

    public static IReadOnlyList<string> All { get; } =
    [
        ConfirmedIndustrialEvent,
        NormalOperation,
        Inconclusive
    ];

    public static bool TryNormalize(
        string? value,
        out string normalized)
    {
        normalized =
            "";

        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        var trimmed =
            value.Trim();

        var match =
            All.FirstOrDefault(item =>
                string.Equals(
                    item,
                    trimmed,
                    StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        normalized =
            match;

        return true;
    }

    public static bool IsDecisive(
        string? value)
    {
        return
            string.Equals(
                value,
                ConfirmedIndustrialEvent,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                value,
                NormalOperation,
                StringComparison.OrdinalIgnoreCase);
    }
}
