using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.2B
///
/// Executa um backtest histórico operacional para UM grupo multivariado,
/// utilizando:
/// - configuração operacional ativa;
/// - histórico real via IHistoricoService;
/// - buffer temporal PRIVADO;
/// - modelo multivariado já persistido;
/// - MultivariateDetector existente;
/// - analisador histórico do MV08.2A.
///
/// Este serviço NÃO:
/// - altera RuntimeDecision;
/// - altera PredictionService;
/// - altera readiness;
/// - habilita promoção;
/// - escreve modelo;
/// - reutiliza o singleton de buffer do runtime.
/// </summary>
public sealed class OperationalMultivariateBacktestService
{
    private readonly IGrupoAnaliseProvider _grupoProvider;
    private readonly IHistoricoService _historicoService;
    private readonly MultivariateModelPersistenceService _modelPersistence;
    private readonly MultivariateHistoricalBacktestAnalyzer _analyzer;

    public OperationalMultivariateBacktestService(
        IGrupoAnaliseProvider grupoProvider,
        IHistoricoService historicoService,
        MultivariateModelPersistenceService modelPersistence,
        MultivariateHistoricalBacktestAnalyzer analyzer)
    {
        _grupoProvider =
            grupoProvider ??
            throw new ArgumentNullException(
                nameof(grupoProvider));

        _historicoService =
            historicoService ??
            throw new ArgumentNullException(
                nameof(historicoService));

        _modelPersistence =
            modelPersistence ??
            throw new ArgumentNullException(
                nameof(modelPersistence));

        _analyzer =
            analyzer ??
            throw new ArgumentNullException(
                nameof(analyzer));
    }

    public async Task<OperationalMultivariateBacktestResult>
        ExecutarAsync(
            string clienteId,
            string groupName,
            DateTime inicio,
            DateTime fim,
            double anomalyScoreThreshold = 75.0,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            clienteId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            groupName);

        if (fim <= inicio)
        {
            throw new ArgumentException(
                "A data final deve ser maior que a data inicial.",
                nameof(fim));
        }

        if (!double.IsFinite(
                anomalyScoreThreshold) ||
            anomalyScoreThreshold <= 0 ||
            anomalyScoreThreshold >= 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(anomalyScoreThreshold),
                "O threshold deve estar no intervalo (0, 100).");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var clienteNormalizado =
            clienteId.Trim();

        var grupoNormalizado =
            groupName.Trim();

        var grupos =
            await _grupoProvider
                .CarregarGruposAsync(
                    clienteNormalizado);

        cancellationToken.ThrowIfCancellationRequested();

        var grupo =
            grupos.FirstOrDefault(
                item =>
                    string.Equals(
                        item.Nome?.Trim(),
                        grupoNormalizado,
                        StringComparison.OrdinalIgnoreCase));

        if (grupo is null)
        {
            throw new KeyNotFoundException(
                $"O grupo multivariado ativo '{grupoNormalizado}' " +
                $"não foi encontrado para o cliente '{clienteNormalizado}'.");
        }

        var tags =
            grupo.Tags
                .Where(tag =>
                    !string.IsNullOrWhiteSpace(tag))
                .Select(tag =>
                    tag.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (tags.Count < 2)
        {
            throw new InvalidOperationException(
                $"O grupo '{grupo.Nome}' precisa possuir pelo menos duas tags.");
        }

        /*
         * Pré-carrega o modelo antes do replay.
         *
         * O persistence service mantém o objeto em cache, então o detector
         * reutiliza o mesmo snapshot durante esta execução enquanto não houver
         * invalidação externa explícita do cache.
         */
        var model =
            await _modelPersistence
                .CarregarAsync(
                    clienteNormalizado,
                    grupo.Nome,
                    cancellationToken);

        if (model is null)
        {
            throw new InvalidOperationException(
                $"Nenhum modelo multivariado treinado foi encontrado " +
                $"para o grupo '{grupo.Nome}'.");
        }

        ValidarCompatibilidadeGrupoModelo(
            clienteNormalizado,
            grupo,
            tags,
            model);

        var leiturasValidasPorTag =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var samples =
            new List<BacktestHistoricalSample>();

        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var historico =
                _historicoService
                    .BuscarHistoricoPorPeriodo(
                        clienteNormalizado,
                        tag,
                        inicio,
                        fim) ??
                [];

            /*
             * Mantém a mesma política do treinamento operacional:
             * - timestamp válido;
             * - valor finito;
             * - dentro da janela;
             * - última leitura da tag em cada minuto.
             */
            var samplesDaTag =
                historico
                    .Where(item =>
                        item is not null)
                    .Where(item =>
                        item.DataHora != default &&
                        item.DataHora >= inicio &&
                        item.DataHora <= fim &&
                        float.IsFinite(
                            item.Valor))
                    .Select(item =>
                        new BacktestHistoricalSample(
                            tag,
                            CriarMinuteIndex(
                                item.DataHora),
                            item.DataHora,
                            item.Valor))
                    .GroupBy(item =>
                        item.MinuteIndex)
                    .Select(group =>
                        group
                            .OrderBy(item =>
                                item.DataHora)
                            .Last())
                    .OrderBy(item =>
                        item.MinuteIndex)
                    .ThenBy(item =>
                        item.DataHora)
                    .ToList();

            leiturasValidasPorTag[tag] =
                samplesDaTag.Count;

            samples.AddRange(
                samplesDaTag);
        }

