namespace IAApi.Application.Incidents.Experimental.Queries;

public sealed record ExperimentalIncidentQueryOptions
{
    public int DefaultPageSize { get; init; } = 20;

    public int MaximumPageSize { get; init; } = 100;
}