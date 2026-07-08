using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/categorias-falha-ia")]
public class CategoriasFalhaIAController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriasFalhaIAController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{clienteId}")]
    public async Task<IActionResult> Listar(string clienteId)
    {
        var categorias = await _context.CategoriasFalhaIA
            .Where(x => x.ClienteId == clienteId && x.Ativa)
            .OrderBy(x => x.Nome)
            .Select(x => new
            {
                x.Id,
                x.ClienteId,
                x.Nome,
                x.Descricao,
                x.Ativa,
                x.DataCriacao,
                x.DataAtualizacao,
                TotalMotivos = x.Motivos.Count(m => m.Ativo)
            })
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CategoriaFalhaIARequest request)
    {
        var categoria = new CategoriaFalhaIA
        {
            ClienteId = request.ClienteId,
            Nome = request.Nome,
            Descricao = request.Descricao,
            Ativa = true,
            DataCriacao = DateTime.Now,
            DataAtualizacao = DateTime.Now
        };

        _context.CategoriasFalhaIA.Add(categoria);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Categoria de falha criada com sucesso.",
            categoria
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(
        int id,
        [FromBody] CategoriaFalhaIARequest request)
    {
        var categoria = await _context.CategoriasFalhaIA
            .FirstOrDefaultAsync(x => x.Id == id);

        if (categoria == null)
            return NotFound(new { erro = "Categoria não encontrada." });

        categoria.Nome = request.Nome;
        categoria.Descricao = request.Descricao;
        categoria.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Categoria atualizada com sucesso.",
            categoria
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desativar(int id)
    {
        var categoria = await _context.CategoriasFalhaIA
            .FirstOrDefaultAsync(x => x.Id == id);

        if (categoria == null)
            return NotFound(new { erro = "Categoria não encontrada." });

        categoria.Ativa = false;
        categoria.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Categoria desativada com sucesso."
        });
    }
}

public class CategoriaFalhaIARequest
{
    public string ClienteId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
}