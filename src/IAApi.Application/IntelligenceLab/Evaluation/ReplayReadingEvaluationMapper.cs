namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Medicao observada em um replay isolado. Nao contem rotulo de verdade-terreno.
/// </summary>
public sealed record ReplayObservedPrediction(
    DateTimeOffset TimestampUtc,
    bool FinalAlert,
    double? EnsembleNormalizedScore,
    Guid DiagnosticId,
    string DecisionSource);

/// <summary>
/// Converte apenas o que a IA efetivamente decidiu para o contrato de leitura
/// do avaliador 3A. Nao cria eventos, normalidade ou metricas de eficacia.
/// </summary>
public static class ReplayReadingEvaluationMapper
{
    public static IReadOnlyList<AiReadingEvaluationResult> Map(
        string runId,
        string scenarioId,
        string testClientId,
        string tagName,
        IReadOnlyList<ReplayObservedPrediction> predictions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(testClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        ArgumentNullException.ThrowIfNull(predictions);

        if (!testClientId.StartsWith("SIM-", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O replay para avaliacao deve usar um cliente sintético SIM-.",
                nameof(testClientId));

        var result = new AiReadingEvaluationResult[predictions.Count];
        DateTimeOffset? previous = null;
        var diagnosticIds = new HashSet<Guid>();
        for (var i = 0; i < predictions.Count; i++)
        {
            var prediction = predictions[i] ??
                throw new ArgumentException("Previsao nula no replay.", nameof(predictions));
            if (prediction.TimestampUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("As leituras exportadas devem estar em UTC.", nameof(predictions));
            if (previous is not null && prediction.TimestampUtc <= previous.Value)
                throw new ArgumentException("As leituras devem ter horarios estritamente crescentes.",
                    nameof(predictions));
            if (prediction.DiagnosticId == Guid.Empty)
                throw new ArgumentException("Toda leitura do replay precisa ter DiagnosticId.",
                    nameof(predictions));
            if (!diagnosticIds.Add(prediction.DiagnosticId))
                throw new ArgumentException("DiagnosticId duplicado no replay.",
                    nameof(predictions));
            if (string.IsNullOrWhiteSpace(prediction.DecisionSource))
                throw new ArgumentException("Fonte da decisao nao pode ser vazia.", nameof(predictions));
            if (prediction.EnsembleNormalizedScore is double score &&
                (!double.IsFinite(score) || score < 0 || score > 100))
                throw new ArgumentException("Score normalizado invalido.", nameof(predictions));

            result[i] = new AiReadingEvaluationResult(
                IndustrialEvaluationSchemas.ReadingV1,
                runId,
                scenarioId,
                testClientId,
                $"{runId}:{tagName}:{prediction.TimestampUtc:O}",
                tagName,
                prediction.TimestampUtc,
                prediction.EnsembleNormalizedScore,
                prediction.FinalAlert,
                prediction.DiagnosticId,
                prediction.DecisionSource);
            previous = prediction.TimestampUtc;
        }
        return Array.AsReadOnly(result);
    }
}
