public class JanelaTreinamentoTagIa
{
    public int Id { get; set; }

    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public DateTime DataHoraInicio { get; set; }

    public DateTime DataHoraFim { get; set; }

    public string? Descricao { get; set; }

    public string? ModoOperacao { get; set; }

    public bool Ativa { get; set; } = true;

    public DateTime DataCadastro { get; set; } = DateTime.Now;

    public DateTime DataAtualizacao { get; set; }

    public bool ValidarPeriodo()
    {
        return DataHoraFim > DataHoraInicio;
    }
}