namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Lifecycle da memória comportamental descoberta.
///
/// Candidate:
/// padrão observado, ainda sem evidência suficiente para identidade estável.
///
/// Known:
/// padrão recorrente com evidência suficiente para receber identidade estável.
///
/// Deprecated:
/// reservado para etapa posterior de manutenção da memória persistida.
/// Esta política inicial não deprecia comportamentos.
/// </summary>
public enum BehaviorMemoryLifecycle
{
    Candidate = 0,
    Known = 1,
    Deprecated = 2
}

public sealed record BehaviorMemoryLifecycleOptions
{
    /// <summary>
    /// Quantidade mínima de ocorrências semelhantes para um padrão
    /// deixar de ser somente candidato.
    /// </summary>
    public int MinimumOccurrencesForKnown { get; init; } = 3;

    /// <summary>
    /// Horizonte temporal mínimo entre a primeira evidência e o fim
    /// da última ocorrência.
    ///
    /// Evita promover como Known várias repetições concentradas
    /// em um intervalo muito curto.
    /// </summary>
    public double MinimumEvidenceSpanMinutesForKnown { get; init; } = 60.0;
}

/// <summary>
/// Resultado puro da avaliação de maturidade da memória comportamental.
///
/// Não contém classificação Normal/KnownAbnormal.
/// Lifecycle e classificação são dimensões independentes.
/// </summary>
public sealed record BehaviorMemoryLifecycleEvaluation
{
    public int GroupIndex { get; init; }

    public BehaviorMemoryLifecycle Lifecycle { get; init; }

    public bool EligibleForStableIdentity { get; init; }

    public int OccurrenceCount { get; init; }

    public double EvidenceSpanMinutes { get; init; }

    public bool RecurrenceSatisfied { get; init; }

    public bool OccurrenceThresholdSatisfied { get; init; }

    public bool EvidenceSpanSatisfied { get; init; }

    public BehaviorDiscoveryCandidate Candidate { get; init; } =
        new();

    public string Reason { get; init; } = "";
}

/// <summary>
/// Decide se um BehaviorDiscoveryCandidate permanece Candidate
/// ou possui evidência suficiente para se tornar Known.
///
/// A política reutiliza o mesmo princípio de maturidade já utilizado
/// em outras partes do projeto: promoção depende de persistência de
/// evidência ao longo do tempo, e não apenas de uma observação isolada.
///
/// Esta etapa:
/// - não cria BEH-xxx;
/// - não classifica Normal/KnownAbnormal;
/// - não persiste dados;
/// - não deprecia comportamentos.
/// </summary>
public sealed class BehaviorMemoryLifecyclePolicy
{
    private readonly BehaviorMemoryLifecycleOptions _options;

    public BehaviorMemoryLifecyclePolicy(
        BehaviorMemoryLifecycleOptions? options = null)
    {
        _options =
            options ??
            new BehaviorMemoryLifecycleOptions();

        ValidateOptions(
            _options);
    }

    public BehaviorMemoryLifecycleEvaluation Evaluate(
        BehaviorDiscoveryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        return EvaluateCore(
            candidate,
            candidate.OccurrenceCount,
            candidate.FirstOccurrenceStart,
            candidate.LastOccurrenceEnd,
            candidate.IsRecurrent);
    }

