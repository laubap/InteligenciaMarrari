using IAApi.Services.ModelTraining;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/ml/isolationforest")]
public class IsolationForestTrainingController : ControllerBase
{
    private const string TipoModelo = "IsolationForest";

    private readonly IsolationForestTrainingService _trainingService;
    private readonly AppDbContext _context;

    public IsolationForestTrainingController(
        IsolationForestTrainingService trainingService,
        AppDbContext context)
    {
        _trainingService =
            trainingService
            ?? throw new ArgumentNullException(
                nameof(trainingService)
            );

        _context =
            context
            ?? throw new ArgumentNullException(
                nameof(context)
            );
    }

    /// <summary>
    /// Treina ou retreina o Isolation Forest de uma tag.
    ///
    /// O treinamento utiliza:
    /// - as janelas normais ativas configuradas pelo cliente; ou
    /// - o histórico recente como fallback, caso nenhuma janela exista.
    /// </summary>
    [HttpPost("treinar")]
    public async Task<IActionResult> Treinar(
        [FromBody] TreinarIsolationForestRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                erro = "Os dados do treinamento são obrigatórios."
            });
        }

        var clienteId =
            request.ClienteId?.Trim() ?? "";

        var tagName =
            request.TagName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(clienteId))
        {
            return BadRequest(new
            {
                erro = "O cliente é obrigatório."
            });
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            return BadRequest(new
            {
                erro = "A tag é obrigatória."
            });
        }

        try
        {
            var resultado =
                await _trainingService.TreinarAsync(
                    clienteId,
                    tagName,
                    cancellationToken
                );

            return Ok(new
            {
                mensagem =
                    "Isolation Forest treinado com sucesso.",

                resultado
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(
                StatusCodes.Status499ClientClosedRequest,
                new
                {
                    erro =
                        "O treinamento foi cancelado."
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                erro = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                erro = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    erro =
                        "Ocorreu um erro inesperado durante o treinamento.",

                    detalhe =
                        ex.Message
                }
            );
        }
    }

    /// <summary>
    /// Consulta o status atual do Isolation Forest de uma tag.
    /// </summary>
    [HttpGet("status/{clienteId}/{tagName}")]
    public async Task<IActionResult> ConsultarStatus(
        string clienteId,
        string tagName)
    {
        clienteId =
            clienteId?.Trim() ?? "";

        tagName =
            tagName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(clienteId))
        {
            return BadRequest(new
            {
                erro = "O cliente é obrigatório."
            });
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            return BadRequest(new
            {
                erro = "A tag é obrigatória."
            });
        }

        var status = await _context.ModelosTagsIaStatus
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.TipoModelo == TipoModelo
            );

        if (status == null)
        {
            return NotFound(new
            {
                erro =
                    "Ainda não existe status de Isolation Forest para esta tag.",

                clienteId,
                tagName,
                tipoModelo =
                    TipoModelo
            });
        }

        return Ok(new
        {
            status.Id,
            status.ClienteId,
            status.TagName,
            status.TipoModelo,
            status.Status,
            status.Versao,
            status.DataUltimoTreinamento,
            status.DataMarcadoDesatualizado,
            status.DataInicioTreinamento,
            status.DataFimTreinamento,
            status.QuantidadePontosTreinamento,
            status.HashJanelasTreinamento,
            status.UltimoErro
        });
    }

    /// <summary>
    /// Lista as janelas ativas que serão utilizadas no treinamento.
    /// </summary>
    [HttpGet("janelas/{clienteId}/{tagName}")]
    public async Task<IActionResult> ConsultarJanelas(
        string clienteId,
        string tagName)
    {
        clienteId =
            clienteId?.Trim() ?? "";

        tagName =
            tagName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(clienteId))
        {
            return BadRequest(new
            {
                erro = "O cliente é obrigatório."
            });
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            return BadRequest(new
            {
                erro = "A tag é obrigatória."
            });
        }

        var janelas = await _context
            .JanelasTreinamentoTagsIa
            .AsNoTracking()
            .Where(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.Ativa
            )
            .OrderBy(x =>
                x.DataHoraInicio
            )
            .ToListAsync();

        var resultado = janelas
            .Select(x => new
            {
                x.Id,
                x.ClienteId,
                x.TagName,
                x.DataHoraInicio,
                x.DataHoraFim,
                x.Descricao,
                x.ModoOperacao,
                x.Ativa,

                duracaoMinutos =
                    Math.Round(
                        (
                            x.DataHoraFim -
                            x.DataHoraInicio
                        ).TotalMinutes,
                        2
                    )
            })
            .ToList();

        return Ok(new
        {
            clienteId,
            tagName,
            quantidadeJanelas =
                resultado.Count,

            usaraFallbackHistoricoRecente =
                resultado.Count == 0,

            janelas =
                resultado
        });
    }
}

public class TreinarIsolationForestRequest
{
    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";
}