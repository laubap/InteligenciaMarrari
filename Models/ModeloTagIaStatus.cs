public class ModeloTagIaStatus
{
    public int Id { get; set; }

    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public string TipoModelo { get; set; } = "";

    public StatusModeloIa Status { get; set; } =
        StatusModeloIa.NaoTreinado;

    public int Versao { get; set; } = 1;

    public DateTime? DataUltimoTreinamento { get; set; }

    public DateTime? DataMarcadoDesatualizado { get; set; }

    public DateTime? DataInicioTreinamento { get; set; }

    public DateTime? DataFimTreinamento { get; set; }

    public string? UltimoErro { get; set; }

    public int QuantidadePontosTreinamento { get; set; }

    public string? HashJanelasTreinamento { get; set; }
}