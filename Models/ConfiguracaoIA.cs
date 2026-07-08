public class ConfiguracaoIA
{
    public int Id { get; set; }

    public string ClienteId { get; set; } = "";

    public int HorasHistorico { get; set; } = 24;
    public int Sensibilidade { get; set; } = 70;
    public int ConfiancaMinima { get; set; } = 80;

    public bool AprendizadoAtivo { get; set; } = true;

    public bool NotificacaoPopup { get; set; } = true;
    public bool NotificacaoEmail { get; set; } = false;
    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;
}