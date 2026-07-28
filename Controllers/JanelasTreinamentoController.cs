using IAApi.Services.ModelTraining;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/janelas-treinamento")]
public class JanelasTreinamentoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ModeloTagStatusService _statusService;

    public JanelasTreinamentoController(
        AppDbContext context,
        ModeloTagStatusService statusService)
    {
        _context = context
            ?? throw new ArgumentNullException(nameof(context));

        _statusService = statusService
            ?? throw new ArgumentNullException(nameof(statusService));
    }

    [HttpGet("{clienteId}/{tagName}")]
    public async Task<IActionResult> Listar(
        string clienteId,
        string tagName,
        [FromQuery] bool somenteAtivas = true)
    {
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

        var clienteNormalizado = clienteId.Trim();
        var tagNormalizada = tagName.Trim();

        var query = _context.JanelasTreinamentoTagsIa
            .AsNoTracking()
            .Where(x =>
                x.ClienteId == clienteNormalizado &&
                x.TagName == tagNormalizada);

        if (somenteAtivas)
        {
            query = query.Where(x => x.Ativa);
        }

        var registros = await query
            .OrderBy(x => x.DataHoraInicio)
            .ToListAsync();

        var janelas = registros
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
                x.DataCadastro,
                x.DataAtualizacao,

                duracaoMinutos =
                    Math.Round(
                        (x.DataHoraFim - x.DataHoraInicio)
                        .TotalMinutes,
                        2)
            })
            .ToList();

        return Ok(new
        {
            clienteId = clienteNormalizado,
            tagName = tagNormalizada,
            somenteAtivas,
            quantidade = janelas.Count,
            janelas
        });
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarJanelaTreinamentoRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                erro = "Os dados da janela são obrigatórios."
            });
        }

        var clienteId = request.ClienteId.Trim();
        var tagName = request.TagName.Trim();

        var erro = ValidarRequest(
            clienteId,
            tagName,
            request.DataHoraInicio,
            request.DataHoraFim
        );

        if (erro != null)
        {
            return BadRequest(new { erro });
        }

        var existeSobreposicao = await _context
            .JanelasTreinamentoTagsIa
            .AnyAsync(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.Ativa &&
                request.DataHoraInicio < x.DataHoraFim &&
                request.DataHoraFim > x.DataHoraInicio);

        if (existeSobreposicao)
        {
            return BadRequest(new
            {
                erro =
                    "A janela informada se sobrepõe a outra janela ativa desta tag."
            });
        }

        var janela = new JanelaTreinamentoTagIa
        {
            ClienteId = clienteId,
            TagName = tagName,
            DataHoraInicio = request.DataHoraInicio,
            DataHoraFim = request.DataHoraFim,
            Descricao = LimparTextoOpcional(request.Descricao),
            ModoOperacao = LimparTextoOpcional(request.ModoOperacao),
            Ativa = true,
            DataCadastro = DateTime.Now,
            DataAtualizacao = DateTime.Now
        };

        _context.JanelasTreinamentoTagsIa.Add(janela);
        await _context.SaveChangesAsync();

        await _statusService.MarcarTodosComoDesatualizados(
            janela.ClienteId,
            janela.TagName
        );

        return CreatedAtAction(
            nameof(Listar),
            new
            {
                clienteId = janela.ClienteId,
                tagName = janela.TagName
            },
            new
            {
                mensagem =
                    "Janela de treinamento criada com sucesso. Os modelos da tag foram marcados como desatualizados.",

                janela = new
                {
                    janela.Id,
                    janela.ClienteId,
                    janela.TagName,
                    janela.DataHoraInicio,
                    janela.DataHoraFim,
                    janela.Descricao,
                    janela.ModoOperacao,
                    janela.Ativa,
                    janela.DataCadastro,

                    duracaoMinutos =
                        Math.Round(
                            (
                                janela.DataHoraFim -
                                janela.DataHoraInicio
                            ).TotalMinutes,
                            2)
                }
            }
        );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(
        int id,
        [FromBody] AtualizarJanelaTreinamentoRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                erro = "Os dados da janela são obrigatórios."
            });
        }

        var janela = await _context.JanelasTreinamentoTagsIa
            .FirstOrDefaultAsync(x => x.Id == id);

        if (janela == null)
        {
            return NotFound(new
            {
                erro = "Janela de treinamento não encontrada."
            });
        }

        var erro = ValidarRequest(
            janela.ClienteId,
            janela.TagName,
            request.DataHoraInicio,
            request.DataHoraFim
        );

        if (erro != null)
        {
            return BadRequest(new { erro });
        }

        var existeSobreposicao = await _context
            .JanelasTreinamentoTagsIa
            .AnyAsync(x =>
                x.Id != id &&
                x.ClienteId == janela.ClienteId &&
                x.TagName == janela.TagName &&
                x.Ativa &&
                request.DataHoraInicio < x.DataHoraFim &&
                request.DataHoraFim > x.DataHoraInicio);

        if (existeSobreposicao)
        {
            return BadRequest(new
            {
                erro =
                    "A janela informada se sobrepõe a outra janela ativa desta tag."
            });
        }

        janela.DataHoraInicio = request.DataHoraInicio;
        janela.DataHoraFim = request.DataHoraFim;
        janela.Descricao = LimparTextoOpcional(request.Descricao);
        janela.ModoOperacao = LimparTextoOpcional(request.ModoOperacao);
        janela.Ativa = request.Ativa;
        janela.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        await _statusService.MarcarTodosComoDesatualizados(
            janela.ClienteId,
            janela.TagName
        );

        return Ok(new
        {
            mensagem =
                "Janela atualizada com sucesso. Os modelos da tag foram marcados como desatualizados.",

            janela = new
            {
                janela.Id,
                janela.ClienteId,
                janela.TagName,
                janela.DataHoraInicio,
                janela.DataHoraFim,
                janela.Descricao,
                janela.ModoOperacao,
                janela.Ativa,
                janela.DataCadastro,
                janela.DataAtualizacao,

                duracaoMinutos =
                    Math.Round(
                        (
                            janela.DataHoraFim -
                            janela.DataHoraInicio
                        ).TotalMinutes,
                        2)
            }
        });
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<IActionResult> Desativar(int id)
    {
        var janela = await _context.JanelasTreinamentoTagsIa
            .FirstOrDefaultAsync(x => x.Id == id);

        if (janela == null)
        {
            return NotFound(new
            {
                erro = "Janela de treinamento não encontrada."
            });
        }

        if (!janela.Ativa)
        {
            return Ok(new
            {
                mensagem = "A janela já estava desativada.",
                janela.Id,
                janela.ClienteId,
                janela.TagName
            });
        }

        janela.Ativa = false;
        janela.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        await _statusService.MarcarTodosComoDesatualizados(
            janela.ClienteId,
            janela.TagName
        );

        return Ok(new
        {
            mensagem =
                "Janela desativada com sucesso. Os modelos da tag foram marcados como desatualizados.",

            janela.Id,
            janela.ClienteId,
            janela.TagName,
            janela.Ativa,
            janela.DataAtualizacao
        });
    }

    [HttpPatch("{id:int}/ativar")]
    public async Task<IActionResult> Ativar(int id)
    {
        var janela = await _context.JanelasTreinamentoTagsIa
            .FirstOrDefaultAsync(x => x.Id == id);

        if (janela == null)
        {
            return NotFound(new
            {
                erro = "Janela de treinamento não encontrada."
            });
        }

        if (janela.Ativa)
        {
            return Ok(new
            {
                mensagem = "A janela já estava ativa.",
                janela.Id,
                janela.ClienteId,
                janela.TagName
            });
        }

        var existeSobreposicao = await _context
            .JanelasTreinamentoTagsIa
            .AnyAsync(x =>
                x.Id != janela.Id &&
                x.ClienteId == janela.ClienteId &&
                x.TagName == janela.TagName &&
                x.Ativa &&
                janela.DataHoraInicio < x.DataHoraFim &&
                janela.DataHoraFim > x.DataHoraInicio);

        if (existeSobreposicao)
        {
            return BadRequest(new
            {
                erro =
                    "Esta janela não pode ser ativada porque se sobrepõe a outra janela ativa da mesma tag."
            });
        }

        janela.Ativa = true;
        janela.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        await _statusService.MarcarTodosComoDesatualizados(
            janela.ClienteId,
            janela.TagName
        );

        return Ok(new
        {
            mensagem =
                "Janela ativada com sucesso. Os modelos da tag foram marcados como desatualizados.",

            janela.Id,
            janela.ClienteId,
            janela.TagName,
            janela.Ativa,
            janela.DataAtualizacao
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id)
    {
        var janela = await _context.JanelasTreinamentoTagsIa
            .FirstOrDefaultAsync(x => x.Id == id);

        if (janela == null)
        {
            return NotFound(new
            {
                erro = "Janela de treinamento não encontrada."
            });
        }

        var clienteId = janela.ClienteId;
        var tagName = janela.TagName;

        _context.JanelasTreinamentoTagsIa.Remove(janela);
        await _context.SaveChangesAsync();

        await _statusService.MarcarTodosComoDesatualizados(
            clienteId,
            tagName
        );

        return Ok(new
        {
            mensagem =
                "Janela excluída com sucesso. Os modelos da tag foram marcados como desatualizados.",

            id,
            clienteId,
            tagName
        });
    }

    private static string? ValidarRequest(
        string clienteId,
        string tagName,
        DateTime inicio,
        DateTime fim)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            return "O cliente é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            return "A tag é obrigatória.";
        }

        if (inicio == default || fim == default)
        {
            return "As datas de início e fim são obrigatórias.";
        }

        if (fim <= inicio)
        {
            return "A data final deve ser maior que a data inicial.";
        }

        return null;
    }

    private static string? LimparTextoOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }
}

public class CriarJanelaTreinamentoRequest
{
    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public DateTime DataHoraInicio { get; set; }

    public DateTime DataHoraFim { get; set; }

    public string? Descricao { get; set; }

    public string? ModoOperacao { get; set; }
}

public class AtualizarJanelaTreinamentoRequest
{
    public DateTime DataHoraInicio { get; set; }

    public DateTime DataHoraFim { get; set; }

    public string? Descricao { get; set; }

    public string? ModoOperacao { get; set; }

    public bool Ativa { get; set; } = true;
}