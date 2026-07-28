// Interface responsável pelo acesso ao histórico das tags.
public interface IHistoricoService
{
    // Busca o histórico recente configurado no appsettings.json.
    List<LeituraBruta> BuscarHistorico(
        string clienteId,
        string tagName,
        string tipoTag
    );

    // Busca o histórico correspondente a um intervalo específico.
    List<LeituraBruta> BuscarHistoricoPorPeriodo(
        string clienteId,
        string tagName,
        DateTime inicio,
        DateTime fim
    );
}