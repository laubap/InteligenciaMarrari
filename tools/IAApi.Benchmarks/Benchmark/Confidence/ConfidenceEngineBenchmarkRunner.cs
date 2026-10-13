namespace IAApi.Benchmark;

public sealed class ConfidenceEngineBenchmarkRunner
{
    private readonly ConfidenceBenchmarkAnalyzer _analyzer;
    private readonly ConfidenceBenchmarkReportWriter _writer;

    public ConfidenceEngineBenchmarkRunner(
        ConfidenceBenchmarkAnalyzer analyzer,
        ConfidenceBenchmarkReportWriter writer)
    {
        _analyzer = analyzer;
        _writer = writer;
    }

    public async Task RunAsync(
        string resultDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(resultDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Diretório de resultados não encontrado: {resultDirectory}");
        }

        var inputFile =
            new DirectoryInfo(resultDirectory)
                .GetFiles("false_positive_structural_analysis_*.csv")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

        if (inputFile is null)
        {
            throw new FileNotFoundException(
                "Nenhum arquivo false_positive_structural_analysis_*.csv foi encontrado.");
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" CONFIDENCE ENGINE BENCHMARK RUNNER");
        Console.WriteLine("========================================");
        Console.WriteLine($"Entrada: {inputFile.FullName}");

        var events =
            ConfidenceBenchmarkCsvReader.Read(
                inputFile.FullName,
                cancellationToken);

        Console.WriteLine($"Eventos lidos: {events.Count:N0}");

        var report =
            _analyzer.Analyze(
                inputFile.FullName,
                events);

        var paths =
            await _writer.WriteAsync(
                report,
                resultDirectory,
                cancellationToken);

        _writer.Print(
            report,
            paths);
    }
}