        if (leiturasValidasPorTag.Any(
                pair =>
                    pair.Value == 0))
        {
            var tagsSemHistorico =
                string.Join(
                    ", ",
                    leiturasValidasPorTag
                        .Where(pair =>
                            pair.Value == 0)
                        .Select(pair =>
                            pair.Key));

            throw new InvalidOperationException(
                $"Não há histórico válido na janela informada para: " +
                $"{tagsSemHistorico}.");
        }

        /*
         * Estado PRIVADO de backtest.
         *
         * O provider privado expõe apenas o grupo em análise, evitando
         * montar vetores de grupos paralelos durante o replay.
         */
        var privateProvider =
            new SingleGroupBacktestProvider(
                grupo);

        var privateGroupService =
            new GrupoAnaliseService(
                privateProvider);

        var privateStateStore =
            new MultivariateRuntimeStateStore();

        var privateBuffer =
            new MultivariateGroupBufferService(
                privateGroupService,
                privateStateStore);

        var detector =
            new MultivariateDetector(
                _modelPersistence);

        var detectionResults =
            new List<MultivariateDetectionResult>();

        long finalizedVectors = 0;
        long completeVectors = 0;
        long incompleteVectors = 0;

        foreach (var sample in samples
                     .OrderBy(item =>
                         item.MinuteIndex)
                     .ThenBy(item =>
                         item.DataHora)
                     .ThenBy(item =>
                         item.TagName,
                         StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var finalized =
                await privateBuffer
                    .AdicionarLeituraAsync(
                        clienteNormalizado,
                        sample.TagName,
                        sample.MinuteIndex,
                        sample.Value,
                        cancellationToken);

            await ProcessarFinalizadosAsync(
                finalized,
                grupo.Nome,
                detector,
                anomalyScoreThreshold,
                detectionResults,
                counters =>
                {
                    finalizedVectors +=
                        counters.Finalized;

                    completeVectors +=
                        counters.Complete;

                    incompleteVectors +=
                        counters.Incomplete;
                },
                cancellationToken);
        }

        var pending =
            privateBuffer
                .FinalizarPendentes(
                    clienteNormalizado);

        await ProcessarFinalizadosAsync(
            pending,
            grupo.Nome,
            detector,
            anomalyScoreThreshold,
            detectionResults,
            counters =>
            {
                finalizedVectors +=
                    counters.Finalized;

                completeVectors +=
                    counters.Complete;

                incompleteVectors +=
                    counters.Incomplete;
            },
            cancellationToken);

        privateBuffer.LimparCliente(
            clienteNormalizado);

        var summary =
            _analyzer.Analisar(
                detectionResults,
                model:
                    model);

        return new OperationalMultivariateBacktestResult
        {
            ClienteId =
                clienteNormalizado,

            GroupName =
                grupo.Nome,

            Inicio =
                inicio,

            Fim =
                fim,

            Threshold =
                anomalyScoreThreshold,

            ModelVersion =
                model.Version,

            ModelAlgorithm =
                model.Algorithm,

            ModelTrainedAtUtc =
                model.TrainedAtUtc,

            ModelSampleCount =
                model.SampleCount,

            Tags =
                tags,

            LeiturasValidasPorTag =
                leiturasValidasPorTag,

            TotalLeiturasValidas =
                leiturasValidasPorTag
                    .Values
                    .Sum(),

            VetoresFinalizados =
                finalizedVectors,

            VetoresCompletos =
                completeVectors,

            VetoresIncompletos =
                incompleteVectors,

            Summary =
                summary
        };
    }

