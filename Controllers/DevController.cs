using IAApi.Controllers;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHostEnvironment _environment;

    public DevController(AppDbContext context, IHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpPost("reset")]
    public IActionResult ResetarBancoTeste()
    {
        // P33: apaga dados de todos os clientes. So responde em Development e
        // para pedidos da propria maquina (mesma regra do Intelligence Lab);
        // fora disso, 404 sem tocar no banco.
        if (!LocalDevelopmentRequestGuard.IsAllowed(HttpContext, _environment))
        {
            return NotFound();
        }

        _context.AnomaliasDetectadas.RemoveRange(_context.AnomaliasDetectadas);
        _context.PerfisIa.RemoveRange(_context.PerfisIa);
        _context.TagsIaConfig.RemoveRange(_context.TagsIaConfig);

        _context.ProcessoTagsIa.RemoveRange(_context.ProcessoTagsIa);
        _context.ProcessoEquipamentosIa.RemoveRange(_context.ProcessoEquipamentosIa);
        _context.ProcessosIa.RemoveRange(_context.ProcessosIa);

        _context.EquipamentoTagsIa.RemoveRange(_context.EquipamentoTagsIa);
        _context.EquipamentosIa.RemoveRange(_context.EquipamentosIa);
        _context.TagDependenciasIa.RemoveRange(_context.TagDependenciasIa);
        _context.TagsContextoIa.RemoveRange(_context.TagsContextoIa);

        _context.SaveChanges();

        return Ok(new
        {
            mensagem = "Banco de teste resetado com sucesso. O histórico das tags foi mantido."
        });
    }
}
