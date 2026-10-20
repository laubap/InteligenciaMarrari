namespace IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.ReadModels;

public sealed class InMemoryIncidentReadModelStore : IIncidentReadModelStore
{
    private readonly Dictionary<string, IncidentReadModel> _models =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _rawAlertIndex =
        new(StringComparer.Ordinal);

    public int Count => _models.Count;

    public void Upsert(IncidentReadModel model)
    {
        _models[model.IncidentId] = model;
        _rawAlertIndex[model.OriginatingRawAlertId] = model.IncidentId;
    }

    public IncidentReadModel? Get(string incidentId) =>
        _models.TryGetValue(
            incidentId,
            out var model)
            ? model
            : null;

    public IncidentReadModel? GetByOriginatingRawAlertId(
        string rawAlertId) =>
        _rawAlertIndex.TryGetValue(
            rawAlertId,
            out var id)
            ? Get(id)
            : null;

    public IReadOnlyList<IncidentReadModel> List(
        IncidentReadModelQuery? query = null)
    {
        query ??= new();

        IEnumerable<IncidentReadModel> values =
            _models.Values;

        if (query.IsOpen is not null)
        {
            values = values.Where(
                x => x.IsOpen == query.IsOpen);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            values = values.Where(
                x => x.Tag.Equals(
                    query.Tag,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Lifecycle))
        {
            values = values.Where(
                x => x.Lifecycle.Equals(
                    query.Lifecycle,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (query.From is not null)
        {
            values = values.Where(
                x => x.LastUpdatedAt >= query.From);
        }

        if (query.To is not null)
        {
            values = values.Where(
                x => x.OpenedAt <= query.To);
        }

        if (query.Escalated is not null)
        {
            values = values.Where(
                x => (x.EscalationCount > 0) ==
                     query.Escalated);
        }

        if (query.SuspendedDataQuality is not null)
        {
            values = values.Where(
                x => x.IsSuspended ==
                     query.SuspendedDataQuality);
        }

        if (query.MinimumDurationMinutes is not null)
        {
            values = values.Where(
                x => x.DurationMinutes >=
                     query.MinimumDurationMinutes);
        }

        if (query.HasCandidateMismatch is not null)
        {
            values = values.Where(
                x => (x.CandidateMismatchCount > 0) ==
                     query.HasCandidateMismatch);
        }

        if (query.LongRunningOnly)
        {
            values = values.Where(
                x => x.DurationMinutes >=
                     query.LongRunningMinutes);
        }

        values = query.Descending
            ? values
                .OrderByDescending(x => x.LastUpdatedAt)
                .ThenBy(x => x.Tag)
            : values
                .OrderBy(x => x.LastUpdatedAt)
                .ThenBy(x => x.Tag);

        return values.ToArray();
    }
}