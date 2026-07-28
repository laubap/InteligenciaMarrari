using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IAApi.Services;

public class TagContextoIaService
{
    private readonly AppDbContext _context;

    public TagContextoIaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TagContextoIa?> ObterAsync(
        string clienteId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        return await _context.TagsContextoIa
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ClienteId == clienteId &&
                     x.TagName == tagName,
                cancellationToken);
    }

    public async Task<TagContextoIa> SalvarAsync(
        TagContextoRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidarRequest(request);

        var contexto = await _context.TagsContextoIa
            .FirstOrDefaultAsync(
                x => x.ClienteId == request.ClienteId &&
                     x.TagName == request.TagName,
                cancellationToken);

        if (contexto == null)
        {
            contexto = new TagContextoIa
            {
                ClienteId = request.ClienteId.Trim(),
                TagName = request.TagName.Trim(),
                DataCriacao = DateTime.Now
            };

            _context.TagsContextoIa.Add(contexto);
        }

        contexto.TipoRepresentacao = request.TipoRepresentacao;
        contexto.Descricao = request.Descricao;
        contexto.Criticidade = request.Criticidade;
        contexto.Equipamento = request.Equipamento;
        contexto.Area = request.Area;
        contexto.ObservacaoModoOperacao = request.ObservacaoModoOperacao;

        contexto.Impactos = SerializarLista(request.Impactos);
        contexto.ModosOperacao = SerializarLista(request.ModosOperacao);
        contexto.CausasProvaveis = SerializarLista(request.CausasProvaveis);
        contexto.AcoesRecomendadas = SerializarLista(request.AcoesRecomendadas);

        contexto.ContextoCompleto =
            !string.IsNullOrWhiteSpace(request.Descricao) &&
            !string.IsNullOrWhiteSpace(request.Criticidade);

        contexto.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);

        return contexto;
    }

    private static string SerializarLista(List<string>? valores)
    {
        var listaLimpa = valores?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        return JsonSerializer.Serialize(listaLimpa);
    }

    private static void ValidarRequest(TagContextoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClienteId))
        {
            throw new ArgumentException(
                "O ClienteId é obrigatório.",
                nameof(request.ClienteId));
        }

        if (string.IsNullOrWhiteSpace(request.TagName))
        {
            throw new ArgumentException(
                "O TagName é obrigatório.",
                nameof(request.TagName));
        }
    }
}