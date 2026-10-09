namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionContextLoader
{
    private readonly AppDbContext _context;
    private readonly ProcessoAnaliseService _processoAnaliseService;

    public PredictionContextLoader(
        AppDbContext context,
        ProcessoAnaliseService processoAnaliseService)
    {
        _context = context;
        _processoAnaliseService = processoAnaliseService;
    }

    public TagIaConfig? BuscarConfiguracaoAtiva(
        string clienteId,
        string tagName)
    {
        return _context.TagsIaConfig
            .FirstOrDefault(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.IaAtiva);
    }

    public TagPerfilIa? BuscarPerfil(
        string clienteId,
        string tagName)
    {
        return _context.PerfisIa
            .FirstOrDefault(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName);
    }

    public PredictionOperationalContextLoadResult
        CarregarContextoOperacional(
            string clienteId,
            string tagName)
    {
        var contexto =
            _context.TagsContextoIa
                .FirstOrDefault(x =>
                    x.ClienteId == clienteId &&
                    x.TagName == tagName);

        var processo =
            _processoAnaliseService.BuscarProcessoDaTag(
                clienteId,
                tagName);

        return new PredictionOperationalContextLoadResult(
            contexto,
            processo);
    }
}

public sealed record PredictionOperationalContextLoadResult(
    TagContextoIa? Contexto,
    ProcessoIa? Processo);