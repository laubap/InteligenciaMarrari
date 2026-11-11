using Microsoft.Extensions.Hosting;

namespace IAApi.Services.AnomalyDetection.Orchestration;

/// <summary>Experimental, fixed at startup; never enabled outside Development.</summary>
public sealed class PredictionHistoryReuseOptions
{
    public const string EnvironmentVariable = "IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY";

    public PredictionHistoryReuseOptions(string environmentName, string? flag)
    {
        Enabled = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && flag == "1";
    }

    public bool Enabled { get; }
}
