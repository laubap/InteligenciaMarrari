namespace IAApi.Services.ModelTraining;

public class JanelaTreinamentoSnapshot
{
    public int JanelaId { get; set; }

    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }

    public string? Descricao { get; set; }

    public string? ModoOperacao { get; set; }
}