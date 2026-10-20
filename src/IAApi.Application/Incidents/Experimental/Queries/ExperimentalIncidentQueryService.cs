using IAApi.Application.Incidents.Experimental.ReadModels;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.ReadModels;
using IAApi.Application.Incidents.Experimental.Queries;
namespace IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Api;

public interface IExperimentalIncidentQueryService
{
    IncidentQueryResult<PagedIncidentResult> List(ExperimentalIncidentListRequest request);
    IncidentDetailsDto? Get(string incidentId);
}

public sealed class ExperimentalIncidentQueryService : IExperimentalIncidentQueryService
{
    private readonly IIncidentReadModelStore _store;
    private readonly ExperimentalIncidentQueryOptions _options;

public ExperimentalIncidentQueryService(
    IIncidentReadModelStore store,
    ExperimentalIncidentQueryOptions options)
{
    _store = store;
    _options = options;
}

    public IncidentQueryResult<PagedIncidentResult> List(
        ExperimentalIncidentListRequest request)
    {
        var pageSize = request.PageSize ?? _options.DefaultPageSize;
        if (request.Page < 1) return Invalid("page must be greater than or equal to 1.");
        if (pageSize < 1 || pageSize > _options.MaximumPageSize)
            return Invalid($"pageSize must be between 1 and {_options.MaximumPageSize}.");
        if (request.MinimumDurationMinutes < 0)
            return Invalid("minimumDurationMinutes cannot be negative.");
        if (request.From is not null && request.To is not null && request.From > request.To)
            return Invalid("from cannot be later than to.");

        bool? isOpen = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            isOpen = request.Status.Trim().ToLowerInvariant() switch
            {
                "open" or "active" => true,
                "resolved" or "closed" => false,
                "all" => null,
                _ => null
            };
            if (isOpen is null && !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
                return Invalid("status must be open, resolved, or all.");
        }
        if (request.OpenOnly == true)
        {
            if (isOpen == false) return Invalid("openOnly conflicts with status=resolved.");
            isOpen = true;
        }

        var models = _store.List(new IncidentReadModelQuery(
            IsOpen: isOpen,
            Tag: request.Tag,
            Lifecycle: request.Lifecycle,
            From: request.From,
            To: request.To,
            Escalated: request.Escalated,
            MinimumDurationMinutes: request.MinimumDurationMinutes,
            Descending: true));
        var total = models.Count;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        var items = models.Skip((request.Page - 1) * pageSize).Take(pageSize)
            .Select(IncidentReadModelDtoMapper.ToListItem).ToArray();
        return IncidentQueryResult<PagedIncidentResult>.Success(
            new(items, request.Page, pageSize, total, totalPages));
    }

    public IncidentDetailsDto? Get(string incidentId)
    {
        if (string.IsNullOrWhiteSpace(incidentId)) return null;
        var model = _store.Get(incidentId);
        return model is null ? null : IncidentReadModelDtoMapper.ToDetails(model);
    }

    private static IncidentQueryResult<PagedIncidentResult> Invalid(string error) =>
        IncidentQueryResult<PagedIncidentResult>.Invalid(error);
}
