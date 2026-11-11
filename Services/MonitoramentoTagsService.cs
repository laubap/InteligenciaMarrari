using IAApi.Services.Diagnostics;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

public class MonitoramentoTagsService : BackgroundService
{
    private readonly PerformanceDiagnostics? _performanceDiagnostics;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoramentoTagsService> _logger;

    public MonitoramentoTagsService(
        IServiceScopeFactory scopeFactory,
        ILogger<MonitoramentoTagsService> logger,
        PerformanceDiagnostics? performanceDiagnostics = null)
    {
        _performanceDiagnostics = performanceDiagnostics;

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var context = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();
                var currentValuePredictionService = scope.ServiceProvider
                    .GetRequiredService<CurrentValuePredictionService>();
                var bridgeValorAtualService = scope.ServiceProvider
                    .GetRequiredService<BridgeValorAtualService>();

                var tagsAtivas = await context.TagsIaConfig
                    .Where(x => x.IaAtiva)
                    .ToListAsync(stoppingToken);

                // Um lote por cliente evita consultas redundantes e preserva
                // o isolamento da aquisicao entre clientes.
                foreach (var lote in MonitoramentoTagsBatchPlanner.CriarLotes(tagsAtivas))
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    IReadOnlyDictionary<string, ValorAtualTagResponse> valoresPorTag;
                    var tempoColeta = Stopwatch.StartNew();

                    try
                    {
                        var valores = await bridgeValorAtualService
                            .BuscarValoresAtuais(lote.NomesTags.ToList());

                        valoresPorTag = valores
                            .Where(x => !string.IsNullOrWhiteSpace(x.TagName))
                            .GroupBy(x => x.TagName, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(
                                grupo => grupo.Key,
                                grupo => grupo.First(),
                                StringComparer.OrdinalIgnoreCase);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Falha na consulta em lote ao Bridge do cliente {ClienteId} ({QuantidadeTags} tags).",
                            lote.ClienteId,
                            lote.NomesTags.Count);
                        continue;
                    }
                    finally
                    {
                        tempoColeta.Stop();
                        _logger.LogInformation(
                            "Bridge lote cliente {ClienteId}: {QuantidadeSolicitadas} tags solicitadas em {DuracaoMs} ms.",
                            lote.ClienteId,
                            lote.NomesTags.Count,
                            tempoColeta.ElapsedMilliseconds);
                    }

                    var tempoPredicoes = Stopwatch.StartNew();
                    var analisadas = 0;
                    foreach (var tag in lote.Tags)
                    {
                        stoppingToken.ThrowIfCancellationRequested();

                        var predictionStartedUtc = _performanceDiagnostics?.Enabled == true
                            ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
                        DateTime? originalReading = null;
                        var tempoTag = Stopwatch.StartNew();
                        var status = "Falha";
                        _logger.LogInformation(
                            "Predição iniciada cliente {ClienteId} tag {TagName}.",
                            tag.ClienteId,
                            tag.TagName);

                        try
                        {
                            valoresPorTag.TryGetValue(tag.TagName, out var valorAtual);
                            if (predictionStartedUtc is not null) originalReading = valorAtual?.DataHora;
                            await currentValuePredictionService.DetectarComValorAtual(
                                tag.ClienteId,
                                tag.TagName,
                                valorAtual,
                                stoppingToken);
                            analisadas++;
                            status = "Concluída";
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            status = "Cancelada";
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Falha ao monitorar tag {TagName} do cliente {ClienteId}",
                                tag.TagName,
                                tag.ClienteId);
                        }
                        finally
                        {
                            tempoTag.Stop();
                            if (predictionStartedUtc is { } startedUtc)
                            {
                                _performanceDiagnostics!.MonitoringReading(
                                    tag.ClienteId, tag.TagName, originalReading, startedUtc,
                                    DateTimeOffset.UtcNow, tempoTag.Elapsed.TotalMilliseconds, status);
                            }
                            _logger.LogInformation(
                                "Predição finalizada cliente {ClienteId} tag {TagName}: {Status} em {DuracaoMs} ms.",
                                tag.ClienteId,
                                tag.TagName,
                                status,
                                tempoTag.ElapsedMilliseconds);
                        }
                    }

                    tempoPredicoes.Stop();
                    _logger.LogInformation(
                        "Predições cliente {ClienteId}: {Concluidas}/{Total} tags em {DuracaoMs} ms.",
                        lote.ClienteId,
                        analisadas,
                        lote.Tags.Count,
                        tempoPredicoes.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro geral no monitoramento automático de tags.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
