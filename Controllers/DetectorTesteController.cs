using IAApi.Services.AnomalyDetection;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dev/detectores")]
public class DetectorTesteController : ControllerBase
{
    private readonly EnsembleDetectionService _ensemble;

    public DetectorTesteController(
        EnsembleDetectionService ensemble)
    {
        _ensemble = ensemble;
    }

    [HttpPost]
    public IActionResult Testar(
        [FromBody] DetectorTesteRequest request)
    {
        if (request.Historico == null ||
            request.Historico.Count < 2)
        {
            return BadRequest(new
            {
                erro =
                    "Informe pelo menos dois pontos históricos."
            });
        }

        var historico = request.Historico
            .Where(ponto =>
                double.IsFinite(ponto.Valor)
            )
            .GroupBy(ponto => ponto.DataHora)
            .Select(grupo => grupo.Last())
            .OrderBy(ponto => ponto.DataHora)
            .Select(ponto => new HistoricoDetectorPoint
            {
                DataHora = ponto.DataHora,
                Valor = ponto.Valor
            })
            .ToList();

        if (historico.Count < 2)
        {
            return BadRequest(new
            {
                erro =
                    "Não existem pontos históricos válidos suficientes."
            });
        }

        var valores = historico
            .Select(ponto => ponto.Valor)
            .ToArray();

        var media = valores.Average();

        var variancia = valores
            .Select(valor =>
                Math.Pow(valor - media, 2)
            )
            .Average();

        var desvioPadrao =
            Math.Sqrt(variancia);

        var dataHoraAtual =
            request.DataHoraAtual == default
                ? historico.Last().DataHora.AddMinutes(1)
                : request.DataHoraAtual;

        var entrada = new DetectorInput
        {
            ClienteId = "TESTE",

            TagName =
                string.IsNullOrWhiteSpace(request.TagName)
                    ? "TESTE.TAG"
                    : request.TagName,

            TipoTag = "teste",

            ValorAtual =
                request.ValorAtual,

            DataHoraAtual =
                dataHoraAtual,

            MediaHistorica =
                media,

            DesvioPadraoHistorico =
                desvioPadrao,

            Historico =
                historico
        };

        var resultado =
            _ensemble.Detectar(entrada);

        return Ok(new
        {
            entrada = new
            {
                entrada.TagName,
                entrada.ValorAtual,
                entrada.DataHoraAtual,
                quantidadeHistorico =
                    entrada.Historico.Count
            },

            perfilCalculado = new
            {
                media,
                desvioPadrao
            },

            resumo = new
            {
                resultado.EhAnomalia,
                resultado.ScoreNormalizado,
                resultado.Detector,

                quantidadeAlertas =
                    resultado.Metricas
                        .GetValueOrDefault(
                            "quantidadeAlertas"
                        ),

                quantidadeAplicaveis =
                    resultado.Metricas
                        .GetValueOrDefault(
                            "quantidadeDetectoresAplicaveis"
                        )
            },

            detectores =
                resultado.DetalhesDetectores,

            resultadoCompleto =
                resultado
        });
    }
}

public class DetectorTesteRequest
{
    public string TagName { get; set; } = "TESTE.TAG";

    public double ValorAtual { get; set; }

    public DateTime DataHoraAtual { get; set; }

    public List<DetectorTestePointRequest> Historico { get; set; } = new();
}

public class DetectorTestePointRequest
{
    public DateTime DataHora { get; set; }

    public double Valor { get; set; }
}