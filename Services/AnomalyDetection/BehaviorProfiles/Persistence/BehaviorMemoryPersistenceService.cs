using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

/// <summary>
/// Resultado da persistência de uma memória comportamental.
/// </summary>
public sealed record BehaviorMemoryPersistenceResult
{
    public int BehaviorId { get; init; }

    public bool CreatedBehavior { get; init; }

    public int AddedOccurrences { get; init; }

    public bool AddedSnapshot { get; init; }

    public int? SnapshotVersion { get; init; }
}

/// <summary>
/// Persiste de forma transacional um plano de memória comportamental.
///
/// Regras:
/// - comportamento é identificado por ClienteId + TagName + Codigo;
/// - repetir o mesmo plano não duplica comportamento, ocorrência ou snapshot;
/// - ocorrências são únicas pelo intervalo dentro do comportamento;
/// - quando surgem novas ocorrências para um comportamento existente,
///   um novo snapshot sequencial é criado;
/// - classificação/origem/status já existentes não são sobrescritos
///   automaticamente, preservando futuras decisões do cliente.
/// </summary>
public sealed class BehaviorMemoryPersistenceService
{
    private readonly AppDbContext _context;

    public BehaviorMemoryPersistenceService(
        AppDbContext context)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));
    }

    public async Task<BehaviorMemoryPersistenceResult> PersistAsync(
        BehaviorMemoryPersistencePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ValidatePlan(
            plan);

        IDbContextTransaction? transaction =
            null;

        var ownsTransaction =
            _context.Database.CurrentTransaction is null;

        if (ownsTransaction)
        {
            transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        cancellationToken);
        }

        try
        {
            var behavior =
                await FindBehaviorAsync(
                    plan.Behavior,
                    cancellationToken);

            var createdBehavior =
                behavior is null;

            if (behavior is null)
            {
                behavior =
                    CloneBehavior(
                        plan.Behavior);

                _context.TagsComportamentosIa.Add(
                    behavior);

                /*
                 * Precisamos do Id antes de montar os filhos.
                 */
                await _context.SaveChangesAsync(
                    cancellationToken);
            }

            var addedOccurrences =
                await AddMissingOccurrencesAsync(
                    behavior.Id,
                    plan.Occurrences,
                    cancellationToken);

            if (addedOccurrences > 0)
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }

            var snapshotVersion =
                await AddSnapshotIfNeededAsync(
                    behavior.Id,
                    plan.Snapshot,
                    createdBehavior,
                    addedOccurrences,
                    cancellationToken);

            if (snapshotVersion.HasValue)
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }

            await RefreshBehaviorAggregateAsync(
                behavior,
                plan.Behavior.Confianca,
                createdBehavior,
                cancellationToken);

            await _context.SaveChangesAsync(
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }

            return new BehaviorMemoryPersistenceResult
            {
                BehaviorId =
                    behavior.Id,

                CreatedBehavior =
                    createdBehavior,

                AddedOccurrences =
                    addedOccurrences,

                AddedSnapshot =
                    snapshotVersion.HasValue,

                SnapshotVersion =
                    snapshotVersion
            };
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(
                    cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<TagComportamentoIa?> FindBehaviorAsync(
        TagComportamentoIa source,
        CancellationToken cancellationToken)
    {
        return await _context.TagsComportamentosIa
            .SingleOrDefaultAsync(
                x =>
                    x.ClienteId ==
                        source.ClienteId &&
                    x.TagName ==
                        source.TagName &&
                    x.Codigo ==
                        source.Codigo,
                cancellationToken);
    }

    private async Task<int> AddMissingOccurrencesAsync(
        int behaviorId,
        IReadOnlyList<TagComportamentoOcorrenciaIa> planned,
        CancellationToken cancellationToken)
    {
        var existing =
            await _context.TagsComportamentosOcorrenciasIa
                .Where(x =>
                    x.TagComportamentoIaId ==
                        behaviorId)
                .Select(x =>
                    new
                    {
                        x.DataHoraInicio,
                        x.DataHoraFim
                    })
                .ToListAsync(
                    cancellationToken);

        var knownKeys =
            existing
                .Select(x =>
                    (
                        x.DataHoraInicio,
                        x.DataHoraFim
                    ))
                .ToHashSet();

        var added =
            0;

        foreach (var source in
                 planned
                     .OrderBy(x =>
                         x.DataHoraInicio)
                     .ThenBy(x =>
                         x.DataHoraFim))
        {
            var key =
                (
                    source.DataHoraInicio,
                    source.DataHoraFim
                );

            /*
             * O HashSet também elimina duplicatas repetidas
             * dentro do próprio plano.
             */
            if (!knownKeys.Add(
                    key))
            {
                continue;
            }

            _context.TagsComportamentosOcorrenciasIa.Add(
                CloneOccurrence(
                    source,
                    behaviorId));

            added++;
        }

        return added;
    }

    private async Task<int?> AddSnapshotIfNeededAsync(
        int behaviorId,
        TagComportamentoSnapshotIa source,
        bool createdBehavior,
        int addedOccurrences,
        CancellationToken cancellationToken)
    {
        /*
         * Primeira persistência sempre registra o snapshot inicial.
         *
         * Reexecução idêntica de um comportamento existente não produz
         * nova versão. Uma nova versão nasce somente quando surgem
         * novas ocorrências.
         */
        if (!createdBehavior &&
            addedOccurrences == 0)
        {
            return null;
        }

        int version;

        if (createdBehavior)
        {
            version =
                1;
        }
        else
        {
            var versions =
                await _context.TagsComportamentosSnapshotsIa
                    .Where(x =>
                        x.TagComportamentoIaId ==
                            behaviorId)
                    .Select(x =>
                        x.Versao)
                    .ToListAsync(
                        cancellationToken);

            version =
                versions.Count == 0
                    ? 1
                    : checked(
                        versions.Max() + 1);
        }

        _context.TagsComportamentosSnapshotsIa.Add(
            CloneSnapshot(
                source,
                behaviorId,
                version));

        return version;
    }

    private async Task RefreshBehaviorAggregateAsync(
        TagComportamentoIa behavior,
        double latestConfidence,
        bool createdBehavior,
        CancellationToken cancellationToken)
    {
        var persistedOccurrences =
            await _context.TagsComportamentosOcorrenciasIa
                .Where(x =>
                    x.TagComportamentoIaId ==
                        behavior.Id)
                .Select(x =>
                    new
                    {
                        x.DataHoraInicio,
                        x.DataHoraFim,
                        x.QuantidadeAmostras
                    })
                .ToListAsync(
                    cancellationToken);

        if (persistedOccurrences.Count == 0)
        {
            throw new InvalidOperationException(
                "Um comportamento persistido deve possuir ao menos uma ocorrência.");
        }

        behavior.PrimeiraObservacao =
            persistedOccurrences.Min(x =>
                x.DataHoraInicio);

        behavior.UltimaObservacao =
            persistedOccurrences.Max(x =>
                x.DataHoraFim);

        behavior.QuantidadeOcorrencias =
            persistedOccurrences.Count;

        behavior.TotalAmostras =
            persistedOccurrences.Sum(x =>
                (long)x.QuantidadeAmostras);

        behavior.Confianca =
            ClampConfidence(
                latestConfidence);

        /*
         * Não sobrescrevemos Status, Origem ou Classificacao
         * de registros existentes. Isso preserva futuras decisões
         * manuais/operacionais do cliente.
         */
        behavior.DataAtualizacao =
            createdBehavior
                ? behavior.DataAtualizacao
                : DateTime.UtcNow;
    }

    private static TagComportamentoIa CloneBehavior(
        TagComportamentoIa source)
    {
        return new TagComportamentoIa
        {
            ClienteId =
                source.ClienteId,

            TagName =
                source.TagName,

            Codigo =
                source.Codigo,

            Status =
                source.Status,

            Origem =
                source.Origem,

            Classificacao =
                source.Classificacao,

            PrimeiraObservacao =
                source.PrimeiraObservacao,

            UltimaObservacao =
                source.UltimaObservacao,

            QuantidadeOcorrencias =
                source.QuantidadeOcorrencias,

            TotalAmostras =
                source.TotalAmostras,

            Confianca =
                ClampConfidence(
                    source.Confianca),

            DataCriacao =
                source.DataCriacao,

            DataAtualizacao =
                source.DataAtualizacao
        };
    }

    private static TagComportamentoOcorrenciaIa CloneOccurrence(
        TagComportamentoOcorrenciaIa source,
        int behaviorId)
    {
        return new TagComportamentoOcorrenciaIa
        {
            TagComportamentoIaId =
                behaviorId,

            DataHoraInicio =
                source.DataHoraInicio,

            DataHoraFim =
                source.DataHoraFim,

            QuantidadeAmostras =
                source.QuantidadeAmostras,

            Confianca =
                ClampConfidence(
                    source.Confianca),

            UtilizadaNoAprendizado =
                source.UtilizadaNoAprendizado,

            DataCriacao =
                source.DataCriacao
        };
    }

    private static TagComportamentoSnapshotIa CloneSnapshot(
        TagComportamentoSnapshotIa source,
        int behaviorId,
        int version)
    {
        return new TagComportamentoSnapshotIa
        {
            TagComportamentoIaId =
                behaviorId,

            Versao =
                version,

            Media =
                source.Media,

            DesvioPadrao =
                source.DesvioPadrao,

            /*
             * B3.25 — identidade comportamental rica.
             *
             * CloneSnapshot é a fronteira final antes da gravação no EF.
             * Todas as dimensões ricas precisam ser copiadas para que o
             * snapshot persistido preserve a mesma identidade calculada
             * pelo builder/rebuild.
             */
            Mediana =
                source.Mediana,

            PrimeiroQuartil =
                source.PrimeiroQuartil,

            TerceiroQuartil =
                source.TerceiroQuartil,

            IntervaloInterquartil =
                source.IntervaloInterquartil,

            RobustScale =
                source.RobustScale,

            TypicalAbsoluteStep =
                source.TypicalAbsoluteStep,

            Minimo =
                source.Minimo,

            Maximo =
                source.Maximo,

            Amplitude =
                source.Amplitude,

            PercentualZeros =
                source.PercentualZeros,

            VariacaoMedia =
                source.VariacaoMedia,

            QuantidadePicos =
                source.QuantidadePicos,

            TotalRegistrosHistorico =
                source.TotalRegistrosHistorico,

            TotalRegistrosUsados =
                source.TotalRegistrosUsados,

            TotalOutliersRemovidos =
                source.TotalOutliersRemovidos,

            DeadbandRecomendado =
                source.DeadbandRecomendado,

            ConfiancaDeadband =
                source.ConfiancaDeadband,

            JustificativaDeadband =
                source.JustificativaDeadband,

            PeriodoInicio =
                source.PeriodoInicio,

            PeriodoFim =
                source.PeriodoFim,

            DataCriacao =
                source.DataCriacao
        };
    }

    private static double ClampConfidence(
        double value)
    {
        if (!double.IsFinite(
                value))
        {
            return 0;
        }

        return Math.Clamp(
            value,
            0,
            100);
    }

    private static void ValidatePlan(
        BehaviorMemoryPersistencePlan plan)
    {
        if (string.IsNullOrWhiteSpace(
                plan.Behavior.ClienteId))
        {
            throw new InvalidOperationException(
                "ClienteId é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(
                plan.Behavior.TagName))
        {
            throw new InvalidOperationException(
                "TagName é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(
                plan.Behavior.Codigo))
        {
            throw new InvalidOperationException(
                "Codigo do comportamento é obrigatório.");
        }

        if (plan.Occurrences.Count == 0)
        {
            throw new InvalidOperationException(
                "O plano deve possuir ao menos uma ocorrência.");
        }

        if (plan.Occurrences.Any(x =>
                x.DataHoraFim <
                x.DataHoraInicio))
        {
            throw new InvalidOperationException(
                "Nenhuma ocorrência pode terminar antes de começar.");
        }
    }
}
