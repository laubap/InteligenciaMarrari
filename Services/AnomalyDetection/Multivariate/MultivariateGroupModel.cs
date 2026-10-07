namespace IAApi.Services.AnomalyDetection.Multivariate;

public sealed class MultivariateGroupModel
{
    public int Version { get; set; }

    public string ClienteId { get; set; } = "";

    public string GroupName { get; set; } = "";

    public DateTime TrainedAtUtc { get; set; }

    public string Algorithm { get; set; } = "";

    public long SampleCount { get; set; }

    public int MinimumAvailableTags { get; set; }

    public int MaxLagMinutes { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<double> Means { get; set; } = [];

    public List<double> StandardDeviations { get; set; } = [];

    public List<List<double>> CovarianceMatrix { get; set; } = [];

    public List<List<double>> CorrelationMatrix { get; set; } = [];
}