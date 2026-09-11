using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Controller que retorna o histórico de anomalias para um cliente específico.
// Ele consulta o banco de dados e devolve os registros ordenados por data de detecção.
[ApiController]
[Route("api/anomalias-historico")]
public class AnomaliaHistoricoController : ControllerBase
{
    private readonly AppDbContext _context;

    public AnomaliaHistoricoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{clienteId}")]
    // Endpoint: GET /api/anomalias-historico/{clienteId}
    // Retorna todos os registros de anomalias detectadas para o cliente,
    // incluindo dependências relacionadas, ordenados da mais recente para a mais antiga.
    public IActionResult ListarPorCliente(string clienteId)
    {
        var historico = _context.AnomaliasDetectadas
            .Include(x => x.Dependencias)
            .Where(x => x.ClienteId == clienteId && x.EhAnomalia)
            .OrderByDescending(x => x.DataDeteccao)
            .ToList();

        return Ok(historico);
    }
}