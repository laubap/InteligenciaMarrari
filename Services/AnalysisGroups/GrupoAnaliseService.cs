public class GrupoAnaliseService
{
    private readonly IGrupoAnaliseProvider
        _provider;

    public GrupoAnaliseService(
        IGrupoAnaliseProvider provider)
    {
        _provider = provider;
    }

    public Task<List<GrupoAnaliseDto>>
        ObterGruposAsync(string clienteId)
    {
        return _provider.CarregarGruposAsync(
            clienteId);
    }
}