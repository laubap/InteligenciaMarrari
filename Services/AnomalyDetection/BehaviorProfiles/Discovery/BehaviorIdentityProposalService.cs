namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Configuração da proposta de identidade comportamental.
///
/// O escopo da sequência é definido pelo chamador.
/// Na persistência futura, o escopo esperado será cliente + tag.
/// </summary>
public sealed record BehaviorIdentityProposalOptions
{
    public string Prefix { get; init; } = "BEH";

    public int NumericPadding { get; init; } = 3;

    public int StartingSequence { get; init; } = 1;
}

/// <summary>
/// Proposta de identidade para um candidato avaliado pelo lifecycle.
///
/// ProposedIdentity permanece null enquanto o lifecycle não for Known.
/// </summary>
public sealed record BehaviorIdentityProposal
{
    public int GroupIndex { get; init; }

    public BehaviorMemoryLifecycle Lifecycle { get; init; }

    public bool HasStableIdentityProposal { get; init; }

    public string? ProposedIdentity { get; init; }

    public BehaviorMemoryLifecycleEvaluation Evaluation { get; init; } =
        new();

    public string Reason { get; init; } = "";
}

/// <summary>
/// Gera propostas controladas BEH-xxx para candidatos elegíveis.
///
/// Esta etapa não persiste nem garante estabilidade entre execuções por si só.
/// A estabilidade definitiva virá da associação da identidade proposta
/// ao registro persistido de comportamento.
///
/// Regras:
/// - somente Known + EligibleForStableIdentity recebe código;
/// - ordem determinística pela primeira ocorrência e GroupIndex;
/// - códigos já reservados nunca são reutilizados;
/// - a sequência continua após o maior código já existente no mesmo escopo.
/// </summary>
public sealed class BehaviorIdentityProposalService
{
    private readonly BehaviorIdentityProposalOptions _options;

    public BehaviorIdentityProposalService(
        BehaviorIdentityProposalOptions? options = null)
    {
        _options =
            options ??
            new BehaviorIdentityProposalOptions();

        ValidateOptions(
            _options);
    }

    public IReadOnlyList<BehaviorIdentityProposal> Create(
        IReadOnlyList<BehaviorMemoryLifecycleEvaluation> evaluations,
        IReadOnlyCollection<string>? reservedIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(
            evaluations);

        var reserved =
            reservedIdentities ??
            Array.Empty<string>();

        var nextSequence =
            DetermineNextSequence(
                reserved);

        var ordered =
            evaluations
                .Where(evaluation =>
                    evaluation is not null)
                .OrderBy(evaluation =>
                    evaluation.Candidate.FirstOccurrenceStart)
                .ThenBy(evaluation =>
                    evaluation.GroupIndex)
                .ToArray();

        var proposals =
            new List<BehaviorIdentityProposal>(
                ordered.Length);

        foreach (var evaluation in ordered)
        {
            var eligible =
                evaluation.Lifecycle ==
                    BehaviorMemoryLifecycle.Known &&
                evaluation.EligibleForStableIdentity;

            if (!eligible)
            {
                proposals.Add(
                    new BehaviorIdentityProposal
                    {
                        GroupIndex =
                            evaluation.GroupIndex,

                        Lifecycle =
                            evaluation.Lifecycle,

                        HasStableIdentityProposal =
                            false,

                        ProposedIdentity =
                            null,

                        Evaluation =
                            evaluation,

                        Reason =
                            "Lifecycle ainda não é Known com identidade estável elegível."
                    });

                continue;
            }

            var identity =
                FormatIdentity(
                    nextSequence);

            nextSequence++;

            proposals.Add(
                new BehaviorIdentityProposal
                {
                    GroupIndex =
                        evaluation.GroupIndex,

                    Lifecycle =
                        evaluation.Lifecycle,

                    HasStableIdentityProposal =
                        true,

                    ProposedIdentity =
                        identity,

                    Evaluation =
                        evaluation,

                    Reason =
                        $"Identidade comportamental proposta: {identity}."
                });
        }

        return proposals;
    }

    private int DetermineNextSequence(
        IReadOnlyCollection<string> reservedIdentities)
    {
        var maximum =
            _options.StartingSequence - 1;

        foreach (var identity in reservedIdentities)
        {
            if (!TryParseSequence(
                    identity,
                    out var sequence))
            {
                continue;
            }

            maximum =
                Math.Max(
                    maximum,
                    sequence);
        }

        return
            checked(maximum + 1);
    }

    private bool TryParseSequence(
        string? identity,
        out int sequence)
    {
        sequence =
            0;

        if (string.IsNullOrWhiteSpace(
                identity))
        {
            return false;
        }

        var prefix =
            _options.Prefix + "-";

        if (!identity.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numericPart =
            identity[prefix.Length..];

        return
            int.TryParse(
                numericPart,
                out sequence) &&
            sequence >=
                _options.StartingSequence;
    }

    private string FormatIdentity(
        int sequence)
    {
        return
            $"{_options.Prefix}-" +
            sequence.ToString(
                $"D{_options.NumericPadding}");
    }

    private static void ValidateOptions(
        BehaviorIdentityProposalOptions options)
    {
        if (string.IsNullOrWhiteSpace(
                options.Prefix))
        {
            throw new ArgumentException(
                "Prefix é obrigatório.",
                nameof(options));
        }

        if (options.Prefix.Contains(
                '-'))
        {
            throw new ArgumentException(
                "Prefix não deve conter '-'.",
                nameof(options));
        }

        if (options.NumericPadding < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "NumericPadding deve ser maior ou igual a 1.");
        }

        if (options.StartingSequence < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "StartingSequence deve ser maior ou igual a 1.");
        }
    }
}
