using Microsoft.EntityFrameworkCore;
using IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV04
/// Serviço operacional para cadastrar e manter grupos de análise multivariada.
/// O JSON de Benchmark não passa por este serviço.
/// </summary>
public sealed class GrupoAnaliseCadastroService
{
    private readonly AppDbContext _context;
    private readonly MultivariateRuntimeStateStore _runtimeStateStore;
    private readonly MultivariateModelPersistenceService _modelPersistenceService;

    public GrupoAnaliseCadastroService(
        AppDbContext context,
        MultivariateRuntimeStateStore runtimeStateStore,
        MultivariateModelPersistenceService modelPersistenceService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _runtimeStateStore = runtimeStateStore ??
            throw new ArgumentNullException(nameof(runtimeStateStore));
        _modelPersistenceService = modelPersistenceService ??
            throw new ArgumentNullException(nameof(modelPersistenceService));
    }

    public async Task<IReadOnlyList<GrupoAnaliseDetalheDto>> ListarAsync(
        string clienteId,
        CancellationToken cancellationToken = default)
    {
        var clienteNormalizado = NormalizarObrigatorio(clienteId, nameof(clienteId));

        var grupos = await _context.GruposAnaliseIa
            .AsNoTracking()
            .Include(x => x.Tags)
            .Where(x => x.ClienteId == clienteNormalizado)
            .OrderBy(x => x.Nome)
            .ToListAsync(cancellationToken);

        return grupos
            .Select(Mapear)
            .ToList();
    }

    public async Task<GrupoAnaliseDetalheDto?> ObterAsync(
        int id,
        string clienteId,
        CancellationToken cancellationToken = default)
    {
        var clienteNormalizado = NormalizarObrigatorio(clienteId, nameof(clienteId));

        var grupo = await _context.GruposAnaliseIa
            .AsNoTracking()
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.ClienteId == clienteNormalizado,
                cancellationToken);

