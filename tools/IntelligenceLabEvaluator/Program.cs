using IAApi.IntelligenceLab.Evaluation;

if (args.Length != 3)
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/IntelligenceLabEvaluator/IAApi.IntelligenceLabEvaluator.csproj -- <exportacao-replay.json> <gabarito-independente.json> <relatorio.json>");
    return 2;
}

try
{
    var exportPath = Path.GetFullPath(args[0]);
    var labelsPath = Path.GetFullPath(args[1]);
    var reportPath = Path.GetFullPath(args[2]);
    if (string.Equals(reportPath, exportPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reportPath, labelsPath, StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("O relatorio nao pode sobrescrever os arquivos de entrada.");
    }

    // Nenhum acesso ao Bridge, API ou banco. A avaliacao nao modifica as entradas.
    var exportJson = await File.ReadAllTextAsync(exportPath);
    var labelsJson = await File.ReadAllTextAsync(labelsPath);
    var report = ReplayLabeledEvaluationRunner.Evaluate(exportJson, labelsJson);
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    await File.WriteAllTextAsync(reportPath, IndustrialEvaluationReportJson.Serialize(report));

    Console.WriteLine($"RunId: {report.RunId}");
    Console.WriteLine("Identidade de codigo/configuracao/modelos: UNVERIFIED (replay 3B.1)");
    Console.WriteLine($"Eventos elegiveis: {report.EventMetrics.EvaluableEvents}");
    Console.WriteLine($"Detectados no prazo: {report.EventMetrics.DetectedOnTime}");
    Console.WriteLine($"Detectados tarde: {report.EventMetrics.DetectedLate}");
    Console.WriteLine($"Eventos perdidos: {report.EventMetrics.Missed}");
    Console.WriteLine($"Episodios de falso alerta: {report.EpisodeMetrics.FalsePositiveEpisodes}");
    Console.WriteLine($"Episodios totais: {report.EpisodeMetrics.TotalEpisodes}");
    Console.WriteLine($"Recall estrito: {report.EventMetrics.StrictRecall?.ToString("P2") ?? "nao disponivel"}");
    Console.WriteLine($"Relatorio: {reportPath}");
    Console.WriteLine("ATENCAO: metrica experimental de cenario sintetico; nao representa precisao industrial em producao.");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine($"Avaliacao rejeitada: {exception.Message}");
    return 1;
}
