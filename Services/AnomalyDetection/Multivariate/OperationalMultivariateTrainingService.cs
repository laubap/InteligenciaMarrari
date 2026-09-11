using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV05
/// Treinamento multivariado operacional baseado em:
/// - grupo persistido no banco;
/// - histórico real fornecido por IHistoricoService (Bridge/PowerServer em produção);
/// - mesma formação temporal de vetores usada pelo runtime multivariado.
///
/// O treinamento usa um estado de buffer privado para não interferir nos
/// vetores que o monitoramento online esteja formando no singleton de runtime.
/// O MultivariateTrainingService legado continua reservado ao benchmark.
/// </summary>
public sealed class OperationalMultivariateTrainingService
{
    private readonly AppDbContext _context;
    private readonly IHistoricoService _historicoService;
    private readonly GrupoAnaliseService _grupoAnaliseService;
    private readonly MultivariateModelPersistenceService _modelPersistenceService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OperationalMultivariateTrainingService(
        AppDbContext context,
        IHistoricoService historicoService,
        GrupoAnaliseService grupoAnaliseService,
        MultivariateModelPersistenceService modelPersistenceService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _historicoService = historicoService ??
            throw new ArgumentNullException(nameof(historicoService));
        _grupoAnaliseService = grupoAnaliseService ??
            throw new ArgumentNullException(nameof(grupoAnaliseService));
        _modelPersistenceService = modelPersistenceService ??
            throw new ArgumentNullException(nameof(modelPersistenceService));
    }

    public async Task<OperationalMultivariateTrainingResult> TreinarAsync(
        int grupoId,
        string clienteId,
        DateTime inicio,
        DateTime fim,
        CancellationToken cancellationToken = default)
    {
        if (grupoId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grupoId),
                "O id do grupo deve ser maior que zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);

        if (fim <= inicio)
        {
            throw new ArgumentException(
                "A data final da janela deve ser maior que a data inicial.",
                nameof(fim));
        }

        var clienteNormalizado = clienteId.Trim();

        var grupo = await _context.GruposAnaliseIa
            .AsNoTracking()
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(
                x => x.Id == grupoId &&
                     x.ClienteId == clienteNormalizado,
                cancellationToken);

        if (grupo is null)
        {
            throw new KeyNotFoundException(
                $"O grupo de análise {grupoId} não foi encontrado para o cliente '{clienteNormalizado}'.");
        }

        if (!grupo.Ativo)
        {
            throw new InvalidOperationException(
                $"O grupo '{grupo.Nome}' está inativo e não pode ser treinado.");
        }

        var tags = grupo.Tags
            .Select(x => x.TagName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count < 2)
        {
            throw new InvalidOperationException(
                $"O grupo '{grupo.Nome}' precisa possuir pelo menos duas tags para treinamento multivariado.");
        }

        var leiturasPorTag = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        var amostras = new List<OperationalTrainingSample>();

        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var historico = _historicoService.BuscarHistoricoPorPeriodo(
                clienteNormalizado,
                tag,
                inicio,
                fim) ?? [];

            var amostrasDaTag = historico
                .Where(x => x is not null)
                .Where(x =>
                    x.DataHora != default &&
                    x.DataHora >= inicio &&
                    x.DataHora <= fim &&
                    float.IsFinite(x.Valor))
                .Select(x => new OperationalTrainingSample(
                    tag,
                    CriarMinuteIndex(x.DataHora),
                    x.DataHora,
                    x.Valor))
                // Se houver mais de uma leitura da mesma tag no mesmo minuto,
                // usa a última leitura industrial daquele minuto.
                .GroupBy(x => x.MinuteIndex)
                .Select(x => x
                    .OrderBy(y => y.DataHora)
                    .Last())
                .OrderBy(x => x.MinuteIndex)
                .ToList();

            leiturasPorTag[tag] = amostrasDaTag.Count;
            amostras.AddRange(amostrasDaTag);
        }

        if (leiturasPorTag.Any(x => x.Value == 0))
        {
            var ausentes = string.Join(
                ", ",
                leiturasPorTag
                    .Where(x => x.Value == 0)
                    .Select(x => x.Key));

            throw new InvalidOperationException(
                $"Não há histórico válido na janela informada para: {ausentes}.");
        }

        /*
         * Estado PRIVADO de treinamento.
         * Não usar o MultivariateRuntimeStateStore singleton da aplicação aqui.
         */
        var trainingStateStore = new MultivariateRuntimeStateStore();
        var trainingBuffer = new MultivariateGroupBufferService(
            _grupoAnaliseService,
            trainingStateStore);

        var acumulador = new OnlineMultivariateAccumulator(
            grupo.Nome,
            tags);

        long vetoresFinalizados = 0;
        long vetoresIncompletos = 0;
        long vetoresInvalidos = 0;

