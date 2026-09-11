namespace IAApi.Benchmark;

public class BenchmarkExecutionSummary
{
    public string ClienteId { get; set; } =
        string.Empty;

    public string BancoOrigem { get; set; } =
        string.Empty;

    public DateTime InicioExecucaoUtc { get; set; }

    public DateTime FimExecucaoUtc { get; set; }

    public double DuracaoSegundos { get; set; }

    public long TotalLeiturasLidas { get; set; }

    public long TotalLeiturasAquecimento { get; set; }

    public long TotalLeiturasInvalidas { get; set; }

    public long TotalDeteccoesExecutadas { get; set; }

    public long TotalAnomaliasDetectadas { get; set; }

    public long TotalErros { get; set; }

    public string DetectionsFile { get; set; } =
        string.Empty;

    public string ErrorsFile { get; set; } =
        string.Empty;
}