        return grupo is null ? null : Mapear(grupo);
    }

    public async Task<GrupoAnaliseDetalheDto> CriarAsync(
        GrupoAnaliseSalvarRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dados = ValidarENormalizar(request);

        var nomesExistentes = await _context.GruposAnaliseIa
            .AsNoTracking()
            .Where(x => x.ClienteId == dados.ClienteId)
            .Select(x => x.Nome)
            .ToListAsync(cancellationToken);

        var nomeJaExiste = nomesExistentes.Any(nome =>
            string.Equals(nome, dados.Nome, StringComparison.OrdinalIgnoreCase));

        if (nomeJaExiste)
        {
            throw new InvalidOperationException(
                $"Já existe um grupo de análise chamado '{dados.Nome}' para o cliente '{dados.ClienteId}'.");
        }

        var grupo = new GrupoAnaliseIa
        {
            ClienteId = dados.ClienteId,
            Nome = dados.Nome,
            Descricao = dados.Descricao,
            Ativo = dados.Ativo,
            CreatedAt = DateTime.Now,
            MinimumAvailableTags = dados.MinimumAvailableTags,
            MaxLagMinutes = dados.MaxLagMinutes,
            PesoEnsemble = dados.PesoEnsemble,
            Sensibilidade = dados.Sensibilidade,
            Algoritmo = dados.Algoritmo,
            Tags = dados.Tags
                .Select(tag => new GrupoAnaliseTagIa
                {
                    TagName = tag.TagName,
                    Role = tag.Role
                })
                .ToList()
        };

        _context.GruposAnaliseIa.Add(grupo);
        await _context.SaveChangesAsync(cancellationToken);

        // Uma nova configuração não deve reutilizar vetores temporais anteriores.
        _runtimeStateStore.LimparCliente(dados.ClienteId);

        // Limpa eventual cache de "modelo ausente" de um grupo antigo com o mesmo nome.
        _modelPersistenceService.InvalidarCache(dados.ClienteId, dados.Nome);

        return Mapear(grupo);
    }

    public async Task<GrupoAnaliseDetalheDto?> AtualizarAsync(
        int id,
        GrupoAnaliseSalvarRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dados = ValidarENormalizar(request);

        var grupo = await _context.GruposAnaliseIa
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.ClienteId == dados.ClienteId,
                cancellationToken);

        if (grupo is null)
        {
            return null;
        }

        var nomeAnterior = grupo.Nome;

        var outrosGrupos = await _context.GruposAnaliseIa
            .AsNoTracking()
            .Where(x => x.Id != id && x.ClienteId == dados.ClienteId)
            .Select(x => x.Nome)
            .ToListAsync(cancellationToken);

        var nomeJaExiste = outrosGrupos.Any(nome =>
            string.Equals(nome, dados.Nome, StringComparison.OrdinalIgnoreCase));

        if (nomeJaExiste)
        {
            throw new InvalidOperationException(
                $"Já existe outro grupo de análise chamado '{dados.Nome}' para o cliente '{dados.ClienteId}'.");
        }

        grupo.Nome = dados.Nome;
        grupo.Descricao = dados.Descricao;
        grupo.Ativo = dados.Ativo;
        grupo.MinimumAvailableTags = dados.MinimumAvailableTags;
        grupo.MaxLagMinutes = dados.MaxLagMinutes;
        grupo.PesoEnsemble = dados.PesoEnsemble;
        grupo.Sensibilidade = dados.Sensibilidade;
        grupo.Algoritmo = dados.Algoritmo;

        _context.GrupoAnaliseTagsIa.RemoveRange(grupo.Tags);
        grupo.Tags = dados.Tags
            .Select(tag => new GrupoAnaliseTagIa
            {
                GrupoAnaliseIaId = grupo.Id,
                TagName = tag.TagName,
                Role = tag.Role
            })
            .ToList();

        await _context.SaveChangesAsync(cancellationToken);

        // Grupo/tags/MaxLag podem ter mudado. Descarta vetores ainda em formação.
        _runtimeStateStore.LimparCliente(dados.ClienteId);

        // Qualquer edição do grupo invalida o modelo anterior de forma fail-safe.
        // Até um novo treinamento, o detector retornará Não Aplicável em vez de
        // usar média/covariância treinadas para uma configuração antiga.
        ExcluirModeloSeExistir(dados.ClienteId, nomeAnterior);
        _modelPersistenceService.InvalidarCache(dados.ClienteId, nomeAnterior);

        if (!string.Equals(nomeAnterior, dados.Nome, StringComparison.OrdinalIgnoreCase))
        {
            _modelPersistenceService.InvalidarCache(dados.ClienteId, dados.Nome);
        }

        return Mapear(grupo);
    }

    public async Task<bool> ExcluirAsync(
        int id,
        string clienteId,
        CancellationToken cancellationToken = default)
    {
        var clienteNormalizado = NormalizarObrigatorio(clienteId, nameof(clienteId));

        var grupo = await _context.GruposAnaliseIa
            .FirstOrDefaultAsync(
                x => x.Id == id && x.ClienteId == clienteNormalizado,
                cancellationToken);

        if (grupo is null)
        {
            return false;
        }

        var nomeGrupo = grupo.Nome;

        _context.GruposAnaliseIa.Remove(grupo);
        await _context.SaveChangesAsync(cancellationToken);

        _runtimeStateStore.LimparCliente(clienteNormalizado);
        ExcluirModeloSeExistir(clienteNormalizado, nomeGrupo);
        _modelPersistenceService.InvalidarCache(clienteNormalizado, nomeGrupo);

        return true;
    }

    private void ExcluirModeloSeExistir(string clienteId, string groupName)
    {
        var caminho = _modelPersistenceService.ObterCaminhoModelo(
            clienteId,
            groupName);

        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }
    }

    private static GrupoAnaliseDetalheDto Mapear(GrupoAnaliseIa grupo)
    {
        return new GrupoAnaliseDetalheDto
        {
            Id = grupo.Id,
            ClienteId = grupo.ClienteId,
            Nome = grupo.Nome,
            Descricao = grupo.Descricao,
            Ativo = grupo.Ativo,
            CreatedAt = grupo.CreatedAt,
            MinimumAvailableTags = grupo.MinimumAvailableTags,
            MaxLagMinutes = grupo.MaxLagMinutes,
            PesoEnsemble = grupo.PesoEnsemble,
            Sensibilidade = grupo.Sensibilidade,
            Algoritmo = grupo.Algoritmo,
            Tags = grupo.Tags
                .OrderBy(x => x.Id)
                .Select(x => new GrupoAnaliseTagDto
                {
                    TagName = x.TagName,
                    Role = x.Role
                })
                .ToList()
        };
    }

    private static DadosNormalizados ValidarENormalizar(
        GrupoAnaliseSalvarRequest request)
    {
        var clienteId = NormalizarObrigatorio(request.ClienteId, nameof(request.ClienteId));
        var nome = NormalizarObrigatorio(request.Nome, nameof(request.Nome));

        var tagsRecebidas = request.Tags ?? [];

        if (tagsRecebidas.Any(x => string.IsNullOrWhiteSpace(x.TagName)))
        {
            throw new ArgumentException(
                "Todas as tags do grupo devem possuir TagName.",
                nameof(request.Tags));
        }

        var tags = tagsRecebidas
            .Select(x => new GrupoAnaliseTagSalvarRequest
            {
                TagName = x.TagName.Trim(),
                Role = string.IsNullOrWhiteSpace(x.Role)
                    ? "Input"
                    : x.Role.Trim()
            })
            .ToList();

        var tagsDuplicadas = tags
            .GroupBy(x => x.TagName, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (tagsDuplicadas.Count > 0)
        {
            throw new ArgumentException(
                $"A mesma tag não pode ser vinculada mais de uma vez ao grupo: {string.Join(", ", tagsDuplicadas)}.",
                nameof(request.Tags));
        }

        if (tags.Count < 2)
        {
            throw new ArgumentException(
                "Um grupo multivariado precisa possuir pelo menos 2 tags distintas.",
                nameof(request.Tags));
        }

        if (request.MinimumAvailableTags < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MinimumAvailableTags),
                "MinimumAvailableTags deve ser no mínimo 2.");
        }

        if (request.MinimumAvailableTags > tags.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MinimumAvailableTags),
                $"MinimumAvailableTags ({request.MinimumAvailableTags}) não pode ser maior que a quantidade de tags do grupo ({tags.Count}).");
        }

        if (request.MaxLagMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MaxLagMinutes),
                "MaxLagMinutes não pode ser negativo.");
        }

        if (request.Sensibilidade < 0 || request.Sensibilidade > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Sensibilidade),
                "Sensibilidade deve estar entre 0 e 1.");
        }

        if (request.PesoEnsemble < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PesoEnsemble),
                "PesoEnsemble não pode ser negativo.");
        }

        return new DadosNormalizados(
            clienteId,
            nome,
            string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            request.Ativo,
            request.MinimumAvailableTags,
            request.MaxLagMinutes,
            request.PesoEnsemble,
            request.Sensibilidade,
            string.IsNullOrWhiteSpace(request.Algoritmo)
                ? "Mahalanobis"
                : request.Algoritmo.Trim(),
            tags);
    }

    private static string NormalizarObrigatorio(string? valor, string parametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException("O valor é obrigatório.", parametro);
        }

        return valor.Trim();
    }

    private sealed record DadosNormalizados(
        string ClienteId,
        string Nome,
        string? Descricao,
        bool Ativo,
        int MinimumAvailableTags,
        int MaxLagMinutes,
        double PesoEnsemble,
        double Sensibilidade,
        string Algoritmo,
        List<GrupoAnaliseTagSalvarRequest> Tags);
}

public sealed class GrupoAnaliseSalvarRequest
{
    public string ClienteId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public int MinimumAvailableTags { get; set; } = 2;
    public int MaxLagMinutes { get; set; }
    public double PesoEnsemble { get; set; } = 1.0;
    public double Sensibilidade { get; set; } = 0.5;
    public string Algoritmo { get; set; } = "Mahalanobis";
    public List<GrupoAnaliseTagSalvarRequest> Tags { get; set; } = [];
}

public sealed class GrupoAnaliseTagSalvarRequest
{
    public string TagName { get; set; } = "";
    public string Role { get; set; } = "Input";
}

public sealed class GrupoAnaliseDetalheDto
{
    public int Id { get; set; }
    public string ClienteId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public bool Ativo { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MinimumAvailableTags { get; set; }
    public int MaxLagMinutes { get; set; }
    public double PesoEnsemble { get; set; }
    public double Sensibilidade { get; set; }
    public string Algoritmo { get; set; } = "";
    public List<GrupoAnaliseTagDto> Tags { get; set; } = [];
}

public sealed class GrupoAnaliseTagDto
{
    public string TagName { get; set; } = "";
    public string Role { get; set; } = "Input";
}
