namespace IAApi.Services.AnomalyDetection.Orchestration;

/// <summary>
/// Owned by one prediction only. Copies values before baseline transformations;
/// never exposes the stored readings, and does not filter or normalize them.
/// </summary>
public sealed class PredictionRawHistorySnapshot
{
    private readonly IHistoricoService _source;
    private readonly string _client, _tag, _type;
    private readonly Reading?[] _readings;
    private sealed record Reading(DateTime Timestamp, float Value);

    internal PredictionRawHistorySnapshot(IHistoricoService source, string client, string tag, string type,
        IEnumerable<LeituraBruta> readings, DateTimeOffset startedUtc, DateTimeOffset completedUtc,
        DateTime predictionReference)
    {
        _source = source; _client = client; _tag = tag; _type = type;
        _readings = readings.Select(r => r is null ? null : new Reading(r.DataHora, r.Valor)).ToArray();
        AcquisitionStartedUtc = startedUtc;
        AcquisitionCompletedUtc = completedUtc;
        PredictionReference = predictionReference;
    }

    public DateTimeOffset AcquisitionStartedUtc { get; }
    public DateTimeOffset AcquisitionCompletedUtc { get; }
    public DateTime PredictionReference { get; }
    public int Count => _readings.Length;

    internal bool Matches(IHistoricoService source, string client, string tag, string type) =>
        ReferenceEquals(_source, source)
        && string.Equals(_client, client, StringComparison.Ordinal)
        && string.Equals(_tag, tag, StringComparison.Ordinal)
        && string.Equals(_type, type, StringComparison.Ordinal);

    internal List<LeituraBruta> CopyReadings() => _readings.Select(r => r is null
        ? null! : new LeituraBruta { DataHora = r.Timestamp, Valor = r.Value }).ToList();
}
