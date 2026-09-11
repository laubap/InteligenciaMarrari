using Microsoft.Extensions.Logging;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV01 — integração multivariada operacional em SHADOW MODE.
///
/// Recebe cada leitura operacional, alimenta o buffer compartilhado entre
/// requests e executa o detector apenas para vetores que o buffer finalizou.
///
/// IMPORTANTE:
/// - não altera EhAnomalia;
/// - não altera score univariado;
/// - não altera risco;
/// - não dispara LLM;
/// - qualquer falha multivariada é fail-open e não derruba a detecção normal.
/// </summary>
public sealed class MultivariateShadowRuntimeCoordinator
{
    private readonly MultivariateGroupBufferService _bufferService;
    private readonly MultivariateDetector _detector;
    private readonly ILogger<MultivariateShadowRuntimeCoordinator> _logger;

    public MultivariateShadowRuntimeCoordinator(
        MultivariateGroupBufferService bufferService,
        MultivariateDetector detector,
        ILogger<MultivariateShadowRuntimeCoordinator> logger)
    {
        _bufferService =
            bufferService ??
            throw new ArgumentNullException(nameof(bufferService));

        _detector =
            detector ??
            throw new ArgumentNullException(nameof(detector));

        _logger =
            logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MultivariateDetectionResult>>
        ProcessarLeituraAsync(
            string clienteId,
            string tagName,
            DateTime dataHora,
            double valor,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        cancellationToken.ThrowIfCancellationRequested();

        if (!double.IsFinite(valor))
        {
            return [];
        }

        try
        {
            var minuteIndex =
                CriarMinuteIndex(dataHora);

            var gruposFinalizados =
                await _bufferService.AdicionarLeituraAsync(
                    clienteId,
                    tagName,
                    minuteIndex,
                    valor,
                    cancellationToken);

            if (gruposFinalizados.Count == 0)
            {
                return [];
            }

            var resultados =
                new List<MultivariateDetectionResult>(
                    gruposFinalizados.Count);

            foreach (var grupo in gruposFinalizados)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resultado =
                    await _detector.DetectarAsync(
                        grupo,
                        cancellationToken: cancellationToken);

                resultados.Add(resultado);

                if (resultado.Aplicavel)
                {
                    _logger.LogInformation(
                        "B3.24.2 MULTIVARIATE SHADOW " +
                        "Cliente={ClienteId} Grupo={GroupName} " +
                        "MinuteIndex={MinuteIndex} Tags={TagCount} " +
                        "Applicable={Applicable} Anomaly={Anomaly} " +
                        "Score={Score:F2} Threshold={Threshold:F2} " +
                        "MahalanobisSquared={MahalanobisSquared:F4}",
                        resultado.ClienteId,
                        resultado.GroupName,
                        resultado.MinuteIndex,
                        resultado.TagsUtilizadas.Count,
                        resultado.Aplicavel,
                        resultado.EhAnomalia,
                        resultado.ScoreNormalizado,
                        resultado.LimiteScore,
                        resultado.MahalanobisSquared);
                }
                else
                {
                    _logger.LogInformation(
                        "B3.24.2 MULTIVARIATE SHADOW " +
                        "Cliente={ClienteId} Grupo={GroupName} " +
                        "MinuteIndex={MinuteIndex} Applicable=false " +
                        "Reason={Reason}",
                        resultado.ClienteId,
                        resultado.GroupName,
                        resultado.MinuteIndex,
                        resultado.Explicacao);
                }
            }

            return resultados;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            /*
             * SHADOW é observacional nesta fase.
             * Uma falha aqui nunca pode impedir a análise univariada.
             */
            _logger.LogWarning(
                ex,
                "B3.24.2 MULTIVARIATE SHADOW FAIL-OPEN " +
                "Cliente={ClienteId} Tag={TagName}",
                clienteId,
                tagName);

            return [];
        }
    }

    public static long CriarMinuteIndex(
        DateTime dataHora)
    {
        /*
         * Mantém a mesma semântica adotada no treinamento operacional:
         * índice monotônico por minuto sem depender de timezone/Unix epoch
         * para DateTime com Kind=Unspecified vindo do ambiente industrial.
         */
        var referencia =
            dataHora == default
                ? DateTime.Now
                : dataHora;

        return referencia.Ticks /
               TimeSpan.TicksPerMinute;
    }
}
