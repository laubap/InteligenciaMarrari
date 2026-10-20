namespace IAApi.Application.Llm.Contracts;

public sealed class LlmResponse
{
    public bool Sucesso { get; set; }

    public string Conteudo { get; set; } = "";

    public string Modelo { get; set; } = "";

    public string Provider { get; set; } = "";

    public string? MotivoFinalizacao { get; set; }

    public long DuracaoTotalNanosegundos { get; set; }

    public long DuracaoCarregamentoNanosegundos { get; set; }

    public int TokensEntrada { get; set; }

    public int TokensSaida { get; set; }

    public string? Erro { get; set; }
}