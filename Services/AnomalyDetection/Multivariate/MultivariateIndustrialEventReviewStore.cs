using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.4D1
///
/// Persistência isolada das revisões industriais multivariadas.
///
/// O upsert usa como identidade lógica:
/// Cliente + Grupo + Evento + Janela + Modelo treinado + Threshold.
///
/// Assim, uma revisão feita para um modelo/threshold antigo não é
/// silenciosamente reutilizada em uma qualificação futura.
/// </summary>
public sealed class MultivariateIndustrialEventReviewStore
{
    private readonly AppDbContext
        _context;

    public MultivariateIndustrialEventReviewStore(
        AppDbContext context)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));
    }

    public async Task<MultivariateIndustrialEventReviewIa>
        UpsertAsync(
            MultivariateIndustrialEventReviewUpsert input,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            input);

        var clienteId =
            NormalizeRequired(
                input.ClienteId,
                nameof(input.ClienteId));

        var groupName =
            NormalizeRequired(
                input.GroupName,
                nameof(input.GroupName));

        var sourceWindow =
            NormalizeSourceWindow(
                input.SourceWindow);

        var reviewer =
            NormalizeRequired(
                input.Reviewer,
                nameof(input.Reviewer));

        if (!MultivariateIndustrialReviewStatuses
                .TryNormalize(
                    input.ReviewStatus,
                    out var reviewStatus))
        {
            throw new ArgumentException(
                "ReviewStatus inválido. Use ConfirmedIndustrialEvent, " +
                "NormalOperation ou Inconclusive.",
                nameof(input.ReviewStatus));
        }

        if (input.MinuteIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.MinuteIndex));
        }

        if (input.ModelVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.ModelVersion));
        }

        if (input.ModelTrainedAtUtc == default)
        {
            throw new ArgumentException(
                "ModelTrainedAtUtc é obrigatório.",
                nameof(input.ModelTrainedAtUtc));
        }

        if (!double.IsFinite(
                input.Threshold) ||
            input.Threshold <= 0 ||
            input.Threshold >= 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.Threshold),
                "Threshold deve estar no intervalo (0, 100).");
        }

        var modelTrainedAtUtc =
            NormalizeUtc(
                input.ModelTrainedAtUtc);

        var reviewedAtUtc =
            input.ReviewedAtUtc.HasValue
                ? NormalizeUtc(
                    input.ReviewedAtUtc.Value)
                : DateTime.UtcNow;

        var existing =
            await _context
                .MultivariateIndustrialEventReviews
                .SingleOrDefaultAsync(
                    item =>
                        item.ClienteId ==
                            clienteId &&
                        item.GroupName ==
                            groupName &&
                        item.MinuteIndex ==
                            input.MinuteIndex &&
                        item.SourceWindow ==
                            sourceWindow &&
                        item.ModelVersion ==
                            input.ModelVersion &&
                        item.ModelTrainedAtUtc ==
                            modelTrainedAtUtc &&
                        item.Threshold ==
                            input.Threshold,
                    cancellationToken);

        if (existing is null)
        {
            var created =
                new MultivariateIndustrialEventReviewIa
                {
                    ClienteId =
                        clienteId,

                    GroupName =
                        groupName,

                    MinuteIndex =
                        input.MinuteIndex,

                    SourceWindow =
                        sourceWindow,

                    ModelVersion =
                        input.ModelVersion,

                    ModelTrainedAtUtc =
                        modelTrainedAtUtc,

                    Threshold =
                        input.Threshold,

                    ReviewStatus =
                        reviewStatus,

                    Reviewer =
                        reviewer,

                    Notes =
                        NormalizeOptional(
                            input.Notes),

                    EvidenceReference =
                        NormalizeOptional(
                            input.EvidenceReference),

                    ReviewedAtUtc =
                        reviewedAtUtc,

                    CreatedAtUtc =
                        DateTime.UtcNow,

                    UpdatedAtUtc =
                        DateTime.UtcNow
                };

            _context
                .MultivariateIndustrialEventReviews
                .Add(
                    created);

            await _context
                .SaveChangesAsync(
                    cancellationToken);

            return created;
        }

        existing.ReviewStatus =
            reviewStatus;

        existing.Reviewer =
            reviewer;

        existing.Notes =
            NormalizeOptional(
                input.Notes);

        existing.EvidenceReference =
            NormalizeOptional(
                input.EvidenceReference);

        existing.ReviewedAtUtc =
            reviewedAtUtc;

        existing.UpdatedAtUtc =
            DateTime.UtcNow;

        await _context
            .SaveChangesAsync(
                cancellationToken);

        return existing;
    }

    public async Task<IReadOnlyList<
        MultivariateIndustrialEventReviewIa>>
        ListAsync(
            string clienteId,
            string groupName,
            int modelVersion,
            DateTime modelTrainedAtUtc,
            double threshold,
            CancellationToken cancellationToken = default)
    {
        var clienteNormalizado =
            NormalizeRequired(
                clienteId,
                nameof(clienteId));

        var grupoNormalizado =
            NormalizeRequired(
                groupName,
                nameof(groupName));

        if (modelVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelVersion));
        }

        if (modelTrainedAtUtc == default)
        {
            throw new ArgumentException(
                "ModelTrainedAtUtc é obrigatório.",
                nameof(modelTrainedAtUtc));
        }

        if (!double.IsFinite(
                threshold) ||
            threshold <= 0 ||
            threshold >= 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold));
        }

        var modelUtc =
            NormalizeUtc(
                modelTrainedAtUtc);

        return await _context
            .MultivariateIndustrialEventReviews
            .AsNoTracking()
            .Where(item =>
                item.ClienteId ==
                    clienteNormalizado &&
                item.GroupName ==
                    grupoNormalizado &&
                item.ModelVersion ==
                    modelVersion &&
                item.ModelTrainedAtUtc ==
                    modelUtc &&
                item.Threshold ==
                    threshold)
            .OrderBy(item =>
                item.MinuteIndex)
            .ThenBy(item =>
                item.SourceWindow)
            .ToListAsync(
                cancellationToken);
    }

    private static string NormalizeRequired(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Valor obrigatório.",
                parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeSourceWindow(
        string? sourceWindow)
    {
        var value =
            NormalizeRequired(
                sourceWindow,
                nameof(sourceWindow));

        if (string.Equals(
                value,
                MultivariateIndustrialEvidenceWindows.Reference,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                MultivariateIndustrialEvidenceWindows.Reference;
        }

        if (string.Equals(
                value,
                MultivariateIndustrialEvidenceWindows.Holdout,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                MultivariateIndustrialEvidenceWindows.Holdout;
        }

        throw new ArgumentException(
            "SourceWindow inválido. Use Reference ou Holdout.",
            nameof(sourceWindow));
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? null
                : value.Trim();
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }
}

public sealed class MultivariateIndustrialEventReviewUpsert
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public long MinuteIndex { get; init; }

    public string SourceWindow { get; init; } =
        "";

    public int ModelVersion { get; init; }

    public DateTime ModelTrainedAtUtc { get; init; }

    public double Threshold { get; init; } =
        75.0;

    public string ReviewStatus { get; init; } =
        "";

    public string Reviewer { get; init; } =
        "";

    public string? Notes { get; init; }

    public string? EvidenceReference { get; init; }

    /// <summary>
    /// Permite preservar o instante fornecido por uma integração/teste.
    /// Quando omitido, o servidor usa DateTime.UtcNow.
    /// </summary>
    public DateTime? ReviewedAtUtc { get; init; }
}