    /// <summary>
    /// Reavalia o lifecycle usando a memória acumulada entre execuções.
    ///
    /// O BehaviorDiscoveryCandidate recebido continua representando o grupo
    /// da execução atual, mas quantidade, horizonte e recorrência vêm da
    /// evidência persistida + evidência nova já reconciliada.
    /// </summary>
    public BehaviorMemoryLifecycleEvaluation EvaluateCumulative(
        BehaviorDiscoveryCandidate candidate,
        int occurrenceCount,
        DateTime? firstOccurrenceStart,
        DateTime? lastOccurrenceEnd,
        bool recurrenceSatisfied)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        if (occurrenceCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurrenceCount));
        }

        return EvaluateCore(
            candidate,
            occurrenceCount,
            firstOccurrenceStart,
            lastOccurrenceEnd,
            recurrenceSatisfied);
    }

    private BehaviorMemoryLifecycleEvaluation EvaluateCore(
        BehaviorDiscoveryCandidate candidate,
        int occurrenceCount,
        DateTime? firstOccurrenceStart,
        DateTime? lastOccurrenceEnd,
        bool recurrenceSatisfied)
    {
        var evidenceSpanMinutes =
            CalculateEvidenceSpanMinutes(
                occurrenceCount,
                firstOccurrenceStart,
                lastOccurrenceEnd);

        var occurrenceThresholdSatisfied =
            occurrenceCount >=
            _options.MinimumOccurrencesForKnown;

        var evidenceSpanSatisfied =
            evidenceSpanMinutes >=
            _options.MinimumEvidenceSpanMinutesForKnown;

        var eligibleForStableIdentity =
            recurrenceSatisfied &&
            occurrenceThresholdSatisfied &&
            evidenceSpanSatisfied;

        return new BehaviorMemoryLifecycleEvaluation
        {
            GroupIndex =
                candidate.GroupIndex,

            Lifecycle =
                eligibleForStableIdentity
                    ? BehaviorMemoryLifecycle.Known
                    : BehaviorMemoryLifecycle.Candidate,

            EligibleForStableIdentity =
                eligibleForStableIdentity,

            OccurrenceCount =
                occurrenceCount,

            EvidenceSpanMinutes =
                evidenceSpanMinutes,

            RecurrenceSatisfied =
                recurrenceSatisfied,

            OccurrenceThresholdSatisfied =
                occurrenceThresholdSatisfied,

            EvidenceSpanSatisfied =
                evidenceSpanSatisfied,

            Candidate =
                candidate,

            Reason =
                BuildReason(
                    recurrenceSatisfied,
                    occurrenceThresholdSatisfied,
                    evidenceSpanSatisfied,
                    occurrenceCount,
                    evidenceSpanMinutes)
        };
    }

    private static double CalculateEvidenceSpanMinutes(
        int occurrenceCount,
        DateTime? firstOccurrenceStart,
        DateTime? lastOccurrenceEnd)
    {
        if (occurrenceCount <= 0 ||
            !firstOccurrenceStart.HasValue ||
            !lastOccurrenceEnd.HasValue)
        {
            return 0;
        }

        return Math.Max(
            0,
            (
                lastOccurrenceEnd.Value -
                firstOccurrenceStart.Value
            ).TotalMinutes);
    }

    private string BuildReason(
        bool recurrenceSatisfied,
        bool occurrenceThresholdSatisfied,
        bool evidenceSpanSatisfied,
        int occurrenceCount,
        double evidenceSpanMinutes)
    {
        if (recurrenceSatisfied &&
            occurrenceThresholdSatisfied &&
            evidenceSpanSatisfied)
        {
            return
                $"Padrão recorrente com {occurrenceCount} ocorrências " +
                $"distribuídas por {evidenceSpanMinutes:F1} minutos; " +
                "evidência suficiente para lifecycle Known.";
        }

        var pending =
            new List<string>();

        if (!recurrenceSatisfied)
        {
            pending.Add(
                "recorrência mínima ainda não confirmada");
        }

        if (!occurrenceThresholdSatisfied)
        {
            pending.Add(
                $"ocorrências {occurrenceCount}/" +
                $"{_options.MinimumOccurrencesForKnown}");
        }

        if (!evidenceSpanSatisfied)
        {
            pending.Add(
                $"horizonte temporal {evidenceSpanMinutes:F1}/" +
                $"{_options.MinimumEvidenceSpanMinutesForKnown:F1} min");
        }

        return
            "Lifecycle Candidate: " +
            string.Join(
                "; ",
                pending) +
            ".";
    }

    private static void ValidateOptions(
        BehaviorMemoryLifecycleOptions options)
    {
        if (options.MinimumOccurrencesForKnown < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MinimumOccurrencesForKnown deve ser maior ou igual a 2.");
        }

        if (!double.IsFinite(
                options.MinimumEvidenceSpanMinutesForKnown) ||
            options.MinimumEvidenceSpanMinutesForKnown < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MinimumEvidenceSpanMinutesForKnown deve ser finito e não negativo.");
        }
    }
}
