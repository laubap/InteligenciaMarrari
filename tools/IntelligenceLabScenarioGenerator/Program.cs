using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.IntelligenceLab.Evaluation;

var (scenarioName, outputArgument) = args switch
{
    [var output] when !output.StartsWith("--", StringComparison.Ordinal) =>
        ("flow-drop-sustained", output),
    ["--scenario", "flow-drop-sustained", var output] =>
        ("flow-drop-sustained", output),
    ["--scenario", "gradual-drift", var output] =>
        ("gradual-drift", output),
    _ => (null, null)
};

if (scenarioName is null || outputArgument is null)
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/IntelligenceLabScenarioGenerator -- " +
        "[--scenario flow-drop-sustained|gradual-drift] <pasta_saida_vazia>");
    return 2;
}

try
{
    var folder = Path.GetFullPath(outputArgument);
    var dataset = scenarioName == "gradual-drift"
        ? SyntheticGradualDriftScenario.Create()
        : SyntheticFlowDropScenario.Create();
    var definition = dataset.Metadata;
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    byte[] Serialize<T>(T value) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options));

    var training = Serialize(dataset.Training);
    var evaluation = Serialize(dataset.Evaluation);
    var labels = Serialize(dataset.IndependentLabelsTemplate);
    string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    var metadata = Serialize(new
    {
        schemaVersion = "industrial-synthetic-scenario-definition/v1",
        scenarioDefinitionId = definition.ScenarioDefinitionId,
        replayScenarioId = definition.ReplayScenarioId,
        testClientId = definition.TestClientId,
        tagName = definition.TagName,
        unit = definition.Unit,
        trainingCount = definition.TrainingCount,
        evaluationCount = definition.EvaluationCount,
        eventStartIndex = definition.EventStartIndex,
        eventEndExclusiveIndex = definition.EventEndExclusiveIndex,
        recoveryStartIndex = definition.RecoveryStartIndex,
        recoveryEndExclusiveIndex = definition.RecoveryEndExclusiveIndex,
        cadenceSeconds = definition.CadenceSeconds,
        sourceTimestampAssumption = "UTC-03:00 (relogio local sem offset)",
        groundTruthStatus = "IndependentTemplatePendingReplayBinding",
        groundTruthDescription = definition.GroundTruthDescription,
        limitation = "PK7.TAG1 e nome tecnico do replay sintetico, nao comprovacao de tag fisica de vazao ou parada de bomba.",
        trainingSha256 = Hash(training),
        evaluationSha256 = Hash(evaluation),
        labelsTemplateSha256 = Hash(labels)
    });

    var files = new (string Name, byte[] Content)[]
    {
        ("treino.json", training),
        ("avaliacao.json", evaluation),
        ("intelligence-lab-independent-labels.template.json", labels),
        ("intelligence-lab-scenario-definition.json", metadata)
    };
    // Nao sobrescrever dados ou resultados de experimentos existentes.
    if (files.Any(file => File.Exists(Path.Combine(folder, file.Name))))
        throw new IOException("A pasta ja contem arquivos do cenario. Use uma pasta nova; nada foi sobrescrito.");

    Directory.CreateDirectory(folder);
    var created = new List<string>();
    try
    {
        foreach (var (name, content) in files)
        {
            var path = Path.Combine(folder, name);
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created.Add(path);
            await output.WriteAsync(content);
        }
    }
    catch
    {
        foreach (var path in created)
            File.Delete(path);
        throw;
    }

    Console.WriteLine($"Cenario: {definition.ScenarioDefinitionId}");
    Console.WriteLine($"Treino: {dataset.Training.Count} leituras; avaliacao: {dataset.Evaluation.Count} leituras.");
    Console.WriteLine($"Evento: indices [{definition.EventStartIndex}, " +
        $"{definition.EventEndExclusiveIndex}) da avaliacao.");
    Console.WriteLine($"Saida: {folder}");
    Console.WriteLine("IMPORTANTE: gabarito independente ainda e TEMPLATE: runId/hash serao vinculados na M4.1B.");
    Console.WriteLine("Nao calcular metricas nem inferir parada real de bomba nesta etapa.");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"Geracao rejeitada: {ex.Message}");
    return 1;
}
