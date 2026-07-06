using Microsoft.EntityFrameworkCore;

public class MonitoramentoTagsService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoramentoTagsService> _logger;

    public MonitoramentoTagsService(
        IServiceScopeFactory scopeFactory,
        ILogger<MonitoramentoTagsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var predictionService = scope.ServiceProvider.GetRequiredService<PredictionService>();

                var tagsAtivas = await context.TagsIaConfig
                    .Where(x => x.IaAtiva)
                    .ToListAsync(stoppingToken);

                foreach (var tag in tagsAtivas)
                {
                    try
                    {
                        await predictionService.DetectarValorAtual(
                            tag.ClienteId,
                            tag.TagName
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Falha ao monitorar tag {TagName} do cliente {ClienteId}",
                            tag.TagName,
                            tag.ClienteId
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro geral no monitoramento automático de tags.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}