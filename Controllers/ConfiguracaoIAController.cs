using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/configuracao-ia")]
public class ConfiguracaoIAController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConfiguracaoIAController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{clienteId}")]
    public async Task<IActionResult> Obter(string clienteId)
    {
        var configuracao = await _context.ConfiguracoesIA
            .FirstOrDefaultAsync(x => x.ClienteId == clienteId);

        if (configuracao == null)
        {
            configuracao = new ConfiguracaoIA
            {
                ClienteId = clienteId
            };

            _context.ConfiguracoesIA.Add(configuracao);
            await _context.SaveChangesAsync();
        }

        return Ok(configuracao);
    }

    [HttpPut("{clienteId}")]
    public async Task<IActionResult> Atualizar(
        string clienteId,
        [FromBody] ConfiguracaoIARequest request)
    {
        var configuracao = await _context.ConfiguracoesIA
            .FirstOrDefaultAsync(x => x.ClienteId == clienteId);

        if (configuracao == null)
        {
            configuracao = new ConfiguracaoIA
            {
                ClienteId = clienteId,
                DataCriacao = DateTime.Now
            };

            _context.ConfiguracoesIA.Add(configuracao);
        }

        configuracao.HorasHistorico = request.HorasHistorico;
        configuracao.Sensibilidade = request.Sensibilidade;
        configuracao.ConfiancaMinima = request.ConfiancaMinima;
        configuracao.AprendizadoAtivo = request.AprendizadoAtivo;
        configuracao.NotificacaoPopup = request.NotificacaoPopup;
        configuracao.NotificacaoEmail = request.NotificacaoEmail;
        configuracao.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Configuração da IA atualizada com sucesso.",
            configuracao
        });
    }
}

public class ConfiguracaoIARequest
{
    public int HorasHistorico { get; set; } = 24;
    public int Sensibilidade { get; set; } = 70;
    public int ConfiancaMinima { get; set; } = 80;

    public bool AprendizadoAtivo { get; set; } = true;

    public bool NotificacaoPopup { get; set; } = true;
    public bool NotificacaoEmail { get; set; } = false;
}