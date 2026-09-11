using Microsoft.EntityFrameworkCore;

/// <summary>
/// B3.24.2 / MV04
/// Fonte operacional dos grupos de análise multivariada.
///
/// Diferente do JsonGrupoAnaliseProvider, este provider lê apenas os grupos
/// persistidos para o cliente solicitado. O JSON continua reservado ao
/// benchmark/laboratório.
/// </summary>
public sealed class DatabaseGrupoAnaliseProvider : IGrupoAnaliseProvider
{
    private readonly AppDbContext _context;

    public DatabaseGrupoAnaliseProvider(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GrupoAnaliseDto>> CarregarGruposAsync(
        string clienteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);

        var clienteNormalizado = clienteId.Trim();

        var grupos = await _context.GruposAnaliseIa
            .AsNoTracking()
            .Include(grupo => grupo.Tags)
            .Where(grupo =>
                grupo.ClienteId == clienteNormalizado &&
                grupo.Ativo)
            .OrderBy(grupo => grupo.Nome)
            .ToListAsync();

        return grupos
            .Select(grupo => new GrupoAnaliseDto
            {
                Id = grupo.Id,
                Nome = grupo.Nome,
                Descricao = grupo.Descricao,
                Algoritmo = grupo.Algoritmo,
                Sensibilidade = grupo.Sensibilidade,
                PesoEnsemble = grupo.PesoEnsemble,
                MinimumAvailableTags = grupo.MinimumAvailableTags,
                MaxLagMinutes = grupo.MaxLagMinutes,
                Tags = grupo.Tags
                    .Select(tag => tag.TagName)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();
    }
}