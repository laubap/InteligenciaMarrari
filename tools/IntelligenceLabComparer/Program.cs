using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;

// CLI offline, somente leitura dos artefatos do replay. Nao acessa Bridge,
// banco de cliente, API ou modelos de producao.
if (args.Length != 5)
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/IntelligenceLabComparer -- " +
        "<pasta_replay_6> <gabarito_6.json> <pasta_replay_7> <gabarito_7.json> <pasta_saida>");
    return 2;
}

try
{
    var sixFolder = Path.GetFullPath(args[0]);
    var sevenFolder = Path.GetFullPath(args[2]);
    var labelsSixPath = Path.GetFullPath(args[1]);
    var labelsSevenPath = Path.GetFullPath(args[3]);
    var output = Path.GetFullPath(args[4]);
    var sixPath = Path.Combine(sixFolder, "intelligence-lab-replay-evaluation-readings.json");
    var sevenPath = Path.Combine(sevenFolder, "intelligence-lab-replay-evaluation-readings.json");
    var reportPath = Path.Combine(output, "intelligence-lab-comparison-report.json");
    var csvPath = Path.Combine(output, "intelligence-lab-comparison-readings.csv");

    if (File.Exists(reportPath) || File.Exists(csvPath))
        throw new IOException("Saida ja existe. Use uma pasta nova; nenhum relatorio sera sobrescrito.");
    var inputs = new[] { sixPath, sevenPath, labelsSixPath, labelsSevenPath,
        Path.Combine(sixFolder, "intelligence-lab-run-manifest.json"),
        Path.Combine(sevenFolder, "intelligence-lab-run-manifest.json") };
    if (inputs.Any(p => p == reportPath || p == csvPath))
        throw new ArgumentException("Saida nao pode sobrescrever entradas.");

    var sixJson = await File.ReadAllTextAsync(sixPath);
    var sevenJson = await File.ReadAllTextAsync(sevenPath);
    var labelsSix = await File.ReadAllTextAsync(labelsSixPath);
    var labelsSeven = await File.ReadAllTextAsync(labelsSevenPath);
    using var firstManifest = JsonDocument.Parse(await File.ReadAllTextAsync(inputs[4]));
    using var secondManifest = JsonDocument.Parse(await File.ReadAllTextAsync(inputs[5]));

    string Sha(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // Comparacao so e valida para exatamente os mesmos bytes de treino e avaliacao.
    // Verifica tambem se cada exportacao pertence ao manifesto declarado.
    var sixManifest = firstManifest.RootElement;
    var sevenManifest = secondManifest.RootElement;
    string Hash(JsonElement manifest, string field) =>
        manifest.GetProperty(field).GetProperty("sha256").GetString() ??
        throw new ArgumentException($"Manifesto sem hash de {field}.");
    if (Hash(sixManifest, "trainingInput") != Hash(sevenManifest, "trainingInput") ||
        Hash(sixManifest, "evaluationInput") != Hash(sevenManifest, "evaluationInput"))
        throw new ArgumentException("Treino/avaliacao diferem entre os dois manifests. Comparacao cancelada.");
    if (sixManifest.GetProperty("configuration").GetProperty("mode").GetString() !=
        "OfflineIsolatedHistoricalReplay" ||
        sevenManifest.GetProperty("configuration").GetProperty("mode").GetString() !=
        "OfflineIsolatedHistoricalReplayFullEnsemble")
        throw new ArgumentException("Os manifestos nao correspondem aos modos 6/7 detectores esperados.");

    static void VerifyExportArtifact(JsonElement manifest, string folder, string fileName, string actualSha)
    {
        var artifacts = manifest.GetProperty("artifacts").EnumerateArray().ToArray();
        var match = artifacts.SingleOrDefault(item =>
            string.Equals(item.GetProperty("name").GetString(), fileName, StringComparison.Ordinal));
        if (match.ValueKind == JsonValueKind.Undefined ||
            !string.Equals(match.GetProperty("sha256").GetString(), actualSha,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Exportacao nao coincide com o manifesto de {folder}.");
    }
    VerifyExportArtifact(sixManifest, sixFolder, Path.GetFileName(sixPath), Sha(sixPath));
    VerifyExportArtifact(sevenManifest, sevenFolder, Path.GetFileName(sevenPath), Sha(sevenPath));

    var comparison = ReplayModeComparisonService.Compare(sixJson, labelsSix, sevenJson, labelsSeven);
    if (sixManifest.GetProperty("runId").GetString() != comparison.RunIdSix ||
        sevenManifest.GetProperty("runId").GetString() != comparison.RunIdSeven)
        throw new ArgumentException("RunIds dos manifests nao coincidem com os replays.");

    var report = new
    {
        comparison.SchemaVersion,
        comparison.ScenarioId,
        comparison.TestClientId,
        comparison.TagName,
        comparison.RunIdSix,
        comparison.RunIdSeven,
        comparison.ExecutionIdentityStatus,
        ManifestInputHashesEqual = true,
        ReplayExportArtifactHashesVerified = true,
        ReproducibilitySix = sixManifest.GetProperty("reproducibilityStatus").GetString(),
        ReproducibilitySeven = sevenManifest.GetProperty("reproducibilityStatus").GetString(),
        TrainingSha256 = Hash(sixManifest, "trainingInput"),
        EvaluationSha256 = Hash(sixManifest, "evaluationInput"),
        comparison.AlignedReadings,
        comparison.AlertDisagreements,
        comparison.SixDetectors,
        comparison.SevenDetectors,
        comparison.Phases,
        Limitations = "Avaliacao sintetica, sem identidade operacional completamente verificada. " +
            "Integridade por hash nao autentica origem nem prova eficacia industrial em producao."
    };
    var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report,
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    var csv = new StringBuilder("index,timestampUtc,tagName,phase,scoreSix,scoreSeven,difference,alertSix,alertSeven\n");
    foreach (var row in comparison.Readings)
    {
        csv.Append(row.Index).Append(',')
            .Append(row.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
            .Append(row.TagName).Append(',').Append(row.Phase).Append(',')
            .Append(row.ScoreSix.ToString("R", CultureInfo.InvariantCulture)).Append(',')
            .Append(row.ScoreSeven.ToString("R", CultureInfo.InvariantCulture)).Append(',')
            .Append(row.Difference.ToString("R", CultureInfo.InvariantCulture)).Append(',')
            .Append(row.AlertSix.ToString().ToLowerInvariant()).Append(',')
            .Append(row.AlertSeven.ToString().ToLowerInvariant()).Append('\n');
    }

    Directory.CreateDirectory(output);
    // Sem sobrescrita; se falhar o segundo arquivo, desfaz o primeiro.
    await using (var outFile = new FileStream(reportPath, FileMode.CreateNew))
        await outFile.WriteAsync(jsonBytes);
    try
    {
        await using var outCsv = new FileStream(csvPath, FileMode.CreateNew);
        await outCsv.WriteAsync(Encoding.UTF8.GetBytes(csv.ToString()));
    }
    catch
    {
        File.Delete(reportPath);
        throw;
    }

    Console.WriteLine($"Leituras alinhadas: {comparison.AlignedReadings}; divergencias de alerta: {comparison.AlertDisagreements}");
    Console.WriteLine($"6 detectores TP/FN/FP/TN: {comparison.SixDetectors.TruePositives}/" +
        $"{comparison.SixDetectors.FalseNegatives}/{comparison.SixDetectors.FalsePositives}/" +
        $"{comparison.SixDetectors.TrueNegatives}");
    Console.WriteLine($"6 detectores precisao/recall por leitura: " +
        $"{FormatMetric(comparison.SixDetectors.ScorableReadingPrecision)}/" +
        $"{FormatMetric(comparison.SixDetectors.ReadingRecall)}; " +
        $"evento no prazo/tardio/ausente: {comparison.SixDetectors.OnTimeDetectedEvents}/" +
        $"{comparison.SixDetectors.LateDetectedEvents}/{comparison.SixDetectors.MissedEvents}; " +
        $"atraso: {FormatMetric(comparison.SixDetectors.DetectionDelayMinutes)} min");
    Console.WriteLine($"7 detectores TP/FN/FP/TN: {comparison.SevenDetectors.TruePositives}/" +
        $"{comparison.SevenDetectors.FalseNegatives}/{comparison.SevenDetectors.FalsePositives}/" +
        $"{comparison.SevenDetectors.TrueNegatives}");
    Console.WriteLine($"7 detectores precisao/recall por leitura: " +
        $"{FormatMetric(comparison.SevenDetectors.ScorableReadingPrecision)}/" +
        $"{FormatMetric(comparison.SevenDetectors.ReadingRecall)}; " +
        $"evento no prazo/tardio/ausente: {comparison.SevenDetectors.OnTimeDetectedEvents}/" +
        $"{comparison.SevenDetectors.LateDetectedEvents}/{comparison.SevenDetectors.MissedEvents}; " +
        $"atraso: {FormatMetric(comparison.SevenDetectors.DetectionDelayMinutes)} min");
    foreach (var phase in comparison.Phases.Where(x => x.Readings > 0))
        Console.WriteLine($"{phase.Phase}: 6={phase.MeanScoreSix:F3}; 7={phase.MeanScoreSeven:F3}; delta={phase.MeanDifference:F3}");
    Console.WriteLine($"Relatorio: {reportPath}");
    Console.WriteLine($"Leituras CSV: {csvPath}");
    Console.WriteLine("Identidade operacional: UNVERIFIED; evidencia experimental, nao precisao industrial.");
    return 0;
}
catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
{
    Console.Error.WriteLine($"Comparacao rejeitada: {e.Message}");
    return 1;
}

static string FormatMetric(double? value) => value?.ToString("F3", CultureInfo.InvariantCulture) ?? "N/A";
