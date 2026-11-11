using Microsoft.Extensions.Logging;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;

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
    private readonly IIntelligenceLabMultivariateShadowCollector?
        _shadowDiagnosticCollector;

    public MultivariateShadowRuntimeCoordinator(
        MultivariateGroupBufferService bufferService,
        MultivariateDetector detector,
        ILogger<MultivariateShadowRuntimeCoordinator> logger,
        IIntelligenceLabMultivariateShadowCollector?
            shadowDiagnosticCollector = null)
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

        _shadowDiagnosticCollector = shadowDiagnosticCollector;
    }

    public async Task<IReadOnlyList<MultivariateDetectionResult>>
        ProcessarLeituraAsync(
            string clienteId,
            string tagName,
            DateTime dataHora,
            double valor,
            string? sourceAnalysisCorrelationId = null,
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
                        dataHora,
                        sourceAnalysisCorrelationId,
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

                MultivariateDetectionResult resultado;
                try
                {
                    resultado = await _detector.DetectarAsync(
                        grupo,
                        cancellationToken: cancellationToken);
                }
                catch
                {
                    RegistrarFalhaMahalanobis(grupo);
                    throw;
                }

                RegistrarResultadoMahalanobis(grupo, resultado);

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

    private void RegistrarResultadoMahalanobis(
        BufferedGroupReading grupo,
        MultivariateDetectionResult resultado)
    {
        if (_shadowDiagnosticCollector is null)
        {
            return;
        }

        try
        {
            _shadowDiagnosticCollector.Record(
                CriarDiagnostico(
                    grupo,
                    resultado.Aplicavel
                        ? MultivariateShadowEventKind.MahalanobisExecuted
                        : MultivariateShadowEventKind.MahalanobisNotApplicable,
                    resultado.ReasonCode,
                    mahalanobisExecuted: resultado.Aplicavel,
                    anomaly: resultado.Aplicavel
                        ? resultado.EhAnomalia
                        : null,
                    normalizedScore: resultado.Aplicavel
                        ? resultado.ScoreNormalizado
                        : null,
                    threshold: resultado.Aplicavel
                        ? resultado.LimiteScore
                        : null,
                    mahalanobisSquared: resultado.Aplicavel
                        ? resultado.MahalanobisSquared
                        : null));
        }
        catch (Exception)
        {
            // Nenhuma falha de captura pode mudar o shadow.
        }
    }

    private void RegistrarFalhaMahalanobis(BufferedGroupReading grupo)
    {
        if (_shadowDiagnosticCollector is null)
        {
            return;
        }

        try
        {
            _shadowDiagnosticCollector.Record(
                CriarDiagnostico(
                    grupo,
                    MultivariateShadowEventKind.MahalanobisFailed,
                    reasonCode: "MahalanobisExecutionFailed",
                    mahalanobisExecuted: false,
                    anomaly: null,
                    normalizedScore: null,
                    threshold: null,
                    mahalanobisSquared: null));
        }
        catch (Exception)
        {
            // Fail-open.
        }
    }

    private static MultivariateShadowDiagnostic CriarDiagnostico(
        BufferedGroupReading grupo,
        MultivariateShadowEventKind eventKind,
        string? reasonCode,
        bool mahalanobisExecuted,
        bool? anomaly,
        double? normalizedScore,
        double? threshold,
        double? mahalanobisSquared)
    {
        var timestamps = grupo.TimestampsPorTag;
        var correlationIds = grupo.SourceAnalysisCorrelationIdsPorTag;
        var tags = grupo.ExpectedTags
            .Concat(grupo.Valores.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var readings = grupo.Valores
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new MultivariateShadowTagReading(
                item.Key,
                timestamps.TryGetValue(item.Key, out var timestamp)
                    ? timestamp
                    : null,
                item.Value,
                correlationIds.TryGetValue(item.Key, out var correlationId)
                    ? correlationId
                    : null))
            .ToArray();

        return new MultivariateShadowDiagnostic(
            Guid.NewGuid(),
            grupo.VectorId,
            eventKind,
            grupo.ClienteId,
            grupo.GroupName,
            grupo.MinuteIndex,
            tags,
            readings,
            grupo.MinimumAvailableTags,
            grupo.MaxLagMinutes,
            grupo.EstaCompleto(),
            reasonCode,
            mahalanobisExecuted,
            anomaly,
            normalizedScore,
            threshold,
            mahalanobisSquared,
            DateTimeOffset.UtcNow);
    }
}
