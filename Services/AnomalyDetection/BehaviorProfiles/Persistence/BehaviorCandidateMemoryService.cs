using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public enum BehaviorCandidateMemoryOutcome
{
    PersistedNewCandidate = 0,
    PersistedExistingCandidate = 1,
    Ambiguous = 2
}

public sealed record BehaviorCandidateMemoryComparison
{
    public int BehaviorId { get; init; }

    public string BehaviorCode { get; init; } = "";

    public bool IsCompatible { get; init; }

    public int PairCount { get; init; }

    public string Reason { get; init; } = "";
}

public sealed record BehaviorCandidateMemoryResult
{
    public BehaviorCandidateMemoryOutcome Outcome { get; init; }

    public string? BehaviorCode { get; init; }

    public int? BehaviorId { get; init; }

    public BehaviorIdentityConfidenceResult? IdentityConfidence { get; init; }

    public BehaviorMemoryPersistenceResult? Persistence { get; init; }

    /// <summary>
    /// Quantidade de ocorrências únicas após combinar memória persistida
    /// e evidência nova da execução atual.
    /// </summary>
    public int CumulativeOccurrenceCount { get; init; }

    public DateTime? CumulativeFirstOccurrenceStart { get; init; }

    public DateTime? CumulativeLastOccurrenceEnd { get; init; }

    /// <summary>
    /// A memória acumulada já contém repetição do mesmo padrão em
    /// ao menos duas ocorrências distintas e compatíveis.
    /// </summary>
    public bool HasCumulativeRecurrenceEvidence { get; init; }

    public IReadOnlyList<BehaviorCandidateMemoryComparison> Comparisons { get; init; } =
        Array.Empty<BehaviorCandidateMemoryComparison>();

    public string Reason { get; init; } = "";
}

public sealed record BehaviorCandidatePromotionResult
{
    public int BehaviorId { get; init; }

    public string PreviousBehaviorCode { get; init; } = "";

    public string BehaviorCode { get; init; } = "";

    public bool Promoted { get; init; }

    public string Reason { get; init; } = "";
}

/// <summary>
/// Indica que a identidade BEH proposta foi reservada por outro
/// comportamento entre a proposta e a tentativa de promoção.
///
/// O índice único do banco continua sendo a autoridade final; esta exceção
/// apenas transforma a colisão em um resultado de domínio tratável pela
/// camada de consolidação.
/// </summary>
public sealed class BehaviorStableIdentityConflictException :
    InvalidOperationException
{
    public BehaviorStableIdentityConflictException(
        string behaviorCode,
        Exception? innerException = null)
        : base(
            $"A identidade {behaviorCode} já está reservada para outro comportamento desta tag.",
            innerException)
    {
        BehaviorCode =
            behaviorCode;
    }

    public string BehaviorCode { get; }
}

/// <summary>
/// Persiste e reconcilia memória comportamental ainda imatura entre execuções.
///
/// Regras desta etapa:
/// - Candidates automáticos usam o mesmo agregado persistente já existente;
/// - identidade provisória usa CAND-{guid}, nunca BEH-xxx;
/// - BEH-xxx continua reservado a lifecycle Known;
/// - matching reutiliza o BehaviorCandidateEpisodeSimilarityComparer;
/// - um Candidate atual só corresponde a uma memória Candidate quando TODAS
///   as suas ocorrências são semelhantes a TODAS as ocorrências persistidas;
/// - mais de um Candidate compatível é ambiguidade e não é fundido;
/// - repetir o mesmo intervalo permanece idempotente pelo serviço de persistência;
/// - a consolidação superior pode solicitar promoção explícita para Known quando
///   a evidência cumulativa satisfizer o lifecycle.
///
/// A promoção preserva o mesmo registro, ocorrências e snapshots.
/// </summary>
public sealed class BehaviorCandidateMemoryService
{
    private const int MaximumStableIdentityReservationAttempts = 10;

    private readonly AppDbContext _context;

    private readonly BehaviorCandidateEpisodeSimilarityComparer _comparer;

    private readonly BehaviorIdentityConfidenceCalculator _confidenceCalculator;

    private readonly BehaviorMemoryPersistencePlanBuilder _planBuilder;

    private readonly BehaviorMemoryPersistenceService _persistenceService;

    private readonly BehaviorMemorySnapshotRebuildService _snapshotRebuildService;

    private readonly BehaviorIdentityProposalService _identityProposalService;

