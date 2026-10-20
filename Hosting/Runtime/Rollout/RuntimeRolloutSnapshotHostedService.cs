using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.8 — persiste snapshots periódicos do rollout.
/// Falhas de persistência são isoladas e não derrubam a API.
/// </summary>
public sealed class RuntimeRolloutSnapshotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly IOptions<RuntimeRolloutSnapshotPersistenceOptions>
        _options;

    private readonly ILogger<RuntimeRolloutSnapshotHostedService>
        _logger;

    public RuntimeRolloutSnapshotHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RuntimeRolloutSnapshotPersistenceOptions> options,
        ILogger<RuntimeRolloutSnapshotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions =
                _options.Value;

            if (!currentOptions.Enabled)
            {
                await DelayAsync(
                    TimeSpan.FromMinutes(1),
                    stoppingToken);

                continue;
            }

            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var snapshotService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            RuntimeRolloutSnapshotService>();

                await snapshotService.SaveCurrentAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao persistir snapshot de rollout. O monitoramento continuará.");
            }

            var intervalMinutes =
                Math.Max(
                    1,
                    currentOptions.IntervalMinutes);

            await DelayAsync(
                TimeSpan.FromMinutes(
                    intervalMinutes),
                stoppingToken);
        }
    }

    private static async Task DelayAsync(
        TimeSpan delay,
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(
                delay,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal da aplicação.
        }
    }
}