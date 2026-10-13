using System.Text.Json;

namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// B3.17 — avaliação final de preservação funcional contra o Oracle.
///
/// IMPORTANTE:
/// A camada funcional altera somente a emissão univariada.
/// O caminho multivariado permanece operacionalmente inalterado.
///
/// Por isso a avaliação correta usa:
///
/// LEGACY REFERENCE:
///   arquivo de detecções de uma execução Legacy equivalente.
///
/// FUNCTIONAL EVIDENCE:
///   união de:
///   1. detecções da execução operacional funcional
///      (inclui multivariado intacto + univariado efetivamente emitido);
///   2. functional_new_evidence
///      (preserva CandidateMismatch/B->C mesmo quando EmitAnomaly=false).
///
/// Uma truth é detector-evaluable quando o Legacy Reference possui
/// ao menos uma detecção para uma das tags da condição dentro da janela.
///
/// Uma truth é preservada quando a união funcional possui ao menos
/// uma evidência para uma das tags da condição dentro da mesma janela.
///
/// Atraso = primeira evidência funcional posterior à primeira Legacy.
/// Evidência funcional anterior ou no mesmo minuto = atraso zero.
/// </summary>
public sealed class FunctionalOraclePreservationEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

    public async Task<FunctionalOraclePreservationSummary> EvaluateAsync(
        string expandedOraclePath,
        string legacyDetectionsPath,
        string operationalDetectionsPath,
        string functionalNewEvidencePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(expandedOraclePath, "Oracle expandido");
        ValidateFile(legacyDetectionsPath, "Detecções Legacy");
        ValidateFile(operationalDetectionsPath, "Detecções operacionais");
        ValidateFile(functionalNewEvidencePath, "Nova evidência funcional");

        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var oracle =
            await LoadOracleAsync(
                expandedOraclePath,
                cancellationToken);

        var legacyDetections =
            await LoadDetectionEvidenceAsync(
                legacyDetectionsPath,
                cancellationToken);

        var operationalDetections =
            await LoadDetectionEvidenceAsync(
                operationalDetectionsPath,
                cancellationToken);

        var functionalNewEvidence =
            await LoadFunctionalEvidenceAsync(
                functionalNewEvidencePath,
                cancellationToken);

        /*
         * União funcional:
         *
         * - detecções operacionais: tudo que realmente seria emitido;
         * - new evidence: CandidateMismatch e demais evidências que
         *   não necessariamente viram uma nova anomalia operacional.
         *
         * Distinct evita duplicar NewAnomaly que aparece nos dois canais.
         */
        var functionalEvidence =
            operationalDetections
                .Select(x =>
                    new FunctionalEvidenceRecord(
                        x.MinuteIndex,
                        x.Tag,
                        "OperationalDetection",
                        null,
                        null))
                .Concat(functionalNewEvidence)
                .GroupBy(
                    x => new
                    {
                        x.MinuteIndex,
                        Tag = x.Tag.ToUpperInvariant()
                    })
                .Select(group =>
                    group
                        .OrderByDescending(x =>
                            string.Equals(
                                x.Source,
                                "FunctionalNewEvidence",
                                StringComparison.Ordinal))
                        .First())
                .ToArray();

        var results =
            new List<FunctionalOracleTruthResult>();

        foreach (var condition in oracle.Conditions)
        {
            /*
             * Critério canônico B3.15/B3.17:
             * condições cujo truth representa ausência/intermitência de dados
             * não são detector-evaluable pelo detector de anomalia de valor.
             *
             * Elas permanecem no relatório, mas não entram no denominador
             * de recall funcional.
             */
            if (condition.MissingDataCondition)
            {
                results.Add(
                    new FunctionalOracleTruthResult(
                        condition.Number,
                        condition.OracleId,
                        condition.Title,
                        DetectorEvaluable: false,
                        LegacyFirstEvidenceMinute: null,
                        FunctionalFirstEvidenceMinute: null,
                        Preserved: false,
                        Lost: false,
                        Delayed: false,
                        DelayMinutes: null,
                        FunctionalEvidenceSource:
                            "ExcludedMissingDataTruth",
                        FunctionalEvidenceSourceDecision: null,
                        FunctionalUsedLegacyFallback: null));

                continue;
            }

            var tagSet =
                condition.Tags.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            var legacyMatches =
                legacyDetections
                    .Where(x =>
                        tagSet.Contains(x.Tag) &&
                        x.MinuteIndex >= condition.ExpectedStartMinuteIndex &&
                        x.MinuteIndex <= condition.ExpectedEndMinuteIndex)
                    .OrderBy(x => x.MinuteIndex)
                    .ToArray();

            if (legacyMatches.Length == 0)
            {
                results.Add(
                    new FunctionalOracleTruthResult(
                        condition.Number,
                        condition.OracleId,
                        condition.Title,
                        DetectorEvaluable: false,
                        LegacyFirstEvidenceMinute: null,
                        FunctionalFirstEvidenceMinute: null,
                        Preserved: false,
                        Lost: false,
                        Delayed: false,
                        DelayMinutes: null,
                        FunctionalEvidenceSource: null,
                        FunctionalEvidenceSourceDecision: null,
                        FunctionalUsedLegacyFallback: null));

                continue;
            }

            var legacyFirst =
                legacyMatches[0];

            var functionalMatches =
                functionalEvidence
                    .Where(x =>
                        tagSet.Contains(x.Tag) &&
                        x.MinuteIndex >= condition.ExpectedStartMinuteIndex &&
                        x.MinuteIndex <= condition.ExpectedEndMinuteIndex)
                    .OrderBy(x => x.MinuteIndex)
                    .ToArray();

            if (functionalMatches.Length == 0)
            {
                results.Add(
                    new FunctionalOracleTruthResult(
                        condition.Number,
                        condition.OracleId,
                        condition.Title,
                        DetectorEvaluable: true,
                        LegacyFirstEvidenceMinute:
                            legacyFirst.MinuteIndex,
                        FunctionalFirstEvidenceMinute:
                            null,
                        Preserved:
                            false,
                        Lost:
                            true,
                        Delayed:
                            false,
                        DelayMinutes:
                            null,
                        FunctionalEvidenceSource:
                            null,
                        FunctionalEvidenceSourceDecision:
                            null,
                        FunctionalUsedLegacyFallback:
                            null));

                continue;
            }

            var functionalFirst =
                functionalMatches[0];

            var rawDelay =
                functionalFirst.MinuteIndex -
                legacyFirst.MinuteIndex;

            var delayed =
                rawDelay > 0;

            results.Add(
                new FunctionalOracleTruthResult(
                    condition.Number,
                    condition.OracleId,
                    condition.Title,
                    DetectorEvaluable: true,
                    LegacyFirstEvidenceMinute:
                        legacyFirst.MinuteIndex,
                    FunctionalFirstEvidenceMinute:
                        functionalFirst.MinuteIndex,
                    Preserved:
                        true,
                    Lost:
                        false,
                    Delayed:
                        delayed,
                    DelayMinutes:
                        delayed ? rawDelay : 0,
                    FunctionalEvidenceSource:
                        functionalFirst.Source,
                    FunctionalEvidenceSourceDecision:
                        functionalFirst.SourceDecision,
                    FunctionalUsedLegacyFallback:
                        functionalFirst.UsedLegacyFallback));
        }

        var detectorEvaluable =
            results
                .Where(x =>
                    x.DetectorEvaluable)
                .ToArray();

        var functionalTruePositives =
            detectorEvaluable.Count(x =>
                x.Preserved);

        var functionalFalseNegatives =
            detectorEvaluable.Count(x =>
                x.Lost);

        var delayedTruthCount =
            detectorEvaluable.Count(x =>
                x.Delayed);

        var delays =
            detectorEvaluable
                .Where(x =>
                    x.DelayMinutes.HasValue)
                .Select(x =>
                    x.DelayMinutes!.Value)
                .ToArray();

        var summary =
            new FunctionalOraclePreservationSummary(
                GeneratedAtUtc:
                    DateTime.UtcNow,
                ExpandedOraclePath:
                    expandedOraclePath,
                LegacyDetectionsPath:
                    legacyDetectionsPath,
                OperationalDetectionsPath:
                    operationalDetectionsPath,
                FunctionalNewEvidencePath:
                    functionalNewEvidencePath,
                TotalOracleConditions:
                    oracle.Conditions.Count,
                DetectorEvaluableTruths:
                    detectorEvaluable.Length,
                FunctionalTruePositives:
                    functionalTruePositives,
                FunctionalFalseNegatives:
                    functionalFalseNegatives,
                FunctionalRecall:
                    detectorEvaluable.Length == 0
                        ? 0
                        : (double)functionalTruePositives /
                          detectorEvaluable.Length,
                TruthLost:
                    functionalFalseNegatives,
                DelayedTruths:
                    delayedTruthCount,
                MaxDelayMinutes:
                    delays.Length == 0
                        ? 0
                        : delays.Max(),
                MinDelayMinutes:
                    delays.Length == 0
                        ? 0
                        : delays.Min(),
                LegacyDetectionRecords:
                    legacyDetections.Count,
                OperationalDetectionRecords:
                    operationalDetections.Count,
                FunctionalNewEvidenceRecords:
                    functionalNewEvidence.Count,
                FunctionalUnionRecords:
                    functionalEvidence.Length,
                Results:
                    results);

        var timestamp =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss");

        var outputPath =
            Path.Combine(
                outputDirectory,
                $"functional_oracle_preservation_{timestamp}.json");

        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(
                summary,
                JsonOptions),
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            "=== B3.17 FUNCTIONAL ORACLE PRESERVATION ===");
        Console.WriteLine(
            $"Oracle conditions: {summary.TotalOracleConditions:N0}");
        Console.WriteLine(
            $"Detector-evaluable truths: {summary.DetectorEvaluableTruths:N0}");
        Console.WriteLine(
            $"Functional TP: {summary.FunctionalTruePositives:N0}");
        Console.WriteLine(
            $"Functional FN: {summary.FunctionalFalseNegatives:N0}");
        Console.WriteLine(
            $"Functional recall: {summary.FunctionalRecall:P2}");
        Console.WriteLine(
            $"Truth lost: {summary.TruthLost:N0}");
        Console.WriteLine(
            $"Delayed truths: {summary.DelayedTruths:N0}");
        Console.WriteLine(
            $"Max delay: {summary.MaxDelayMinutes:N0} min");
        Console.WriteLine(
            $"Min delay: {summary.MinDelayMinutes:N0} min");
        Console.WriteLine(
            $"Legacy detection records: {summary.LegacyDetectionRecords:N0}");
        Console.WriteLine(
            $"Operational detection records: {summary.OperationalDetectionRecords:N0}");
        Console.WriteLine(
            $"Functional new evidence records: {summary.FunctionalNewEvidenceRecords:N0}");
        Console.WriteLine(
            $"Functional union records: {summary.FunctionalUnionRecords:N0}");
        Console.WriteLine(
            $"Report: {outputPath}");

        return summary;
    }

    private static void ValidateFile(
        string path,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{description} não encontrado.",
                path);
        }
    }

    private static async Task<OracleDocument> LoadOracleAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream =
            File.OpenRead(path);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken:
                    cancellationToken);

        var root =
            document.RootElement;

        JsonElement conditionsElement;

        if (root.ValueKind ==
            JsonValueKind.Array)
        {
            conditionsElement =
                root;
        }
        else if (!TryGetPropertyIgnoreCase(
                     root,
                     "conditions",
                     out conditionsElement))
        {
            throw new InvalidDataException(
                "O Oracle expandido deve possuir a propriedade 'conditions'.");
        }

        var conditions =
            new List<OracleCondition>();

        foreach (var element in
                 conditionsElement.EnumerateArray())
        {
            var number =
                GetInt32(
                    element,
                    "number",
                    conditions.Count + 1);

            var oracleId =
                GetString(
                    element,
                    "oracleId") ??
                GetString(
                    element,
                    "id") ??
                $"ORACLE-{number}";

            var title =
                GetString(
                    element,
                    "title") ??
                oracleId;

            var tags =
                GetStringArray(
                    element,
                    "tags");

            if (tags.Count == 0)
            {
                throw new InvalidDataException(
                    $"Oracle '{oracleId}' não possui tags.");
            }

            conditions.Add(
                new OracleCondition(
                    number,
                    oracleId,
                    title,
                    tags,
                    GetInt64(
                        element,
                        "expectedStartMinuteIndex"),
                    GetInt64(
                        element,
                        "expectedEndMinuteIndex"),
                    MissingDataCondition:
                        GetBoolean(
                            element,
                            "missingDataCondition"),
                    Supplemental:
                        GetBoolean(
                            element,
                            "supplemental")));
        }

        return new OracleDocument(
            conditions);
    }

    private static async Task<List<DetectionEvidenceRecord>>
        LoadDetectionEvidenceAsync(
            string path,
            CancellationToken cancellationToken)
    {
        var result =
            new List<DetectionEvidenceRecord>();

        using var reader =
            new StreamReader(path);

        while (await reader.ReadLineAsync(
                   cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var document =
                JsonDocument.Parse(line);

            var root =
                document.RootElement;

            var tag =
                GetString(
                    root,
                    "tagName") ??
                GetString(
                    root,
                    "tag");

            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (!TryGetInt64(
                    root,
                    "minuteIndex",
                    out var minuteIndex))
            {
                continue;
            }

            /*
             * O arquivo detections contém somente anomalias gravadas.
             * Ainda assim, se EhAnomalia existir explicitamente e for false,
             * ignoramos defensivamente a linha.
             */
            if (TryGetBoolean(
                    root,
                    "ehAnomalia",
                    out var ehAnomalia) &&
                !ehAnomalia)
            {
                continue;
            }

            result.Add(
                new DetectionEvidenceRecord(
                    minuteIndex,
                    tag));
        }

        return result;
    }

    private static async Task<List<FunctionalEvidenceRecord>>
        LoadFunctionalEvidenceAsync(
            string path,
            CancellationToken cancellationToken)
    {
        var result =
            new List<FunctionalEvidenceRecord>();

        using var reader =
            new StreamReader(path);

        while (await reader.ReadLineAsync(
                   cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var document =
                JsonDocument.Parse(line);

            var root =
                document.RootElement;

            if (!GetBoolean(
                    root,
                    "emitNewEvidence"))
            {
                continue;
            }

            var tag =
                GetString(
                    root,
                    "tag");

            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (!TryGetInt64(
                    root,
                    "minuteIndex",
                    out var minuteIndex))
            {
                continue;
            }

            int? sourceDecision =
                null;

            if (TryGetPropertyIgnoreCase(
                    root,
                    "sourceDecision",
                    out var sourceElement) &&
                sourceElement.ValueKind ==
                JsonValueKind.Number &&
                sourceElement.TryGetInt32(
                    out var source))
            {
                sourceDecision =
                    source;
            }

            bool? usedLegacyFallback =
                null;

            if (TryGetBoolean(
                    root,
                    "usedLegacyFallback",
                    out var fallback))
            {
                usedLegacyFallback =
                    fallback;
            }

            result.Add(
                new FunctionalEvidenceRecord(
                    minuteIndex,
                    tag,
                    "FunctionalNewEvidence",
                    sourceDecision,
                    usedLegacyFallback));
        }

        return result;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            foreach (var property in
                     element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value =
                        property.Value;

                    return true;
                }
            }
        }

        value =
            default;

        return false;
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        return TryGetPropertyIgnoreCase(
                   element,
                   propertyName,
                   out var value) &&
               value.ValueKind ==
               JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetBoolean(
        JsonElement element,
        string propertyName)
    {
        return TryGetBoolean(
                   element,
                   propertyName,
                   out var value) &&
               value;
    }

    private static bool TryGetBoolean(
        JsonElement element,
        string propertyName,
        out bool value)
    {
        if (TryGetPropertyIgnoreCase(
                element,
                propertyName,
                out var property) &&
            (property.ValueKind ==
             JsonValueKind.True ||
             property.ValueKind ==
             JsonValueKind.False))
        {
            value =
                property.GetBoolean();

            return true;
        }

        value =
            false;

        return false;
    }

    private static int GetInt32(
        JsonElement element,
        string propertyName,
        int fallback)
    {
        return TryGetPropertyIgnoreCase(
                   element,
                   propertyName,
                   out var value) &&
               value.ValueKind ==
               JsonValueKind.Number &&
               value.TryGetInt32(
                   out var result)
            ? result
            : fallback;
    }

    private static long GetInt64(
        JsonElement element,
        string propertyName)
    {
        if (!TryGetInt64(
                element,
                propertyName,
                out var result))
        {
            throw new InvalidDataException(
                $"Propriedade numérica obrigatória ausente: {propertyName}");
        }

        return result;
    }

    private static bool TryGetInt64(
        JsonElement element,
        string propertyName,
        out long result)
    {
        if (TryGetPropertyIgnoreCase(
                element,
                propertyName,
                out var value) &&
            value.ValueKind ==
            JsonValueKind.Number &&
            value.TryGetInt64(
                out result))
        {
            return true;
        }

        result =
            0;

        return false;
    }

    private static IReadOnlyList<string> GetStringArray(
        JsonElement element,
        string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(
                element,
                propertyName,
                out var value) ||
            value.ValueKind !=
            JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(x =>
                x.ValueKind ==
                JsonValueKind.String)
            .Select(x =>
                x.GetString())
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Select(x =>
                x!)
            .ToArray();
    }

    private sealed record OracleDocument(
        IReadOnlyList<OracleCondition> Conditions);

    private sealed record OracleCondition(
        int Number,
        string OracleId,
        string Title,
        IReadOnlyList<string> Tags,
        long ExpectedStartMinuteIndex,
        long ExpectedEndMinuteIndex,
        bool MissingDataCondition,
        bool Supplemental);

    private sealed record DetectionEvidenceRecord(
        long MinuteIndex,
        string Tag);

    private sealed record FunctionalEvidenceRecord(
        long MinuteIndex,
        string Tag,
        string Source,
        int? SourceDecision,
        bool? UsedLegacyFallback);

}

public sealed record FunctionalOraclePreservationSummary(
    DateTime GeneratedAtUtc,
    string ExpandedOraclePath,
    string LegacyDetectionsPath,
    string OperationalDetectionsPath,
    string FunctionalNewEvidencePath,
    int TotalOracleConditions,
    int DetectorEvaluableTruths,
    int FunctionalTruePositives,
    int FunctionalFalseNegatives,
    double FunctionalRecall,
    int TruthLost,
    int DelayedTruths,
    long MaxDelayMinutes,
    long MinDelayMinutes,
    int LegacyDetectionRecords,
    int OperationalDetectionRecords,
    int FunctionalNewEvidenceRecords,
    int FunctionalUnionRecords,
    IReadOnlyList<FunctionalOracleTruthResult> Results);

public sealed record FunctionalOracleTruthResult(
    int Number,
    string OracleId,
    string Title,
    bool DetectorEvaluable,
    long? LegacyFirstEvidenceMinute,
    long? FunctionalFirstEvidenceMinute,
    bool Preserved,
    bool Lost,
    bool Delayed,
    long? DelayMinutes,
    string? FunctionalEvidenceSource,
    int? FunctionalEvidenceSourceDecision,
    bool? FunctionalUsedLegacyFallback);
