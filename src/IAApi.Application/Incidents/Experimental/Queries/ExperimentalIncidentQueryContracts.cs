

namespace IAApi.Application.Incidents.Experimental.Queries;

public sealed record ExperimentalIncidentListRequest(
    string? Status = null,
    string? Lifecycle = null,
    string? Tag = null,
    DateTime? From = null,
    DateTime? To = null,
    bool? Escalated = null,
    bool? OpenOnly = null,
    double? MinimumDurationMinutes = null,
    int Page = 1,
    int? PageSize = null);

public sealed record PagedIncidentResult(
    IReadOnlyList<IncidentListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record IncidentQueryResult<T>(T? Value, string? ValidationError)
{
    public bool IsValid => ValidationError is null;
    public static IncidentQueryResult<T> Success(T value) => new(value, null);
    public static IncidentQueryResult<T> Invalid(string error) => new(default, error);
}
