using Microsoft.EntityFrameworkCore;

// Serviço responsável por localizar o processo principal associado a uma tag.
// Se nenhum vínculo estiver explicitamente marcado como Principal=true,
// o serviço retorna null em vez de assumir um processo arbitrariamente.
public class ProcessoAnaliseService
{
    private readonly AppDbContext _context;

    public ProcessoAnaliseService(AppDbContext context)
    {
        _context = context;
    }

    public ProcessoIa? BuscarProcessoDaTag(
        string clienteId,
        string tagName)
    {
        var processoTag = _context.ProcessoTagsIa
            .AsNoTracking()
            .Include(x => x.ProcessoIa)
            .FirstOrDefault(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.Principal);

        return processoTag?.ProcessoIa;
    }
}