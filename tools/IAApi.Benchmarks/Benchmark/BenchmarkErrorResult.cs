namespace IAApi.Benchmark;

public class BenchmarkErrorResult
{
    public string ClienteId { get; set; } =
        string.Empty;

    public string TagName { get; set; } =
        string.Empty;

    public int MinuteIndex { get; set; }

    public DateTime DataHora { get; set; }

    public float Valor { get; set; }

    public string TipoErro { get; set; } =
        string.Empty;

    public string Mensagem { get; set; } =
        string.Empty;
}