    private static async Task
        ProcessarFinalizadosAsync(
            IReadOnlyList<BufferedGroupReading> finalized,
            string targetGroupName,
            MultivariateDetector detector,
            double threshold,
            ICollection<MultivariateDetectionResult> results,
            Action<BacktestVectorCounters> updateCounters,
            CancellationToken cancellationToken)
    {
        long finalizedCount = 0;
        long completeCount = 0;
        long incompleteCount = 0;

        foreach (var reading in finalized)
        {
            if (!string.Equals(
                    reading.GroupName,
                    targetGroupName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            finalizedCount++;

            if (reading.EstaCompleto())
            {
                completeCount++;
            }
            else
            {
                incompleteCount++;
            }

            var result =
                await detector
                    .DetectarAsync(
                        reading,
                        threshold,
                        cancellationToken);

            results.Add(
                result);
        }

        updateCounters(
            new BacktestVectorCounters(
                finalizedCount,
                completeCount,
                incompleteCount));
    }

    private static void
        ValidarCompatibilidadeGrupoModelo(
            string clienteId,
            GrupoAnaliseDto grupo,
            IReadOnlyCollection<string> groupTags,
            MultivariateGroupModel model)
    {
        if (!string.Equals(
                model.ClienteId?.Trim(),
                clienteId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O cliente do modelo não corresponde ao cliente do backtest.");
        }

        if (!string.Equals(
                model.GroupName?.Trim(),
                grupo.Nome?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O grupo do modelo não corresponde ao grupo do backtest.");
        }

        var modelTags =
            new HashSet<string>(
                model.Tags ?? [],
                StringComparer.OrdinalIgnoreCase);

        var configuredTags =
            new HashSet<string>(
                groupTags,
                StringComparer.OrdinalIgnoreCase);

        if (!modelTags.SetEquals(
                configuredTags))
        {
            throw new InvalidOperationException(
                "As tags do modelo não correspondem às tags configuradas " +
                "atualmente para o grupo.");
        }

        if (model.MinimumAvailableTags !=
            Math.Max(
                2,
                grupo.MinimumAvailableTags))
        {
            throw new InvalidOperationException(
                "MinimumAvailableTags do modelo diverge da configuração atual.");
        }

        if (model.MaxLagMinutes !=
            Math.Max(
                0,
                grupo.MaxLagMinutes))
        {
            throw new InvalidOperationException(
                "MaxLagMinutes do modelo diverge da configuração atual.");
        }
    }

    private static long CriarMinuteIndex(
        DateTime dataHora)
    {
        return
            dataHora.Ticks /
            TimeSpan.TicksPerMinute;
    }

    private sealed record BacktestHistoricalSample(
        string TagName,
        long MinuteIndex,
        DateTime DataHora,
        double Value);

    private sealed record BacktestVectorCounters(
        long Finalized,
        long Complete,
        long Incomplete);

    private sealed class SingleGroupBacktestProvider
        : IGrupoAnaliseProvider
    {
        private readonly GrupoAnaliseDto _group;

        public SingleGroupBacktestProvider(
            GrupoAnaliseDto group)
        {
            _group =
                new GrupoAnaliseDto
                {
                    Id =
                        group.Id,

                    Nome =
                        group.Nome,

                    MinimumAvailableTags =
                        group.MinimumAvailableTags,

                    MaxLagMinutes =
                        group.MaxLagMinutes,

                    Tags =
                        group.Tags
                            .ToList()
                };
        }

        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            return Task.FromResult(
                new List<GrupoAnaliseDto>
                {
                    new()
                    {
                        Id =
                            _group.Id,

                        Nome =
                            _group.Nome,

                        MinimumAvailableTags =
                            _group.MinimumAvailableTags,

                        MaxLagMinutes =
                            _group.MaxLagMinutes,

                        Tags =
                            _group.Tags
                                .ToList()
                    }
                });
        }
    }
}

public sealed class OperationalMultivariateBacktestResult
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public DateTime Inicio { get; init; }

    public DateTime Fim { get; init; }

    public double Threshold { get; init; }

    public int ModelVersion { get; init; }

    public string ModelAlgorithm { get; init; } =
        "";

    public DateTime ModelTrainedAtUtc { get; init; }

    public long ModelSampleCount { get; init; }

    public IReadOnlyList<string> Tags { get; init; } =
        Array.Empty<string>();

    public IReadOnlyDictionary<string, int>
        LeiturasValidasPorTag { get; init; } =
            new Dictionary<string, int>();

    public int TotalLeiturasValidas { get; init; }

    public long VetoresFinalizados { get; init; }

    public long VetoresCompletos { get; init; }

    public long VetoresIncompletos { get; init; }

    public MultivariateHistoricalBacktestSummary Summary { get; init; } =
        new();
}
