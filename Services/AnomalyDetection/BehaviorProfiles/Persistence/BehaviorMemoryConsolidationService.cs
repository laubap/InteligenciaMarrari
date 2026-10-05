using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using Microsoft.EntityFrameworkCore.Storage;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public enum BehaviorMemoryConsolidationOutcome
{
    CandidatePending = 0,
    IdentityConfidenceUnavailable = 1,
    Ambiguous = 2,
    ExistingBehaviorCollision = 3,
    PersistedExistingBehavior = 4,
    PersistedNewBehavior = 5,
    PersistedCandidate = 6,
    PromotedCandidateToKnown = 7
}

/// <summary>
/// Resultado de consolidação de um grupo comportamental descoberto.
/// </summary>
public sealed record BehaviorMemoryConsolidationItemResult
{
    public int GroupIndex { get; init; }

    public BehaviorMemoryLifecycle Lifecycle { get; init; }

    public BehaviorMemoryConsolidationOutcome Outcome { get; init; }

    public string? BehaviorCode { get; init; }

    public int? BehaviorId { get; init; }

    public BehaviorIdentityConfidenceResult? IdentityConfidence { get; init; }

    public BehaviorMemoryReconciliationResult? Reconciliation { get; init; }

    public BehaviorCandidateMemoryResult? CandidateMemory { get; init; }

    public BehaviorCandidatePromotionResult? Promotion { get; init; }

    public BehaviorMemoryPersistenceResult? Persistence { get; init; }

    public string Reason { get; init; } = "";
}

public sealed record BehaviorMemoryConsolidationResult
{
    public IReadOnlyList<BehaviorMemoryConsolidationItemResult> Items { get; init; } =
        Array.Empty<BehaviorMemoryConsolidationItemResult>();

    public int CandidatePendingCount =>
        Items.Count(x =>
            x.Outcome ==
            BehaviorMemoryConsolidationOutcome.CandidatePending);

    public int PersistedExistingCount =>
        Items.Count(x =>
            x.Outcome ==
            BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior);

    public int PersistedNewCount =>
        Items.Count(x =>
            x.Outcome ==
            BehaviorMemoryConsolidationOutcome.PersistedNewBehavior);

    public int PersistedCandidateCount =>
        Items.Count(x =>
            x.Outcome ==
            BehaviorMemoryConsolidationOutcome.PersistedCandidate);

    public int PromotedCandidateCount =>
        Items.Count(x =>
            x.Outcome ==
            BehaviorMemoryConsolidationOutcome.PromotedCandidateToKnown);

    public int BlockedCount =>
        Items.Count -
        CandidatePendingCount -
        PersistedExistingCount -
        PersistedNewCount -
        PersistedCandidateCount -
        PromotedCandidateCount;
}

/// <summary>
/// Coordena a consolidação da memória comportamental já descoberta.
///
/// Ordem arquitetural:
///
/// 1. lifecycle é avaliado para sabermos a maturidade da descoberta;
/// 2. TODA descoberta caracterizável é reconciliada com memórias Known;
/// 3. se existe BEH compatível, a evidência pode reforçá-lo mesmo quando
///    a descoberta isolada ainda seria Candidate;
/// 4. se não existe match Known e a descoberta atual ainda é imatura,
///    ela é reconciliada/persistida como memória provisória CAND;
/// 5. após persistir/reutilizar CAND, o lifecycle é reavaliado com a evidência
///    cumulativa entre execuções;
/// 6. CAND cumulativamente maduro é promovido na mesma linha para BEH/Known;
/// 7. descobertas maduras sem memória CAND recebem nova identidade BEH;
/// 8. identidade, plano e persistência Known são aplicados depois.
///
/// Isso separa:
/// - "evidência suficiente para criar uma identidade nova";
/// - "evidência suficiente para reconhecer uma identidade já conhecida".
/// </summary>
public sealed class BehaviorMemoryConsolidationService
{
    private readonly AppDbContext _context;