        foreach (var sample in amostras
                     .OrderBy(x => x.MinuteIndex)
                     .ThenBy(x => x.DataHora)
                     .ThenBy(x => x.TagName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var finalizados = await trainingBuffer.AdicionarLeituraAsync(
                clienteNormalizado,
                sample.TagName,
                sample.MinuteIndex,
                sample.Value,
                cancellationToken);

            ProcessarFinalizados(
                finalizados,
                grupo.Nome,
                acumulador,
                ref vetoresFinalizados,
                ref vetoresIncompletos,
                ref vetoresInvalidos);
        }

        var pendentes = trainingBuffer.FinalizarPendentes(clienteNormalizado);

        ProcessarFinalizados(
            pendentes,
            grupo.Nome,
            acumulador,
            ref vetoresFinalizados,
            ref vetoresIncompletos,
            ref vetoresInvalidos);

        trainingBuffer.LimparCliente(clienteNormalizado);

        if (acumulador.SampleCount < 2)
        {
            throw new InvalidOperationException(
                $"O grupo '{grupo.Nome}' produziu apenas {acumulador.SampleCount} vetor(es) completo(s). " +
                "São necessários pelo menos 2 vetores completos para calcular a covariância.");
        }

        var model = acumulador.CriarModelo(
            clienteNormalizado,
            grupo.MinimumAvailableTags,
            grupo.MaxLagMinutes);

        var modelPath = _modelPersistenceService.ObterCaminhoModelo(
            clienteNormalizado,
            grupo.Nome);

        var directory = Path.GetDirectoryName(modelPath)
            ?? throw new InvalidOperationException(
                "Não foi possível determinar o diretório do modelo multivariado.");

        Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(
            modelPath,
            JsonSerializer.Serialize(model, JsonOptions),
            cancellationToken);

        // Garante que a próxima inferência não reutilize modelo/cache anterior.
        _modelPersistenceService.InvalidarCache(
            clienteNormalizado,
            grupo.Nome);

        return new OperationalMultivariateTrainingResult
        {
            ClienteId = clienteNormalizado,
            GrupoId = grupo.Id,
            GrupoNome = grupo.Nome,
            Inicio = inicio,
            Fim = fim,
            Tags = tags,
            LeiturasValidasPorTag = leiturasPorTag,
            TotalLeiturasValidas = leiturasPorTag.Values.Sum(),
            VetoresFinalizados = vetoresFinalizados,
            VetoresCompletosUsados = model.SampleCount,
            VetoresIncompletosIgnorados = vetoresIncompletos,
            VetoresInvalidosIgnorados = vetoresInvalidos,
            MinimumAvailableTags = model.MinimumAvailableTags,
            MaxLagMinutes = model.MaxLagMinutes,
            ModeloGerado = true,
            CaminhoModelo = modelPath,
            TreinadoEmUtc = model.TrainedAtUtc
        };
    }

    private static void ProcessarFinalizados(
        IReadOnlyList<BufferedGroupReading> finalizados,
        string targetGroupName,
        OnlineMultivariateAccumulator acumulador,
        ref long vetoresFinalizados,
        ref long vetoresIncompletos,
        ref long vetoresInvalidos)
    {
        foreach (var leitura in finalizados)
        {
            if (!string.Equals(
                    leitura.GroupName,
                    targetGroupName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            vetoresFinalizados++;

            // Mantém exatamente a regra do treinamento benchmark:
            // a matriz-base é treinada somente com vetores completos.
            if (!leitura.EstaCompleto())
            {
                vetoresIncompletos++;
                continue;
            }

            if (!acumulador.TryAdd(leitura.Valores))
            {
                vetoresInvalidos++;
            }
        }
    }

    private static long CriarMinuteIndex(DateTime dataHora)
    {
        // O buffer precisa apenas de um índice monotônico em minutos.
        // Ticks evita depender de timezone/Unix epoch para DateTime Unspecified.
        return dataHora.Ticks / TimeSpan.TicksPerMinute;
    }

    private sealed record OperationalTrainingSample(
        string TagName,
        long MinuteIndex,
        DateTime DataHora,
        double Value);
}

public sealed class OperationalMultivariateTrainingResult
{
    public string ClienteId { get; set; } = "";
    public int GrupoId { get; set; }
    public string GrupoNome { get; set; } = "";
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, int> LeiturasValidasPorTag { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public int TotalLeiturasValidas { get; set; }
    public long VetoresFinalizados { get; set; }
    public long VetoresCompletosUsados { get; set; }
    public long VetoresIncompletosIgnorados { get; set; }
    public long VetoresInvalidosIgnorados { get; set; }
    public int MinimumAvailableTags { get; set; }
    public int MaxLagMinutes { get; set; }
    public bool ModeloGerado { get; set; }
    public string CaminhoModelo { get; set; } = "";
    public DateTime TreinadoEmUtc { get; set; }
}
