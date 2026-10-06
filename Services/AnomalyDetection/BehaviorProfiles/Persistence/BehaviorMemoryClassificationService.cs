using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

/// <summary>
/// Altera explicitamente a classificação semântica de um comportamento
/// já conhecido.
///
/// Responsabilidade deliberadamente restrita:
/// - NÃO promove Candidate para Known;
/// - NÃO altera identidade BEH-xxx;
/// - NÃO altera ocorrências;
/// - NÃO cria ou altera snapshots;
/// - NÃO altera confiança;
/// - NÃO altera origem.
///
/// A classificação semântica é uma decisão explícita separada do
/// aprendizado automático da identidade comportamental.
/// </summary>
public sealed class BehaviorMemoryClassificationService
{
    private readonly AppDbContext _context;

    public BehaviorMemoryClassificationService(
        AppDbContext context)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));
    }

    public async Task<BehaviorMemoryClassificationResult>
        ClassifyAsync(
            string clienteId,
            string tagName,
            string behaviorCode,
            string classification,
            CancellationToken cancellationToken = default)
    {
        clienteId =
            RequireValue(
                clienteId,
                nameof(clienteId));

        tagName =
            RequireValue(
                tagName,
                nameof(tagName));

        behaviorCode =
            RequireValue(
                behaviorCode,
                nameof(behaviorCode));

        var normalizedClassification =
            NormalizeClassification(
                classification);

        var behavior =
            await _context
                .TagsComportamentosIa
                .SingleOrDefaultAsync(
                    x =>
                        x.ClienteId == clienteId &&
                        x.TagName == tagName &&
                        x.Codigo == behaviorCode,
                    cancellationToken);

        if (behavior is null)
        {
            throw new KeyNotFoundException(
                $"Comportamento '{behaviorCode}' não encontrado para " +
                $"Cliente='{clienteId}' e Tag='{tagName}'.");
        }

        if (!string.Equals(
                behavior.Status,
                TagComportamentoIa.StatusKnown,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Somente comportamentos com Status='{TagComportamentoIa.StatusKnown}' " +
                $"podem receber classificação semântica. " +
                $"O comportamento '{behavior.Codigo}' está com Status='{behavior.Status}'.");
        }

        var previousClassification =
            behavior.Classificacao;

        var changed =
            !string.Equals(
                previousClassification,
                normalizedClassification,
                StringComparison.Ordinal);

        if (changed)
        {
            behavior.Classificacao =
                normalizedClassification;

            behavior.DataAtualizacao =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);
        }

        return new BehaviorMemoryClassificationResult
        {
            BehaviorId =
                behavior.Id,

            ClienteId =
                behavior.ClienteId,

            TagName =
                behavior.TagName,

            BehaviorCode =
                behavior.Codigo,

            Status =
                behavior.Status,

            PreviousClassification =
                previousClassification,

            Classification =
                behavior.Classificacao,

            Changed =
                changed,

            UpdatedAtUtc =
                behavior.DataAtualizacao
        };
    }

    private static string RequireValue(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                $"{parameterName} é obrigatório.",
                parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeClassification(
        string? classification)
    {
        if (string.IsNullOrWhiteSpace(
                classification))
        {
            throw new ArgumentException(
                "Classification é obrigatória.",
                nameof(classification));
        }

        var value =
            classification.Trim();

        if (string.Equals(
                value,
                TagComportamentoIa.ClassificacaoNormal,
                StringComparison.OrdinalIgnoreCase))
        {
            return TagComportamentoIa.ClassificacaoNormal;
        }

        if (string.Equals(
                value,
                TagComportamentoIa.ClassificacaoKnownAbnormal,
                StringComparison.OrdinalIgnoreCase))
        {
            return TagComportamentoIa.ClassificacaoKnownAbnormal;
        }

        if (string.Equals(
                value,
                TagComportamentoIa.ClassificacaoUndefined,
                StringComparison.OrdinalIgnoreCase))
        {
            return TagComportamentoIa.ClassificacaoUndefined;
        }

        throw new ArgumentException(
            $"Classificação '{classification}' inválida. " +
            $"Valores aceitos: " +
            $"'{TagComportamentoIa.ClassificacaoNormal}', " +
            $"'{TagComportamentoIa.ClassificacaoKnownAbnormal}' ou " +
            $"'{TagComportamentoIa.ClassificacaoUndefined}'.",
            nameof(classification));
    }
}

public sealed record BehaviorMemoryClassificationResult
{
    public int BehaviorId { get; init; }

    public string ClienteId { get; init; } = "";

    public string TagName { get; init; } = "";

    public string BehaviorCode { get; init; } = "";

    public string Status { get; init; } = "";

    public string PreviousClassification { get; init; } = "";

    public string Classification { get; init; } = "";

    public bool Changed { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
