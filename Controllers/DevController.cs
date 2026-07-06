using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly AppDbContext _context;

    public DevController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("reset")]
    public IActionResult ResetarBancoTeste()
    {
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