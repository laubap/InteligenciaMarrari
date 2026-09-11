public interface IGrupoAnaliseProvider
{
    Task<List<GrupoAnaliseDto>> CarregarGruposAsync(
        string clienteId);
}