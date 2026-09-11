using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/grupos-analise")]
public sealed class GrupoAnaliseController : ControllerBase
{
    private readonly GrupoAnaliseCadastroService _service;

    public GrupoAnaliseController(GrupoAnaliseCadastroService service)
    {
        _service = service;
    }

    /// <summary>
    /// Cria um grupo multivariado e vincula as tags selecionadas pelo cliente.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GrupoAnaliseDetalheDto>> Criar(
        [FromBody] GrupoAnaliseSalvarRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var grupo = await _service.CriarAsync(request, cancellationToken);

            return CreatedAtAction(
                nameof(ObterPorId),
                new { clienteId = grupo.ClienteId, id = grupo.Id },
                grupo);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    /// <summary>
    /// Lista grupos do cliente, inclusive inativos, para permitir administração no front.
    /// </summary>
    [HttpGet("cliente/{clienteId}")]
    public async Task<ActionResult<IReadOnlyList<GrupoAnaliseDetalheDto>>> Listar(
        string clienteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var grupos = await _service.ListarAsync(clienteId, cancellationToken);
            return Ok(grupos);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpGet("cliente/{clienteId}/{id:int}")]
    public async Task<ActionResult<GrupoAnaliseDetalheDto>> ObterPorId(
        string clienteId,
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var grupo = await _service.ObterAsync(id, clienteId, cancellationToken);

            return grupo is null
                ? NotFound()
                : Ok(grupo);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GrupoAnaliseDetalheDto>> Atualizar(
        int id,
        [FromBody] GrupoAnaliseSalvarRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var grupo = await _service.AtualizarAsync(id, request, cancellationToken);

            return grupo is null
                ? NotFound()
                : Ok(grupo);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(
        int id,
        [FromQuery] string clienteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var excluido = await _service.ExcluirAsync(id, clienteId, cancellationToken);

            return excluido
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}