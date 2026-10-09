namespace IAApi.Services.AnomalyDetection.Presentation;

public sealed class PredictionDependencyMapper
{
    private readonly InterpretacaoDependenciasService
        _interpretacaoDependenciasService;

    public PredictionDependencyMapper(
        InterpretacaoDependenciasService interpretacaoDependenciasService)
    {
        _interpretacaoDependenciasService =
            interpretacaoDependenciasService;
    }

    public List<DependenciaAnalisadaDto> Mapear(
        AnaliseRelacionadasResultado analiseRelacionadas,
        TagContextoIa? contexto)
    {
        return analiseRelacionadas.Resultados
            .Select(item =>
                new DependenciaAnalisadaDto
                {
                    TagName =
                        item.TagName,

                    Descricao =
                        item.Descricao,

                    ValorAtual =
                        item.ValorAtual,

                    Media =
                        item.Media,

                    LimiteInferior =
                        item.LimiteInferior,

                    LimiteSuperior =
                        item.LimiteSuperior,

                    EhAnomalia =
                        item.EhAnomalia,

                    Status = item.EhAnomalia
                        ? "anômala"
                        : "normal",

                    TipoRelacao =
                        item.TipoRelacao,

                    Impacto =
                        item.Impacto,

                    Criticidade =
                        item.Criticidade,

                    Equipamento =
                        item.Equipamento,

                    Area =
                        item.Area,

                    Interpretacao =
                        _interpretacaoDependenciasService
                            .Interpretar(
                                item,
                                contexto)
                })
            .ToList();
    }
}