using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

/// <summary>
/// Reconstrói a evidência histórica consolidada de um comportamento
/// a partir da fonte histórica canônica.
///
/// A IAApi persiste conhecimento (BEH/Candidate, ocorrências e snapshots),
/// mas não duplica o histórico bruto. Por isso, quando precisamos
/// reavaliar uma memória persistida, buscamos novamente os intervalos
/// utilizados no aprendizado.
///
/// Cada ocorrência é tratada separadamente:
/// histórico bruto
/// -> preservação do relógio do historiador
/// -> sanitização estrutural
/// -> caracterização completa do episódio
/// -> agregação opcional em snapshot.
///
/// A caracterização completa é reutilizável pela reconciliação cross-run,
/// preservando a mesma semântica de similaridade usada na descoberta.
/// </summary>
public sealed class BehaviorMemorySnapshotRebuildService
{
    private readonly AppDbContext _context;

    private readonly IHistoricoService _historicoService;

    private readonly BehaviorCandidateEpisodeCharacterizer _characterizer;

    private readonly BehaviorMemoryPersistencePlanBuilder _snapshotBuilder;

    public BehaviorMemorySnapshotRebuildService(
        AppDbContext context,
        IHistoricoService historicoService,
        TagProfileStatisticsCalculator statisticsCalculator,
        BehaviorMemoryPersistencePlanBuilder snapshotBuilder)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));

        _historicoService =
            historicoService ??
            throw new ArgumentNullException(
                nameof(historicoService));

        ArgumentNullException.ThrowIfNull(
            statisticsCalculator);

        _characterizer =
            new BehaviorCandidateEpisodeCharacterizer(
                statisticsCalculator);

        _snapshotBuilder =
            snapshotBuilder ??
            throw new ArgumentNullException(
                nameof(snapshotBuilder));
    }

    /// <summary>
    /// Reconstrói todas as ocorrências habilitadas para aprendizado
    /// como características comportamentais completas.
    ///
    /// Esse método é reutilizado tanto pelo snapshot cumulativo quanto,
    /// posteriormente, pela reconciliação de Candidates entre execuções.
    /// </summary>
    public async Task<IReadOnlyList<BehaviorCandidateEpisodeCharacteristics>>
        RebuildCharacteristicsAsync(
            int behaviorId,
            string clienteId,
            string tagName,
            IReadOnlyList<TagComportamentoOcorrenciaIa>? pendingOccurrences = null,
            CancellationToken cancellationToken = default)
    {
        if (behaviorId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(behaviorId));
        }

        ValidateText(
            clienteId,
            nameof(clienteId));

        ValidateText(
            tagName,
            nameof(tagName));

        var behaviorExists =
            await _context.TagsComportamentosIa
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id ==
                            behaviorId &&
                        x.ClienteId ==
                            clienteId &&
                        x.TagName ==
                            tagName,
                    cancellationToken);

        if (!behaviorExists)
        {
            throw new InvalidOperationException(
                "O comportamento informado não pertence ao cliente/tag solicitados.");
        }

        var persistedOccurrences =
            await _context.TagsComportamentosOcorrenciasIa
                .AsNoTracking()
                .Where(x =>
                    x.TagComportamentoIaId ==
                        behaviorId &&
                    x.UtilizadaNoAprendizado)
                .OrderBy(x =>
                    x.DataHoraInicio)
                .ThenBy(x =>
                    x.DataHoraFim)
                .ToListAsync(
                    cancellationToken);

        var intervals =
            persistedOccurrences
                .Select(x =>
                    new OccurrenceInterval(
                        x.DataHoraInicio,
                        x.DataHoraFim))
                .Concat(
                    (pendingOccurrences ??
                     Array.Empty<TagComportamentoOcorrenciaIa>())
                    .Where(x =>
                        x is not null &&
                        x.UtilizadaNoAprendizado)
                    .Select(x =>
                        new OccurrenceInterval(
                            x.DataHoraInicio,
                            x.DataHoraFim)))
                .Distinct()
                .OrderBy(x =>
                    x.Start)
                .ThenBy(x =>
                    x.End)
                .ToArray();

        if (intervals.Length == 0)
        {
            throw new InvalidOperationException(
                "Não existem ocorrências habilitadas para reconstruir a memória.");
        }

        foreach (var interval in intervals)
        {
            if (interval.End <
                interval.Start)
            {
                throw new InvalidOperationException(
                    "Uma ocorrência de aprendizado possui intervalo inválido.");
            }
        }

        var characteristics =
            new List<BehaviorCandidateEpisodeCharacteristics>(
                intervals.Length);

        for (var index = 0;
             index < intervals.Length;
             index++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var interval =
                intervals[index];

            /*
             * O Bridge/PowerServer pode tratar o limite final da consulta
             * como exclusivo. Consultamos com uma pequena folga de 1 segundo
             * para permitir recuperar uma leitura exatamente em interval.End.
             *
             * Essa folga vale somente para a consulta ao historiador.
             * O filtro abaixo continua usando o intervalo persistido original,
             * portanto nenhuma leitura posterior à ocorrência entra no episódio.
             */
            var queryEnd =
                interval.End == DateTime.MaxValue
                    ? interval.End
                    : interval.End.AddSeconds(1);

            var raw =
                _historicoService
                    .BuscarHistoricoPorPeriodo(
                        clienteId,
                        tagName,
                        interval.Start,
                        queryEnd)
                ?? new List<LeituraBruta>();

            /*
             * Alguns provedores podem devolver pontos fora do período.
             * O intervalo persistido continua sendo o limite autoritativo.
             */
            var detectorHistory =
                raw
                    .Where(x =>
                        x is not null)
                    .Select(x =>
                        new
                        {
                            Timestamp =
                                x.DataHora,

                            x.Valor
                        })
                    .Where(x =>
                        x.Timestamp >=
                            interval.Start &&
                        x.Timestamp <=
                            interval.End)
                    .Select(x =>
                        new HistoricoDetectorPoint
                        {
                            DataHora =
                                x.Timestamp,

                            Valor =
                                x.Valor
                        })
                    .ToArray();

            var sanitization =
                DetectionHistorySanitizer
                    .Sanitize(
                        detectorHistory);

            var structurallyClean =
                sanitization.History
                    .Select(x =>
                        new LeituraBruta
                        {
                            DataHora =
                                x.DataHora,

                            Valor =
                                Convert.ToSingle(
                                    x.Valor)
                        })
                    .ToArray();

            if (structurallyClean.Length <
                BehaviorCandidateEpisodeCharacterizer.MinimumSamples)
            {
                throw new InvalidOperationException(
                    $"Histórico insuficiente para reconstruir a ocorrência " +
                    $"{interval.Start:O}..{interval.End:O}. " +
                    $"Foram obtidas {structurallyClean.Length} leituras válidas.");
            }

            var episode =
                new BehaviorCandidateEpisode
                {
                    SourceSegmentIndex =
                        0,

                    EpisodeIndex =
                        index,

                    StartReadingIndex =
                        0,

                    EndReadingIndex =
                        structurallyClean.Length - 1,

                    Inicio =
                        interval.Start,

                    Fim =
                        interval.End,

                    Historico =
                        structurallyClean,

                    OpeningChangePoint =
                        null
                };

            var characteristic =
                _characterizer.Characterize(
                    episode);

            if (!characteristic.IsCharacterizable ||
                characteristic.ProfileFeatures is null)
            {
                throw new InvalidOperationException(
                    $"Não foi possível reconstruir completamente a ocorrência " +
                    $"{interval.Start:O}..{interval.End:O}: " +
                    $"{characteristic.Reason}");
            }

            characteristics.Add(
                characteristic);
        }

        return characteristics;
    }

    public async Task<TagComportamentoSnapshotIa> RebuildAsync(
        int behaviorId,
        string clienteId,
        string tagName,
        IReadOnlyList<TagComportamentoOcorrenciaIa>? pendingOccurrences = null,
        CancellationToken cancellationToken = default)
    {
        var characteristics =
            await RebuildCharacteristicsAsync(
                behaviorId,
                clienteId,
                tagName,
                pendingOccurrences,
                cancellationToken);

        var snapshot =
            _snapshotBuilder
                .BuildSnapshotPreview(
                    characteristics,
                    version: 1);

        snapshot.JustificativaDeadband =
            $"Snapshot consolidado reconstruído do historiador a partir de " +
            $"{characteristics.Count} ocorrências habilitadas para aprendizado. " +
            snapshot.JustificativaDeadband;

        return snapshot;
    }


    private static void ValidateText(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "O valor é obrigatório.",
                parameterName);
        }
    }

    private readonly record struct OccurrenceInterval(
        DateTime Start,
        DateTime End);
}
