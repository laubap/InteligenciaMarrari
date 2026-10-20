namespace IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.ReadModels;

public interface IIncidentReadModelStore
{
    void Upsert(IncidentReadModel model);

    IncidentReadModel? Get(string incidentId);

    IncidentReadModel? GetByOriginatingRawAlertId(string rawAlertId);

    IReadOnlyList<IncidentReadModel> List(
        IncidentReadModelQuery? query = null);

    int Count { get; }
}