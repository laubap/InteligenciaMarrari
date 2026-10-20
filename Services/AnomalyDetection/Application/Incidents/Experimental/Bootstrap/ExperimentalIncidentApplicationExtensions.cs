using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Api;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.ReadModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IAApi.Application.Incidents.Experimental.ReadModels;

namespace IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Bootstrap;

public static class ExperimentalIncidentApplicationExtensions
{
    public static async Task LoadExperimentalIncidentSnapshotAsync(
        this WebApplication app)
    {
        var experimentalIncidentApiOptions =
            app.Services
                .GetRequiredService<
                    IOptions<ExperimentalIncidentApiOptions>>()
                .Value;

        if (!experimentalIncidentApiOptions.Enabled ||
            string.IsNullOrWhiteSpace(
                experimentalIncidentApiOptions.SnapshotPath))
        {
            return;
        }

        try
        {
            var snapshotPath =
                Path.IsPathRooted(
                    experimentalIncidentApiOptions.SnapshotPath)
                    ? experimentalIncidentApiOptions.SnapshotPath
                    : Path.Combine(
                        Directory.GetCurrentDirectory(),
                        experimentalIncidentApiOptions.SnapshotPath);

            var loader =
                app.Services.GetRequiredService<
                    IncidentReadModelSnapshotLoader>();

            var store =
                app.Services.GetRequiredService<
                    IIncidentReadModelStore>();

            var loaded =
                await loader.LoadAsync(
                    snapshotPath,
                    store);

            app.Logger.LogInformation(
                "Experimental Incident API loaded {IncidentCount} read models from {SnapshotPath}.",
                loaded,
                snapshotPath);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(
                ex,
                "Experimental Incident API snapshot load failed; API remains isolated and empty.");
        }
    }
}