    private readonly BehaviorMemoryLifecyclePolicy _lifecyclePolicy;

    private readonly BehaviorIdentityConfidenceCalculator _confidenceCalculator;

    private readonly BehaviorMemoryReconciliationService _reconciliationService;

    private readonly BehaviorCandidateMemoryService _candidateMemoryService;

    private readonly BehaviorIdentityProposalService _identityProposalService;

    private readonly BehaviorMemoryPersistencePlanBuilder _planBuilder;

    private readonly BehaviorMemoryPersistenceService _persistenceService;

    private readonly BehaviorMemorySnapshotRebuildService _snapshotRebuildService;

    public BehaviorMemoryConsolidationService(
        AppDbContext context,
        BehaviorMemoryLifecyclePolicy lifecyclePolicy,
        BehaviorIdentityConfidenceCalculator confidenceCalculator,
        BehaviorMemoryReconciliationService reconciliationService,
        BehaviorCandidateMemoryService candidateMemoryService,
        BehaviorIdentityProposalService identityProposalService,
        BehaviorMemoryPersistencePlanBuilder planBuilder,
        BehaviorMemoryPersistenceService persistenceService,
        BehaviorMemorySnapshotRebuildService snapshotRebuildService)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));

        _lifecyclePolicy =
            lifecyclePolicy ??
            throw new ArgumentNullException(
                nameof(lifecyclePolicy));

        _confidenceCalculator =
            confidenceCalculator ??
            throw new ArgumentNullException(
                nameof(confidenceCalculator));

        _reconciliationService =
            reconciliationService ??
            throw new ArgumentNullException(
                nameof(reconciliationService));

        _candidateMemoryService =
            candidateMemoryService ??
            throw new ArgumentNullException(
                nameof(candidateMemoryService));

        _identityProposalService =
            identityProposalService ??
            throw new ArgumentNullException(
                nameof(identityProposalService));

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
    }

    public async Task<BehaviorMemoryConsolidationResult> ConsolidateAsync(
        string clienteId,
        string tagName,
        IReadOnlyList<BehaviorDiscoveryCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ValidateText(
            clienteId,
            nameof(clienteId));

        ValidateText(
            tagName,
            nameof(tagName));

        ArgumentNullException.ThrowIfNull(
            candidates);

        var ordered =
            candidates
                .Where(x =>
                    x is not null)
                .OrderBy(x =>
                    x.FirstOccurrenceStart)
                .ThenBy(x =>
                    x.GroupIndex)
                .ToArray();

        EnsureUniqueGroupIndices(
            ordered);

        var finalResults =
            new Dictionary<int, BehaviorMemoryConsolidationItemResult>();

        var workItems =
            new List<WorkItem>();

        /*
         * Fase 1:
         * lifecycle + reconciliação.
         *
         * Matching com memória Known vem antes da exigência de maturidade
         * para criação de um comportamento novo.
         */
        foreach (var candidate in ordered)
        {
            var lifecycle =
                _lifecyclePolicy.Evaluate(
                    candidate);

            var reconciliation =
                await _reconciliationService
                    .ReconcileAsync(
                        clienteId,
                        tagName,
                        candidate,
                        cancellationToken);

            if (reconciliation.Outcome ==
                BehaviorMemoryReconciliationOutcome.Ambiguous)
            {
                finalResults[
                    candidate.GroupIndex] =
                    new BehaviorMemoryConsolidationItemResult
                    {
                        GroupIndex =
                            candidate.GroupIndex,

                        Lifecycle =
                            lifecycle.Lifecycle,

                        Outcome =
                            BehaviorMemoryConsolidationOutcome.Ambiguous,

                        Reconciliation =
                            reconciliation,

                        Reason =
                            reconciliation.Reason
                    };

                continue;
            }

            if (reconciliation.Outcome ==
                BehaviorMemoryReconciliationOutcome.ExistingBehavior)
            {
                var confidence =
                    reconciliation.MatchedIdentityConfidence;

                if (confidence is null ||
                    !confidence.CanEvaluate)
                {
                    finalResults[
                        candidate.GroupIndex] =
                        new BehaviorMemoryConsolidationItemResult
                        {
                            GroupIndex =
                                candidate.GroupIndex,

                            Lifecycle =
                                lifecycle.Lifecycle,

                            Outcome =
                                BehaviorMemoryConsolidationOutcome.IdentityConfidenceUnavailable,

                            IdentityConfidence =
                                confidence,

                            Reconciliation =
                                reconciliation,

                            Reason =
                                "O comportamento existente foi reconhecido, mas não foi possível calcular confiança válida de pertencimento."
                        };

                    continue;
                }

                workItems.Add(
                    new WorkItem(
                        lifecycle,
                        confidence,
                        reconciliation));

                continue;
            }

            /*
             * Nenhum BEH existente correspondeu.
             * Agora, e somente agora, aplicamos a maturidade necessária
             * para permitir nascimento de uma identidade nova.
             */
            if (lifecycle.Lifecycle !=
                    BehaviorMemoryLifecycle.Known ||
                !lifecycle.EligibleForStableIdentity)
            {
                finalResults[
                    candidate.GroupIndex] =
                    await ConsolidateCandidateAtomicallyAsync(
                        clienteId,
                        tagName,
                        candidate,
                        lifecycle,
                        reconciliation,
                        cancellationToken);

                continue;
            }

            var newIdentityConfidence =
                _confidenceCalculator.Calculate(
                    candidate.Occurrences);

            if (!newIdentityConfidence.CanEvaluate)
            {
                finalResults[
                    candidate.GroupIndex] =
                    new BehaviorMemoryConsolidationItemResult
                    {
                        GroupIndex =
                            candidate.GroupIndex,

                        Lifecycle =
                            lifecycle.Lifecycle,

                        Outcome =
                            BehaviorMemoryConsolidationOutcome.IdentityConfidenceUnavailable,

                        IdentityConfidence =
                            newIdentityConfidence,

                        Reconciliation =
                            reconciliation,

                        Reason =
                            newIdentityConfidence.Reason
                    };

                continue;
            }

            workItems.Add(
                new WorkItem(
                    lifecycle,
                    newIdentityConfidence,
                    reconciliation));
        }

        /*
         * Proteção contra fusão silenciosa:
         *
         * se dois grupos diferentes desta mesma descoberta apontarem para
         * o mesmo BEH persistido, nenhuma fusão é aplicada automaticamente.
         */
        var existingCollisions =
            workItems
                .Where(x =>
                    x.Reconciliation.Outcome ==
                        BehaviorMemoryReconciliationOutcome.ExistingBehavior &&
                    x.Reconciliation.MatchedBehaviorId.HasValue)
                .GroupBy(x =>
                    x.Reconciliation.MatchedBehaviorId!.Value)
                .Where(group =>
                    group.Count() > 1)
                .SelectMany(group =>
                    group)
                .Select(x =>
                    x.Lifecycle.GroupIndex)
                .ToHashSet();

        foreach (var work in workItems)
        {
            if (!existingCollisions.Contains(
                    work.Lifecycle.GroupIndex))
            {
                continue;
            }

            finalResults[
                work.Lifecycle.GroupIndex] =
                new BehaviorMemoryConsolidationItemResult
                {
                    GroupIndex =
                        work.Lifecycle.GroupIndex,

                    Lifecycle =
                        work.Lifecycle.Lifecycle,

                    Outcome =
                        BehaviorMemoryConsolidationOutcome.ExistingBehaviorCollision,

                    IdentityConfidence =
                        work.Confidence,

                    Reconciliation =
                        work.Reconciliation,

                    Reason =
                        "Dois ou mais grupos distintos desta descoberta apontaram para o mesmo comportamento persistido; nenhuma fusão foi aplicada automaticamente."
                };
        }

        /*
         * Propostas de identidade somente para NewBehavior maduros
         * e não bloqueados.
         */
        var newBehaviorWork =
            workItems
                .Where(work =>
                    !existingCollisions.Contains(
                        work.Lifecycle.GroupIndex) &&
                    work.Reconciliation.Outcome ==
                        BehaviorMemoryReconciliationOutcome.NewBehavior)
                .ToArray();

        /*
         * Consideramos códigos de todas as reconciliações do lote,
         * inclusive candidatos ainda imaturos, para nunca reciclar identidade.
         */
        var allReservedIdentities =
            ordered
                .Select(candidate =>
                    finalResults.TryGetValue(
                        candidate.GroupIndex,
                        out var completed)
                        ? completed.Reconciliation?.ReservedIdentities
                        : workItems
                            .FirstOrDefault(x =>
                                x.Lifecycle.GroupIndex ==
                                candidate.GroupIndex)
                            ?.Reconciliation.ReservedIdentities)
                .Where(x =>
                    x is not null)
                .SelectMany(x =>
                    x!)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var newProposals =
            _identityProposalService
                .Create(
                    newBehaviorWork
                        .Select(x =>
                            x.Lifecycle)
                        .ToArray(),
                    allReservedIdentities)
                .ToDictionary(
                    x =>
                        x.GroupIndex);

        /*
         * Persistência por comportamento.
         * O serviço de persistência mantém atomicidade individual
         * e idempotência.
         */
        foreach (var work in workItems)
        {
            var groupIndex =
                work.Lifecycle.GroupIndex;

            if (finalResults.ContainsKey(
                    groupIndex))
            {
                continue;
            }

            BehaviorIdentityProposal proposal;

            BehaviorMemoryConsolidationOutcome successfulOutcome;

            if (work.Reconciliation.Outcome ==
                BehaviorMemoryReconciliationOutcome.ExistingBehavior)
            {
                if (string.IsNullOrWhiteSpace(
                        work.Reconciliation.MatchedBehaviorCode))
                {
                    throw new InvalidOperationException(
                        "Reconciliação ExistingBehavior retornou sem código de comportamento.");
                }

                proposal =
                    CreateExistingIdentityProposal(
                        work.Lifecycle,
                        work.Reconciliation.MatchedBehaviorCode);

                successfulOutcome =
                    BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior;
            }
            else
            {
                if (!newProposals.TryGetValue(
                        groupIndex,
                        out proposal!))
                {
                    throw new InvalidOperationException(
                        "Não foi possível localizar a proposta de identidade do novo comportamento.");
                }

                successfulOutcome =
                    BehaviorMemoryConsolidationOutcome.PersistedNewBehavior;
            }

            var plan =
                _planBuilder.Build(
                    clienteId,
                    tagName,
                    proposal,
                    work.Confidence);

            /*
             * Para um BEH já existente, o snapshot do novo lote isolado
             * não representa a memória completa.
             *
             * Reconstruímos usando:
             * - todas as ocorrências persistidas habilitadas para aprendizado;
             * - as ocorrências novas ainda não persistidas.
             *
             * O historiador continua sendo a fonte canônica dos dados brutos.
             */
            if (work.Reconciliation.Outcome ==
                BehaviorMemoryReconciliationOutcome.ExistingBehavior)
            {
                if (!work.Reconciliation.MatchedBehaviorId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Reconciliação ExistingBehavior retornou sem Id persistido.");
                }

                var consolidatedSnapshot =
                    await _snapshotRebuildService
                        .RebuildAsync(
                            work.Reconciliation.MatchedBehaviorId.Value,
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
            }

            var persistence =
                await _persistenceService
                    .PersistAsync(
                        plan,
                        cancellationToken);

            finalResults[
                groupIndex] =
                new BehaviorMemoryConsolidationItemResult
                {
                    GroupIndex =
                        groupIndex,

                    Lifecycle =
                        work.Lifecycle.Lifecycle,

                    Outcome =
                        successfulOutcome,

                    BehaviorCode =
                        proposal.ProposedIdentity,

                    BehaviorId =
                        persistence.BehaviorId,

                    IdentityConfidence =
                        work.Confidence,

                    Reconciliation =
                        work.Reconciliation,

                    Persistence =
                        persistence,

                    Reason =
                        successfulOutcome ==
                            BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior
                            ? $"Memória reconciliada e persistida no comportamento existente {proposal.ProposedIdentity}."
                            : $"Novo comportamento {proposal.ProposedIdentity} consolidado e persistido."
                };
        }

        return new BehaviorMemoryConsolidationResult
        {
            Items =
                ordered
                    .Select(candidate =>
                        finalResults[
                            candidate.GroupIndex])
                    .ToArray()
        };
    }

    /// <summary>
    /// Consolida a memória Candidate e, quando ela atinge maturidade cumulativa,
    /// promove a mesma linha para Known dentro da MESMA transação.
    ///
    /// Invariante:
    /// - se a promoção falhar, nenhuma nova ocorrência/snapshot Candidate
    ///   daquela tentativa pode permanecer persistida.
    ///
    /// Os serviços internos detectam a transação já existente e apenas se
    /// juntam a ela; commit/rollback pertencem a esta unidade de trabalho.
    /// </summary>
    private async Task<BehaviorMemoryConsolidationItemResult>
        ConsolidateCandidateAtomicallyAsync(
            string clienteId,
            string tagName,
            BehaviorDiscoveryCandidate candidate,
            BehaviorMemoryLifecycleEvaluation lifecycle,
            BehaviorMemoryReconciliationResult reconciliation,
            CancellationToken cancellationToken)
    {
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
            var candidateMemory =
                await _candidateMemoryService
                    .ConsolidateAsync(
                        clienteId,
                        tagName,
                        candidate,
                        cancellationToken);

            BehaviorMemoryConsolidationItemResult result;

            if (candidateMemory.Outcome ==
                BehaviorCandidateMemoryOutcome.Ambiguous)
            {
                result =
                    new BehaviorMemoryConsolidationItemResult
                    {
                        GroupIndex =
                            candidate.GroupIndex,

                        Lifecycle =
                            lifecycle.Lifecycle,

                        Outcome =
                            BehaviorMemoryConsolidationOutcome.Ambiguous,

                        Reconciliation =
                            reconciliation,

                        CandidateMemory =
                            candidateMemory,

                        Reason =
                            candidateMemory.Reason
                    };
            }
            else
            {
                var cumulativeLifecycle =
                    _lifecyclePolicy
                        .EvaluateCumulative(
                            candidate,
                            candidateMemory.CumulativeOccurrenceCount,
                            candidateMemory.CumulativeFirstOccurrenceStart,
                            candidateMemory.CumulativeLastOccurrenceEnd,
                            candidateMemory.HasCumulativeRecurrenceEvidence);

                if (cumulativeLifecycle.Lifecycle ==
                        BehaviorMemoryLifecycle.Known &&
                    cumulativeLifecycle.EligibleForStableIdentity)
                {
                    if (!candidateMemory.BehaviorId.HasValue ||
                        string.IsNullOrWhiteSpace(
                            candidateMemory.BehaviorCode))
                    {
                        throw new InvalidOperationException(
                            "Memória Candidate cumulativamente madura foi persistida sem Id/código.");
                    }

                    if (candidateMemory.IdentityConfidence is null ||
                        !candidateMemory.IdentityConfidence.CanEvaluate)
                    {
                        result =
                            new BehaviorMemoryConsolidationItemResult
                            {
                                GroupIndex =
                                    candidate.GroupIndex,

                                Lifecycle =
                                    cumulativeLifecycle.Lifecycle,

                                Outcome =
                                    BehaviorMemoryConsolidationOutcome.IdentityConfidenceUnavailable,

                                BehaviorCode =
                                    candidateMemory.BehaviorCode,

                                BehaviorId =
                                    candidateMemory.BehaviorId,

                                IdentityConfidence =
                                    candidateMemory.IdentityConfidence,

                                Reconciliation =
                                    reconciliation,

                                CandidateMemory =
                                    candidateMemory,

                                Persistence =
                                    candidateMemory.Persistence,

                                Reason =
                                    "A memória Candidate atingiu maturidade cumulativa para Known, mas a confiança de identidade ainda não pôde ser avaliada."
                            };
                    }
                    else
                    {
                        var promotion =
                            await _candidateMemoryService
                                .PromoteToNextAvailableKnownAsync(
                                    candidateMemory.BehaviorId.Value,
                                    clienteId,
                                    tagName,
                                    cumulativeLifecycle,
                                    candidateMemory.IdentityConfidence,
                                    reconciliation.ReservedIdentities,
                                    cancellationToken);

                        result =
                            new BehaviorMemoryConsolidationItemResult
                            {
                                GroupIndex =
                                    candidate.GroupIndex,

                                Lifecycle =
                                    BehaviorMemoryLifecycle.Known,

                                Outcome =
                                    BehaviorMemoryConsolidationOutcome.PromotedCandidateToKnown,

                                BehaviorCode =
                                    promotion.BehaviorCode,

                                BehaviorId =
                                    promotion.BehaviorId,

                                IdentityConfidence =
                                    candidateMemory.IdentityConfidence,

                                Reconciliation =
                                    reconciliation,

                                CandidateMemory =
                                    candidateMemory,

                                Promotion =
                                    promotion,

                                Persistence =
                                    candidateMemory.Persistence,

                                Reason =
                                    promotion.Reason
                            };
                    }
                }
                else
                {
                    result =
                        new BehaviorMemoryConsolidationItemResult
                        {
                            GroupIndex =
                                candidate.GroupIndex,

                            Lifecycle =
                                cumulativeLifecycle.Lifecycle,

                            Outcome =
                                BehaviorMemoryConsolidationOutcome.PersistedCandidate,

                            BehaviorCode =
                                candidateMemory.BehaviorCode,

                            BehaviorId =
                                candidateMemory.BehaviorId,

                            IdentityConfidence =
                                candidateMemory.IdentityConfidence,

                            Reconciliation =
                                reconciliation,

                            CandidateMemory =
                                candidateMemory,

                            Persistence =
                                candidateMemory.Persistence,

                            Reason =
                                candidateMemory.Reason
                        };
                }
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(
                    cancellationToken);
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                /*
                 * SaveChanges pode deixar entidades em estados Modified/Added
                 * mesmo depois do rollback do banco. Não permitimos que uma
                 * tentativa falha contamine a próxima operação no mesmo scope.
                 */
                _context.ChangeTracker.Clear();
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

    private static BehaviorIdentityProposal CreateExistingIdentityProposal(
        BehaviorMemoryLifecycleEvaluation lifecycle,
        string behaviorCode)
    {
        return new BehaviorIdentityProposal
        {
            GroupIndex =
                lifecycle.GroupIndex,

            /*
             * A identidade persistida já é Known.
             * A evidência nova pode ainda ter lifecycle Candidate.
             */
            Lifecycle =
                BehaviorMemoryLifecycle.Known,

            HasStableIdentityProposal =
                true,

            ProposedIdentity =
                behaviorCode,

            Evaluation =
                lifecycle,

            Reason =
                $"Identidade persistida reutilizada após reconciliação: {behaviorCode}."
        };
    }

    private static void EnsureUniqueGroupIndices(
        IReadOnlyList<BehaviorDiscoveryCandidate> candidates)
    {
        var duplicate =
            candidates
                .GroupBy(x =>
                    x.GroupIndex)
                .FirstOrDefault(group =>
                    group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"GroupIndex duplicado no lote de consolidação: {duplicate.Key}.");
        }
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

    private sealed record WorkItem(
        BehaviorMemoryLifecycleEvaluation Lifecycle,
        BehaviorIdentityConfidenceResult Confidence,
        BehaviorMemoryReconciliationResult Reconciliation);
}