    public BehaviorCandidateMemoryService(
        AppDbContext context,
        BehaviorCandidateEpisodeSimilarityComparer comparer,
        BehaviorIdentityConfidenceCalculator confidenceCalculator,
        BehaviorMemoryPersistencePlanBuilder planBuilder,
        BehaviorMemoryPersistenceService persistenceService,
        BehaviorMemorySnapshotRebuildService snapshotRebuildService,
        BehaviorIdentityProposalService? identityProposalService = null)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));

        _comparer =
            comparer ??
            throw new ArgumentNullException(
                nameof(comparer));

        _confidenceCalculator =
            confidenceCalculator ??
            throw new ArgumentNullException(
                nameof(confidenceCalculator));

        _planBuilder =
            planBuilder ??
            throw new ArgumentNullException(
                nameof(planBuilder));

        _persistenceService =
            persistenceService ??
            throw new ArgumentNullException(
                nameof(persistenceService));

        _snapshotRebuildService =
            snapshotRebuildService ??
            throw new ArgumentNullException(
                nameof(snapshotRebuildService));

        _identityProposalService =
            identityProposalService ??
            new BehaviorIdentityProposalService();
    }

    public async Task<BehaviorCandidateMemoryResult> ConsolidateAsync(
        string clienteId,
        string tagName,
        BehaviorDiscoveryCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ValidateText(
            clienteId,
            nameof(clienteId));

        ValidateText(
            tagName,
            nameof(tagName));

        ArgumentNullException.ThrowIfNull(
            candidate);

        var incoming =
            candidate.Occurrences
                .Where(x =>
                    x is not null &&
                    x.IsCharacterizable &&
                    x.ProfileFeatures is not null)
                .OrderBy(x =>
                    x.Inicio)
                .ThenBy(x =>
                    x.Fim)
                .ThenBy(x =>
                    x.SourceSegmentIndex)
                .ThenBy(x =>
                    x.EpisodeIndex)
                .ToArray();

        if (incoming.Length == 0)
        {
            throw new InvalidOperationException(
                "O Candidate não possui ocorrências caracterizáveis para persistência cross-run.");
        }

        var persistedCandidates =
            await _context.TagsComportamentosIa
                .AsNoTracking()
                .Where(x =>
                    x.ClienteId ==
                        clienteId &&
                    x.TagName ==
                        tagName &&
                    x.Status ==
                        TagComportamentoIa.StatusCandidate &&
                    x.Origem ==
                        TagComportamentoIa.OrigemAutomatic)
                .OrderBy(x =>
                    x.Id)
                .ToArrayAsync(
                    cancellationToken);

        var comparisons =
            new List<BehaviorCandidateMemoryComparison>(
                persistedCandidates.Length);

        var compatible =
            new List<CandidateMatch>();

        foreach (var persistedCandidate in persistedCandidates)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var persistedCharacteristics =
                await _snapshotRebuildService
                    .RebuildCharacteristicsAsync(
                        persistedCandidate.Id,
                        clienteId,
                        tagName,
                        cancellationToken:
                            cancellationToken);

            EnsurePersistedMemoryIsInternallyConsistent(
                persistedCandidate,
                persistedCharacteristics);

            var evaluation =
                CompareCompleteLink(
                    incoming,
                    persistedCharacteristics);

            comparisons.Add(
                new BehaviorCandidateMemoryComparison
                {
                    BehaviorId =
                        persistedCandidate.Id,

                    BehaviorCode =
                        persistedCandidate.Codigo,

                    IsCompatible =
                        evaluation.IsCompatible,

                    PairCount =
                        evaluation.PairCount,

                    Reason =
                        evaluation.Reason
                });

            if (evaluation.IsCompatible)
            {
                compatible.Add(
                    new CandidateMatch(
                        persistedCandidate,
                        persistedCharacteristics));
            }
        }

        if (compatible.Count > 1)
        {
            return new BehaviorCandidateMemoryResult
            {
                Outcome =
                    BehaviorCandidateMemoryOutcome.Ambiguous,

                Comparisons =
                    comparisons,

                Reason =
                    "A descoberta atual é compatível com mais de uma memória Candidate persistida; nenhuma fusão foi aplicada automaticamente."
            };
        }

        if (compatible.Count == 1)
        {
            return await PersistExistingCandidateAsync(
                clienteId,
                tagName,
                incoming,
                compatible[0],
                comparisons,
                cancellationToken);
        }

        return await PersistNewCandidateAsync(
            clienteId,
            tagName,
            incoming,
            comparisons,
            cancellationToken);
    }

    private async Task<BehaviorCandidateMemoryResult>
        PersistNewCandidateAsync(
            string clienteId,
            string tagName,
            IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> incoming,
            IReadOnlyList<BehaviorCandidateMemoryComparison> comparisons,
            CancellationToken cancellationToken)
    {
        var evidence =
            BuildEvidence(
                incoming);

        var provisionalCode =
            CreateProvisionalCode();

        var plan =
            BuildCandidatePlan(
                clienteId,
                tagName,
                provisionalCode,
                incoming,
                evidence);

        var persistence =
            await _persistenceService
                .PersistAsync(
                    plan,
                    cancellationToken);

        return new BehaviorCandidateMemoryResult
        {
            Outcome =
                BehaviorCandidateMemoryOutcome.PersistedNewCandidate,

            BehaviorCode =
                provisionalCode,

            BehaviorId =
                persistence.BehaviorId,

            IdentityConfidence =
                evidence.Confidence,

            Persistence =
                persistence,

            CumulativeOccurrenceCount =
                evidence.UniqueOccurrences.Count,

            CumulativeFirstOccurrenceStart =
                GetFirstOccurrenceStart(
                    evidence.UniqueOccurrences),

            CumulativeLastOccurrenceEnd =
                GetLastOccurrenceEnd(
                    evidence.UniqueOccurrences),

            HasCumulativeRecurrenceEvidence =
                evidence.UniqueOccurrences.Count >= 2,

            Comparisons =
                comparisons,

            Reason =
                $"Nova memória Candidate persistida como {provisionalCode}. Ela ainda não possui identidade BEH e não é considerada Known."
        };
    }

    private async Task<BehaviorCandidateMemoryResult>
        PersistExistingCandidateAsync(
            string clienteId,
            string tagName,
            IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> incoming,
            CandidateMatch match,
            IReadOnlyList<BehaviorCandidateMemoryComparison> comparisons,
            CancellationToken cancellationToken)
    {
        var combined =
            match.PersistedCharacteristics
                .Concat(
                    incoming)
                .ToArray();

        var evidence =
            BuildEvidence(
                combined);

        var plan =
            BuildCandidatePlan(
                clienteId,
                tagName,
                match.Behavior.Codigo,
                incoming,
                evidence);

        var consolidatedSnapshot =
            await _snapshotRebuildService
                .RebuildAsync(
                    match.Behavior.Id,
                    clienteId,
                    tagName,
                    plan.Occurrences,
                    cancellationToken);

        plan =
            plan with
            {
                Snapshot =
                    consolidatedSnapshot
            };

        var persistence =
            await _persistenceService
                .PersistAsync(
                    plan,
                    cancellationToken);

        return new BehaviorCandidateMemoryResult
        {
            Outcome =
                BehaviorCandidateMemoryOutcome.PersistedExistingCandidate,

            BehaviorCode =
                match.Behavior.Codigo,

            BehaviorId =
                persistence.BehaviorId,

            IdentityConfidence =
                evidence.Confidence,

            Persistence =
                persistence,

            CumulativeOccurrenceCount =
                evidence.UniqueOccurrences.Count,

            CumulativeFirstOccurrenceStart =
                GetFirstOccurrenceStart(
                    evidence.UniqueOccurrences),

            CumulativeLastOccurrenceEnd =
                GetLastOccurrenceEnd(
                    evidence.UniqueOccurrences),

            HasCumulativeRecurrenceEvidence =
                evidence.UniqueOccurrences.Count >= 2,

            Comparisons =
                comparisons,

            Reason =
                $"A descoberta atual foi reconciliada com a memória Candidate existente {match.Behavior.Codigo}. A memória cumulativa pode ser promovida pela consolidação quando satisfizer o lifecycle Known."
        };
    }

    /// <summary>
    /// Promove um Candidate cumulativamente maduro usando a próxima identidade
    /// BEH disponível.
    ///
    /// A proposta inicial pode ficar obsoleta entre a leitura das reservas e a
    /// escrita. Quando o índice único do banco sinaliza que outra execução
    /// venceu a corrida, as reservas são recarregadas e uma nova proposta é
    /// gerada pelo BehaviorIdentityProposalService.
    ///
    /// O mesmo Candidate nunca é duplicado: se outra execução já o tiver
    /// promovido, PromoteToKnownAsync devolve a identidade persistida.
    /// </summary>
    public async Task<BehaviorCandidatePromotionResult>
        PromoteToNextAvailableKnownAsync(
            int behaviorId,
            string clienteId,
            string tagName,
            BehaviorMemoryLifecycleEvaluation cumulativeLifecycle,
            BehaviorIdentityConfidenceResult identityConfidence,
            IReadOnlyCollection<string>? initiallyReservedIdentities = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            cumulativeLifecycle);

        ArgumentNullException.ThrowIfNull(
            identityConfidence);

        if (cumulativeLifecycle.Lifecycle !=
                BehaviorMemoryLifecycle.Known ||
            !cumulativeLifecycle.EligibleForStableIdentity)
        {
            throw new InvalidOperationException(
                "A promoção automática exige lifecycle Known elegível para identidade estável.");
        }

        var reservedIdentities =
            new HashSet<string>(
                initiallyReservedIdentities ??
                Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

        for (var attempt = 1;
             attempt <= MaximumStableIdentityReservationAttempts;
             attempt++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var proposal =
                _identityProposalService
                    .Create(
                        new[]
                        {
                            cumulativeLifecycle
                        },
                        reservedIdentities)
                    .Single();

            if (!proposal.HasStableIdentityProposal ||
                string.IsNullOrWhiteSpace(
                    proposal.ProposedIdentity))
            {
                throw new InvalidOperationException(
                    "O Candidate maduro não recebeu proposta de identidade BEH.");
            }

            try
            {
                return await PromoteToKnownAsync(
                    behaviorId,
                    clienteId,
                    tagName,
                    proposal.ProposedIdentity,
                    identityConfidence,
                    cancellationToken);
            }
            catch (BehaviorStableIdentityConflictException conflict)
            {
                /*
                 * A proposta ficou obsoleta. O código em conflito entra
                 * imediatamente na reserva local e recarregamos todas as
                 * identidades confirmadas no banco antes da próxima tentativa.
                 */
                reservedIdentities.Add(
                    conflict.BehaviorCode);

                var persistedReservations =
                    await LoadReservedIdentityCodesAsync(
                        clienteId,
                        tagName,
                        cancellationToken);

                foreach (var persistedCode in
                         persistedReservations)
                {
                    reservedIdentities.Add(
                        persistedCode);
                }
            }
        }

        throw new InvalidOperationException(
            $"Não foi possível reservar uma identidade estável após {MaximumStableIdentityReservationAttempts} tentativas concorrentes.");
    }

    /// <summary>
    /// Promove uma memória Candidate automática para Known preservando
    /// o mesmo Id, ocorrências e snapshots já persistidos.
    ///
    /// Somente Codigo, Status, confiança consolidada e DataAtualizacao
    /// podem mudar nesta operação. Origem e Classificacao são preservadas.
    /// </summary>
    public async Task<BehaviorCandidatePromotionResult> PromoteToKnownAsync(
        int behaviorId,
        string clienteId,
        string tagName,
        string stableBehaviorCode,
        BehaviorIdentityConfidenceResult identityConfidence,
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

        ValidateText(
            stableBehaviorCode,
            nameof(stableBehaviorCode));

        ArgumentNullException.ThrowIfNull(
            identityConfidence);

        if (!identityConfidence.CanEvaluate)
        {
            throw new InvalidOperationException(
                "A promoção para Known exige confiança de identidade avaliável.");
        }

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
                await _context.TagsComportamentosIa
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id == behaviorId &&
                            x.ClienteId == clienteId &&
                            x.TagName == tagName,
                        cancellationToken);

            if (behavior is null)
            {
                throw new InvalidOperationException(
                    "A memória Candidate solicitada para promoção não foi encontrada.");
            }

            /*
             * Outra execução pode ter promovido o mesmo Candidate depois
             * da proposta local de BEH e antes desta tentativa.
             *
             * Nesse caso a identidade já persistida vence. Não tentamos
             * renomear um Known nem tratamos isso como erro.
             */
            if (behavior.Status ==
                TagComportamentoIa.StatusKnown)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(
                        cancellationToken);
                }

                return CreateAlreadyPromotedResult(
                    behavior,
                    stableBehaviorCode);
            }

            if (behavior.Status !=
                    TagComportamentoIa.StatusCandidate ||
                behavior.Origem !=
                    TagComportamentoIa.OrigemAutomatic)
            {
                throw new InvalidOperationException(
                    "Somente memória Candidate automática pode ser promovida por este fluxo.");
            }

            var identityAlreadyUsed =
                await IsIdentityUsedByAnotherBehaviorAsync(
                    behaviorId,
                    clienteId,
                    tagName,
                    stableBehaviorCode,
                    cancellationToken);

            if (identityAlreadyUsed)
            {
                throw new BehaviorStableIdentityConflictException(
                    stableBehaviorCode);
            }

            var previousCode =
                behavior.Codigo;

            behavior.Codigo =
                stableBehaviorCode;

            behavior.Status =
                TagComportamentoIa.StatusKnown;

            behavior.Confianca =
                Math.Clamp(
                    identityConfidence.Confidence,
                    0,
                    100);

            behavior.DataAtualizacao =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }

            return new BehaviorCandidatePromotionResult
            {
                BehaviorId =
                    behavior.Id,

                PreviousBehaviorCode =
                    previousCode,

                BehaviorCode =
                    behavior.Codigo,

                Promoted =
                    true,

                Reason =
                    $"Memória Candidate {previousCode} promovida para Known como {behavior.Codigo}, preservando behaviorId {behavior.Id}."
            };
        }
        catch (DbUpdateException exception)
            when (transaction is not null)
        {
            /*
             * O banco possui índice único em ClienteId + TagName + Codigo.
             * Ele é a autoridade final contra duas reservas simultâneas.
             *
             * Se perdemos a corrida, encerramos a nossa transação e lemos
             * novamente o estado já confirmado pela outra execução.
             */
            await transaction.RollbackAsync(
                CancellationToken.None);

            await transaction.DisposeAsync();
            transaction =
                null;

            _context.ChangeTracker.Clear();

            var persistedBehavior =
                await _context.TagsComportamentosIa
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id == behaviorId &&
                            x.ClienteId == clienteId &&
                            x.TagName == tagName,
                        cancellationToken);

            if (persistedBehavior is not null &&
                persistedBehavior.Status ==
                    TagComportamentoIa.StatusKnown)
            {
                return CreateAlreadyPromotedResult(
                    persistedBehavior,
                    stableBehaviorCode);
            }

            var identityWasTaken =
                await IsIdentityUsedByAnotherBehaviorAsync(
                    behaviorId,
                    clienteId,
                    tagName,
                    stableBehaviorCode,
                    cancellationToken);

            if (identityWasTaken)
            {
                throw new BehaviorStableIdentityConflictException(
                    stableBehaviorCode,
                    exception);
            }

            throw;
        }
        catch (DbUpdateException exception)
        {
            /*
             * Quando a promoção participa de uma transação externa,
             * o commit/rollback pertence ao chamador.
             *
             * O EF cria savepoint para SaveChanges dentro de uma transação
             * ativa; após a falha limpamos apenas o tracking local e
             * verificamos se a causa foi uma reserva concorrente de BEH.
             *
             * - identidade ocupada: converte para conflito tratável e o
             *   PromoteToNextAvailableKnownAsync tenta o próximo BEH;
             * - qualquer outra falha (ex.: trigger do teste de atomicidade):
             *   propaga DbUpdateException para o chamador fazer rollback
             *   da unidade completa Candidate + promoção.
             */
            _context.ChangeTracker.Clear();

            var persistedBehavior =
                await _context.TagsComportamentosIa
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id == behaviorId &&
                            x.ClienteId == clienteId &&
                            x.TagName == tagName,
                        cancellationToken);

            if (persistedBehavior is not null &&
                persistedBehavior.Status ==
                    TagComportamentoIa.StatusKnown)
            {
                return CreateAlreadyPromotedResult(
                    persistedBehavior,
                    stableBehaviorCode);
            }

            var identityWasTaken =
                await IsIdentityUsedByAnotherBehaviorAsync(
                    behaviorId,
                    clienteId,
                    tagName,
                    stableBehaviorCode,
                    cancellationToken);

            if (identityWasTaken)
            {
                throw new BehaviorStableIdentityConflictException(
                    stableBehaviorCode,
                    exception);
            }

            throw;
        }

        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
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

    private async Task<IReadOnlyList<string>>
        LoadReservedIdentityCodesAsync(
            string clienteId,
            string tagName,
            CancellationToken cancellationToken)
    {
        return await _context.TagsComportamentosIa
            .AsNoTracking()
            .Where(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName)
            .Select(x =>
                x.Codigo)
            .Where(x =>
                !string.IsNullOrWhiteSpace(
                    x))
            .Distinct()
            .ToArrayAsync(
                cancellationToken);
    }

    private async Task<bool> IsIdentityUsedByAnotherBehaviorAsync(
        int behaviorId,
        string clienteId,
        string tagName,
        string stableBehaviorCode,
        CancellationToken cancellationToken)
    {
        return await _context.TagsComportamentosIa
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id != behaviorId &&
                    x.ClienteId == clienteId &&
                    x.TagName == tagName &&
                    x.Codigo == stableBehaviorCode,
                cancellationToken);
    }

    private static BehaviorCandidatePromotionResult CreateAlreadyPromotedResult(
        TagComportamentoIa behavior,
        string requestedBehaviorCode)
    {
        var sameIdentity =
            string.Equals(
                behavior.Codigo,
                requestedBehaviorCode,
                StringComparison.OrdinalIgnoreCase);

        return new BehaviorCandidatePromotionResult
        {
            BehaviorId =
                behavior.Id,

            PreviousBehaviorCode =
                behavior.Codigo,

            BehaviorCode =
                behavior.Codigo,

            Promoted =
                false,

            Reason =
                sameIdentity
                    ? $"O comportamento {behavior.Codigo} já estava promovido para Known."
                    : $"O comportamento já foi promovido por outra execução como {behavior.Codigo}; a proposta obsoleta {requestedBehaviorCode} foi descartada."
        };
    }

    private BehaviorMemoryPersistencePlan BuildCandidatePlan(
        string clienteId,
        string tagName,
        string behaviorCode,
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> incoming,
        CandidateEvidence evidence)
    {
        var createdAt =
            DateTime.UtcNow;

        var behavior =
            new TagComportamentoIa
            {
                ClienteId =
                    clienteId,

                TagName =
                    tagName,

                Codigo =
                    behaviorCode,

                Status =
                    TagComportamentoIa.StatusCandidate,

                Origem =
                    TagComportamentoIa.OrigemAutomatic,

                Classificacao =
                    TagComportamentoIa.ClassificacaoUndefined,

                PrimeiraObservacao =
                    incoming.Min(x =>
                        x.Inicio),

                UltimaObservacao =
                    incoming.Max(x =>
                        x.Fim),

                QuantidadeOcorrencias =
                    incoming.Count,

                TotalAmostras =
                    incoming.Sum(x =>
                        (long)x.UsedSampleCount),

                Confianca =
                    evidence.Confidence.CanEvaluate
                        ? evidence.Confidence.Confidence
                        : 0,

                DataCriacao =
                    createdAt
            };

        var occurrences =
            incoming
                .Select(x =>
                {
                    var interval =
                        new OccurrenceInterval(
                            x.Inicio,
                            x.Fim);

                    evidence.ConfidenceByInterval
                        .TryGetValue(
                            interval,
                            out var confidence);

                    return new TagComportamentoOcorrenciaIa
                    {
                        DataHoraInicio =
                            x.Inicio,

                        DataHoraFim =
                            x.Fim,

                        QuantidadeAmostras =
                            x.UsedSampleCount,

                        Confianca =
                            confidence,

                        /*
                         * Candidate continua fora do runtime operacional,
                         * porém esta ocorrência é evidência aceita para
                         * construir/reconciliar a própria memória Candidate.
                         */
                        UtilizadaNoAprendizado =
                            true,

                        DataCriacao =
                            createdAt
                    };
                })
                .ToArray();

        var snapshot =
            _planBuilder
                .BuildSnapshotPreview(
                    incoming,
                    version: 1,
                    createdAtUtc: createdAt);

        snapshot.JustificativaDeadband =
            "Snapshot provisório de memória Candidate. " +
            snapshot.JustificativaDeadband;

        return new BehaviorMemoryPersistencePlan
        {
            Behavior =
                behavior,

            Occurrences =
                occurrences,

            Snapshot =
                snapshot
        };
    }

    private CandidateEvidence BuildEvidence(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> source)
    {
        var unique =
            source
                .Where(x =>
                    x is not null &&
                    x.IsCharacterizable &&
                    x.ProfileFeatures is not null)
                .GroupBy(x =>
                    new OccurrenceInterval(
                        x.Inicio,
                        x.Fim))
                .Select(group =>
                    group.First())
                .OrderBy(x =>
                    x.Inicio)
                .ThenBy(x =>
                    x.Fim)
                .ToArray();

        var normalized =
            unique
                .Select(
                    (occurrence, index) =>
                        occurrence with
                        {
                            SourceSegmentIndex =
                                0,

                            EpisodeIndex =
                                index
                        })
                .ToArray();

        var confidence =
            _confidenceCalculator
                .Calculate(
                    normalized);

        var confidenceByInterval =
            unique.ToDictionary(
                x =>
                    new OccurrenceInterval(
                        x.Inicio,
                        x.Fim),
                _ =>
                    0.0);

        if (confidence.CanEvaluate)
        {
            foreach (var occurrenceConfidence in
                     confidence.Occurrences)
            {
                if (occurrenceConfidence.EpisodeIndex < 0 ||
                    occurrenceConfidence.EpisodeIndex >=
                        unique.Length)
                {
                    continue;
                }

                var occurrence =
                    unique[
                        occurrenceConfidence.EpisodeIndex];

                confidenceByInterval[
                    new OccurrenceInterval(
                        occurrence.Inicio,
                        occurrence.Fim)] =
                    Math.Clamp(
                        occurrenceConfidence.Confidence,
                        0,
                        100);
            }
        }

        return new CandidateEvidence(
            confidence,
            confidenceByInterval,
            unique);
    }

    private CompleteLinkEvaluation CompareCompleteLink(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> incoming,
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> persisted)
    {
        if (persisted.Count == 0)
        {
            return new CompleteLinkEvaluation(
                false,
                0,
                "A memória Candidate persistida não possui ocorrências reconstruíveis.");
        }

        var pairCount =
            0;

        foreach (var current in incoming)
        {
            foreach (var historical in persisted)
            {
                pairCount++;

                var comparison =
                    _comparer.Compare(
                        current,
                        historical);

                if (!comparison.CanCompare)
                {
                    return new CompleteLinkEvaluation(
                        false,
                        pairCount,
                        "Ao menos um par Candidate atual/persistido não pôde ser comparado.");
                }

                if (!comparison.IsSimilar)
                {
                    return new CompleteLinkEvaluation(
                        false,
                        pairCount,
                        "Ao menos um par Candidate atual/persistido não satisfez a regra completa de similaridade.");
                }
            }
        }

        return new CompleteLinkEvaluation(
            true,
            pairCount,
            $"Todas as {pairCount} comparações Candidate atual/persistido satisfizeram a regra completa de similaridade.");
    }

    private void EnsurePersistedMemoryIsInternallyConsistent(
        TagComportamentoIa behavior,
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> characteristics)
    {
        if (characteristics.Count < 2)
        {
            return;
        }

        var confidence =
            _confidenceCalculator
                .Calculate(
                    characteristics
                        .Select(
                            (occurrence, index) =>
                                occurrence with
                                {
                                    SourceSegmentIndex =
                                        0,

                                    EpisodeIndex =
                                        index
                                })
                        .ToArray());

        if (!confidence.CanEvaluate)
        {
            throw new InvalidOperationException(
                $"A memória Candidate {behavior.Codigo} contém ocorrências que já não formam uma identidade internamente consistente.");
        }
    }

    private static string CreateProvisionalCode()
    {
        return
            $"CAND-{Guid.NewGuid():N}"
                .ToUpperInvariant();
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

    private sealed record CandidateMatch(
        TagComportamentoIa Behavior,
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> PersistedCharacteristics);

    private static DateTime? GetFirstOccurrenceStart(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences)
    {
        return occurrences.Count == 0
            ? null
            : occurrences.Min(x =>
                x.Inicio);
    }

    private static DateTime? GetLastOccurrenceEnd(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences)
    {
        return occurrences.Count == 0
            ? null
            : occurrences.Max(x =>
                x.Fim);
    }

    private sealed record CandidateEvidence(
        BehaviorIdentityConfidenceResult Confidence,
        IReadOnlyDictionary<OccurrenceInterval, double> ConfidenceByInterval,
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> UniqueOccurrences);

    private readonly record struct OccurrenceInterval(
        DateTime Start,
        DateTime End);

    private sealed record CompleteLinkEvaluation(
        bool IsCompatible,
        int PairCount,
        string Reason);
}
