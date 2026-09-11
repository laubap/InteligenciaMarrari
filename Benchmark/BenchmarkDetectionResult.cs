namespace IAApi.Benchmark;

public class BenchmarkDetectionResult
{
    public string ClienteId { get; set; } =
        string.Empty;

    public string TagName { get; set; } =
        string.Empty;

    public int MinuteIndex { get; set; }

    public DateTime DataHora { get; set; }

    public float Valor { get; set; }

    public bool EhAnomalia { get; set; }

    public float Score { get; set; }

    public string Mensagem { get; set; } =
        string.Empty;

    public string? Severidade { get; set; }

    public bool InterpretacaoGeradaPorLlm { get; set; }

    public List<BenchmarkDetectorResult> Detectores { get; set; } =
        new();
}

public class BenchmarkDetectorResult
{
    public string Nome { get; set; } =
        string.Empty;

    public bool Aplicavel { get; set; }

    public bool VotouAnomalia { get; set; }

    public double ScoreNormalizado { get; set; }

    public double Peso { get; set; }

    public string Explicacao { get; set; } =
        string.Empty;
}