namespace IAApi.IntelligenceLab.Evaluation;

public sealed class IndustrialEvaluationValidationException(
    IReadOnlyList<string> errors) : ArgumentException(
        "Contrato de avaliação industrial inválido: " + string.Join(" | ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public static class IndustrialEvaluationValidator
{
    private const string SyntheticClientPrefix = "SIM-";

    public static void Validate(IndustrialEvaluationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var errors = new List<string>();
        Required(run.SchemaVersion, "run.schemaVersion", errors);
        if (!string.Equals(run.SchemaVersion, IndustrialEvaluationSchemas.RunV1, StringComparison.Ordinal))
        {
            errors.Add("run.schemaVersion não é suportado.");
        }

        Required(run.RunId, "run.runId", errors);
        Required(run.ScenarioId, "run.scenarioId", errors);
        ValidateSyntheticClient(run.TestClientId, "run.testClientId", errors);

        if (run.AlertEpisodeCadence <= TimeSpan.Zero)
        {
            errors.Add("run.alertEpisodeCadence deve ser positivo.");
        }

        if (run.MinimumEventCoverageReadings < 1)
        {
            errors.Add("run.minimumEventCoverageReadings deve ser pelo menos um.");
        }

        ValidateExecutionIdentity(run.ExecutionIdentity, errors);
        ValidateEvents(run, errors);
        ValidateNormalIntervals(run, errors);
        ValidateReadings(run, errors);

        if (errors.Count > 0)
        {
            throw new IndustrialEvaluationValidationException(errors);
        }
    }

    private static void ValidateExecutionIdentity(
        EvaluationExecutionIdentity? identity,
        List<string> errors)
    {
        if (identity is null)
        {
            errors.Add("run.executionIdentity é obrigatório.");
            return;
        }

        Required(identity.CodeCommit, "executionIdentity.codeCommit", errors);
        Required(identity.ApiVersion, "executionIdentity.apiVersion", errors);
        Required(identity.AlgorithmConfigurationVersion,
            "executionIdentity.algorithmConfigurationVersion", errors);
        Required(identity.ConfigurationFingerprint,
            "executionIdentity.configurationFingerprint", errors);

        foreach (var model in identity.Models ?? [])
        {
            Required(model.Component, "model.component", errors);
            Required(model.ModelId, "model.modelId", errors);
            Required(model.ModelVersion, "model.modelVersion", errors);
            if (model.TrainedAtUtc.HasValue)
            {
                Utc(model.TrainedAtUtc.Value, "model.trainedAtUtc", errors);
            }
        }
    }

    private static void ValidateEvents(
        IndustrialEvaluationRun run,
        List<string> errors)
    {
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in run.Events ?? [])
        {
            if (item is null)
            {
                errors.Add("events não pode conter nulo.");
                continue;
            }

            if (!string.Equals(item.SchemaVersion, IndustrialEvaluationSchemas.EventV1,
                    StringComparison.Ordinal))
            {
                errors.Add($"event '{item.EventId}' possui schemaVersion inválido.");
            }

            Required(item.EventId, "event.eventId", errors);
            if (!eventIds.Add(item.EventId ?? string.Empty))
            {
                errors.Add($"eventId duplicado: '{item.EventId}'.");
            }

            ValidateRunScope(item.ScenarioId, item.TestClientId, run, "event", errors);
            ValidateTags(item.Tags, "event.tags", errors);
            Required(item.FaultType, "event.faultType", errors);
            Required(item.FaultClass, "event.faultClass", errors);
            if (item.LabelOrigin is null)
            {
                errors.Add($"event '{item.EventId}' não possui labelOrigin.");
            }
            else
            {
                Required(item.LabelOrigin.Reference, "event.labelOrigin.reference", errors);
                Required(item.LabelOrigin.ReviewStatus, "event.labelOrigin.reviewStatus", errors);
            }

            Utc(item.StartedAtUtc, "event.startedAtUtc", errors);
            Utc(item.EndedAtUtc, "event.endedAtUtc", errors);
            Utc(item.DetectionWindowOpensAtUtc, "event.detectionWindowOpensAtUtc", errors);
            Utc(item.DetectionWindowClosesAtUtc, "event.detectionWindowClosesAtUtc", errors);
            Utc(item.LateDetectionClosesAtUtc, "event.lateDetectionClosesAtUtc", errors);

            if (item.EndedAtUtc <= item.StartedAtUtc)
            {
                errors.Add($"event '{item.EventId}' possui duração inválida.");
            }

            if (item.DetectionWindowClosesAtUtc < item.DetectionWindowOpensAtUtc ||
                item.LateDetectionClosesAtUtc < item.DetectionWindowClosesAtUtc)
            {
                errors.Add($"event '{item.EventId}' possui janelas de detecção inconsistentes.");
            }
        }
    }

    private static void ValidateNormalIntervals(
        IndustrialEvaluationRun run,
        List<string> errors)
    {
        var intervalIds = new HashSet<string>(StringComparer.Ordinal);
        var perTag = new Dictionary<string, List<ExplicitNormalInterval>>(StringComparer.Ordinal);

        foreach (var item in run.NormalIntervals ?? [])
        {
            if (item is null)
            {
                errors.Add("normalIntervals não pode conter nulo.");
                continue;
            }

            Required(item.IntervalId, "normalInterval.intervalId", errors);
            if (!intervalIds.Add(item.IntervalId ?? string.Empty))
            {
                errors.Add($"normalIntervalId duplicado: '{item.IntervalId}'.");
            }

            ValidateRunScope(item.ScenarioId, item.TestClientId, run, "normalInterval", errors);
            ValidateTags(item.Tags, "normalInterval.tags", errors);
            Required(item.LabelReference, "normalInterval.labelReference", errors);
            Utc(item.StartsAtUtc, "normalInterval.startsAtUtc", errors);
            Utc(item.EndsAtUtc, "normalInterval.endsAtUtc", errors);
            if (item.EndsAtUtc <= item.StartsAtUtc)
            {
                errors.Add($"normalInterval '{item.IntervalId}' possui duração inválida.");
            }

            foreach (var tag in item.Tags ?? [])
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    if (!perTag.TryGetValue(tag, out var intervals))
                    {
                        intervals = [];
                        perTag.Add(tag, intervals);
                    }

                    intervals.Add(item);
                }
            }
        }

        foreach (var (_, intervals) in perTag)
        {
            var ordered = intervals.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.IntervalId,
                StringComparer.Ordinal).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                if (Overlaps(ordered[index - 1].StartsAtUtc, ordered[index - 1].EndsAtUtc,
                        ordered[index].StartsAtUtc, ordered[index].EndsAtUtc))
                {
                    errors.Add($"intervalos normais se sobrepõem para a tag '{TagForOverlap(ordered[index - 1], ordered[index])}'.");
                }
            }
        }

        foreach (var normal in run.NormalIntervals ?? [])
        {
            if (normal is null)
            {
                continue;
            }

            foreach (var item in run.Events ?? [])
            {
                if (item is null || !SharesTag(normal.Tags, item.Tags))
                {
                    continue;
                }

                // Intervalos são [início, fim): adjacência é válida.
                // Uma janela de detecção pode começar antes da falha e terminar
                // antes da recuperação física. Ambas as regiões proíbem
                // rótulo explícito de normalidade para a mesma tag.
                if (Overlaps(normal.StartsAtUtc, normal.EndsAtUtc,
                        item.DetectionWindowOpensAtUtc, item.LateDetectionClosesAtUtc))
                {
                    errors.Add($"normalInterval '{normal.IntervalId}' se sobrepõe à janela de detecção do evento '{item.EventId}'.");
                }
                else if (Overlaps(normal.StartsAtUtc, normal.EndsAtUtc,
                             item.StartedAtUtc, item.EndedAtUtc))
                {
                    errors.Add($"normalInterval '{normal.IntervalId}' se sobrepõe à duração física do evento '{item.EventId}'.");
                }
            }
        }
    }

    private static void ValidateReadings(
        IndustrialEvaluationRun run,
        List<string> errors)
    {
        var readingIds = new HashSet<string>(StringComparer.Ordinal);
        var lastTimestampByTag = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

        foreach (var item in run.Readings ?? [])
        {
            if (item is null)
            {
                errors.Add("readings não pode conter nulo.");
                continue;
            }

            if (!string.Equals(item.SchemaVersion, IndustrialEvaluationSchemas.ReadingV1,
                    StringComparison.Ordinal))
            {
                errors.Add($"reading '{item.ReadingId}' possui schemaVersion inválido.");
            }

            Required(item.ReadingId, "reading.readingId", errors);
            if (!readingIds.Add(item.ReadingId ?? string.Empty))
            {
                errors.Add($"readingId duplicado: '{item.ReadingId}'.");
            }

            Required(item.RunId, "reading.runId", errors);
            if (!string.Equals(item.RunId, run.RunId, StringComparison.Ordinal))
            {
                errors.Add($"reading '{item.ReadingId}' pertence a outro runId.");
            }

            ValidateRunScope(item.ScenarioId, item.TestClientId, run, "reading", errors);
            Required(item.TagName, "reading.tagName", errors);
            Required(item.DecisionSource, "reading.decisionSource", errors);
            Utc(item.TimestampUtc, "reading.timestampUtc", errors);

            if (item.EnsembleNormalizedScore is double score &&
                (!double.IsFinite(score) || score < 0 || score > 100))
            {
                errors.Add($"reading '{item.ReadingId}' possui score inválido.");
            }

            if (!string.IsNullOrWhiteSpace(item.TagName))
            {
                if (lastTimestampByTag.TryGetValue(item.TagName, out var last) &&
                    item.TimestampUtc <= last)
                {
                    errors.Add($"leituras da tag '{item.TagName}' não estão em ordem temporal estrita.");
                }

                lastTimestampByTag[item.TagName] = item.TimestampUtc;
            }
        }
    }

    private static void ValidateRunScope(
        string scenarioId,
        string testClientId,
        IndustrialEvaluationRun run,
        string entity,
        List<string> errors)
    {
        if (!string.Equals(scenarioId, run.ScenarioId, StringComparison.Ordinal))
        {
            errors.Add($"{entity}.scenarioId diverge do run.");
        }

        if (!string.Equals(testClientId, run.TestClientId, StringComparison.Ordinal))
        {
            errors.Add($"{entity}.testClientId diverge do run.");
        }
    }

    private static void ValidateTags(
        IReadOnlyList<string>? tags,
        string name,
        List<string> errors)
    {
        if (tags is null || tags.Count == 0)
        {
            errors.Add($"{name} deve conter ao menos uma tag.");
            return;
        }

        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            Required(tag, name, errors);
            if (!string.IsNullOrWhiteSpace(tag) && !distinct.Add(tag))
            {
                errors.Add($"{name} contém tag duplicada '{tag}'.");
            }
        }
    }

    private static void ValidateSyntheticClient(string? clientId, string name,
        List<string> errors)
    {
        Required(clientId, name, errors);
        if (!string.IsNullOrWhiteSpace(clientId) &&
            !clientId.StartsWith(SyntheticClientPrefix, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{name} deve usar cliente sintético com prefixo '{SyntheticClientPrefix}'.");
        }
    }

    private static void Required(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} é obrigatório.");
        }
    }

    private static void Utc(DateTimeOffset value, string name, List<string> errors)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            errors.Add($"{name} deve estar em UTC.");
        }
    }

    private static bool SharesTag(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        (left ?? []).Intersect(right ?? [], StringComparer.Ordinal).Any();

    private static bool Overlaps(DateTimeOffset leftStart, DateTimeOffset leftEnd,
        DateTimeOffset rightStart, DateTimeOffset rightEnd) =>
        leftStart < rightEnd && rightStart < leftEnd;

    private static string TagForOverlap(ExplicitNormalInterval left,
        ExplicitNormalInterval right) =>
        left.Tags.Intersect(right.Tags, StringComparer.Ordinal).OrderBy(x => x,
            StringComparer.Ordinal).FirstOrDefault() ?? "desconhecida";
}
