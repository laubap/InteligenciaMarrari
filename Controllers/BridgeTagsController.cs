using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/tags/bridge")]
public class BridgeTagsController : ControllerBase
{
    private readonly BridgeTagsService _bridgeTagsService;

    public BridgeTagsController(BridgeTagsService bridgeTagsService)
    {
        _bridgeTagsService = bridgeTagsService;
    }

    [HttpGet("{clienteId}")]
    public async Task<IActionResult> ListarTagsDoCliente(string clienteId)
    {
        try
        {
            var json = await _bridgeTagsService.BuscarTagsCliente(clienteId);

            var resposta = JsonSerializer.Deserialize<BridgeTagsResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            return Ok(resposta?.Tags ?? new List<BridgeTagDto>());
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                erro = "Erro ao buscar tags no Bridge.",
                detalhe = ex.Message
            });
        }
    }
}

public class BridgeTagsResponse
{
    public string Cliente { get; set; } = "";
    public int Total { get; set; }
    public List<BridgeTagDto> Tags { get; set; } = new();
}

public class BridgeTagDto
{
    public string FullName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Apelido { get; set; } = "";
    public string Unidade { get; set; } = "";
    public string Descricao { get; set; } = "";
}