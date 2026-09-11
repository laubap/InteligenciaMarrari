using System.Diagnostics;
using System.Text.Json;
using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Benchmark.Diagnostics;
using IAApi.Benchmark.RegimeShadow;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.PersistenceCounterfactual;
using IAApi.Benchmark.PersistenceExperimental;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;

namespace IAApi.Benchmark;

/// <summary>
/// Reproduz cronologicamente as leituras do SQLite e executa:
/// 1. os detectores univariados já existentes, com histórico por tag;
/// 2. o detector multivariado, com grupos de tags sincronizados por minuto.
///
/// As duas análises são mantidas em paralelo.
/// </summary>
public class BenchmarkRunner
{
    private const string TipoTagPadrao = "perfil-comportamental";
    private const long IntervaloProgresso = 500_000;
    private const long IntervaloFlush = 500_000;
    private const double PesoDetectorMultivariado = 0.25;

    private static readonly string[] TagsBenchmark =
    {
        "DRYCHAMBER01.MATERIAL_TEMPERATURE",
        "DRYCHAMBER01.MATERIAL_HUMIDITY",
        "DRYCHAMBER02.MATERIAL_TEMPERATURE",
        "DRYCHAMBER02.MATERIAL_HUMIDITY",
        "DRYCHAMBER03.MATERIAL_TEMPERATURE",
        "DRYCHAMBER04.MATERIAL_TEMPERATURE",
        "DRYCHAMBER04.MATERIAL_HUMIDITY",
        "DRYCHAMBER05.MATERIAL_TEMPERATURE",
        "DRYCHAMBER05.MATERIAL_HUMIDITY",
        "PLANT.STATE.ONLINE",
        "PLANT.MODE.CODE",
        "PLANT.PRODUCTION_RATE",
        "WAREHOUSE.HUMIDITY",
        "WAREHOUSE.TEMPERATURE_SENSOR",
        "WEATHER.AMBIENT_TEMPERATURE",
        "WEATHER.RELATIVE_HUMIDITY",
        "WEATHER.WIND_SPEED",
        "WEATHER.COOLING_DEGREE_INDEX"
    };

    private readonly EnsembleDetectionService _ensembleDetectionService;
    private readonly SQLiteHistoricoService _historicoService;
    private readonly BenchmarkOptions _options;
    private readonly GrupoAnaliseService _grupoAnaliseService;
    private readonly MultivariateGroupBufferService _multivariateBufferService;
    private readonly MultivariateDetector _multivariateDetector;
    private readonly JsonSerializerOptions _jsonOptions;

    public BenchmarkRunner(
        EnsembleDetectionService ensembleDetectionService,
        SQLiteHistoricoService historicoService,
        BenchmarkOptions options,
        GrupoAnaliseService grupoAnaliseService,
        MultivariateGroupBufferService multivariateBufferService,
        MultivariateDetector multivariateDetector)
    {
        _ensembleDetectionService = ensembleDetectionService
            ?? throw new ArgumentNullException(
                nameof(ensembleDetectionService));

        _historicoService = historicoService
            ?? throw new ArgumentNullException(
                nameof(historicoService));

        _options = options
            ?? throw new ArgumentNullException(
                nameof(options));

        _grupoAnaliseService = grupoAnaliseService
            ?? throw new ArgumentNullException(
                nameof(grupoAnaliseService));

        _multivariateBufferService = multivariateBufferService
            ?? throw new ArgumentNullException(
                nameof(multivariateBufferService));

        _multivariateDetector = multivariateDetector
            ?? throw new ArgumentNullException(
                nameof(multivariateDetector));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<BenchmarkExecutionSummary> ExecutarAsync(
        bool bancoLimpo = false,
        bool modoRapido = false,
        BaselineDiagnosticOptions? baselineDiagnostics = null,
        RegimeShadowOptions? regimeShadow = null,
        DualBaselineShadowOptions? dualBaselineShadow = null,
        BenchmarkBehaviorProfileOptions? behaviorProfileObservability = null,
        PersistenceCounterfactualOptions? persistenceCounterfactual = null,
        bool persistenceTemporalExperiment = false,
        ExperimentalIncidentOptions? experimentalIncident = null,
        ExperimentalAnomalyDecisionOptions? experimentalAnomalyDecision = null,
        bool functionalExperimentalAnomalyDecisionEnabled = false,
        FunctionalOperationalCanaryOptions? functionalOperationalCanary = null,
        CancellationToken cancellationToken = default)
    {
        _options.Validar();

        baselineDiagnostics ??=
            new BaselineDiagnosticOptions();

        regimeShadow ??= new RegimeShadowOptions();

        behaviorProfileObservability ??=
            new BenchmarkBehaviorProfileOptions();

        persistenceCounterfactual ??= new PersistenceCounterfactualOptions();
        experimentalIncident ??= new ExperimentalIncidentOptions();
        experimentalAnomalyDecision ??=
            new ExperimentalAnomalyDecisionOptions();

        /*
         * B3.16 — modo funcional operacional.
         *
         * A flag NÃO muta resultado.EhAnomalia.
         * Quando habilitada, a decisão operacional controla apenas
         * se a detecção univariada será emitida no arquivo de saída.
         *
         * Qualquer indisponibilidade experimental é resolvida
         * conservadoramente por LegacyFallback.
         */
        if (functionalExperimentalAnomalyDecisionEnabled)
        {
            Console.WriteLine(
                "Functional Experimental Anomaly Decision recebido pelo BenchmarkRunner (efeito operacional habilitado para saída univariada).");
        }

        var databasePath =
            bancoLimpo
                ? _options.CleanDatabasePath
                : _options.AnomalyDatabasePath;

        var prefixoArquivo =
            modoRapido
                ? bancoLimpo
                    ? "fast_clean"
                    : "fast_anomaly"
                : bancoLimpo
                    ? "clean"
                    : "anomaly";

        _historicoService.LimparTudo();
        _multivariateBufferService.LimparCliente(
            _options.ClienteId);

        var gruposMultivariados =
            await _grupoAnaliseService.ObterGruposAsync(
                _options.ClienteId);

        var tagsMultivariadas = gruposMultivariados
            .SelectMany(grupo => grupo.Tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        /*
         * Mantém as tags usadas anteriormente pelo benchmark
         * e acrescenta todas as tags necessárias aos grupos.
         */
        var tagsParaBenchmark = TagsBenchmark
            .Concat(tagsMultivariadas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        /*
         * Otimização importante:
         * as tags extras dos grupos são lidas para a análise multivariada,
         * mas o caminho univariado continua sendo executado somente nas
         * mesmas tags do benchmark anterior.
         *
         * Isso preserva a comparação com a execução antiga e evita aplicar
         * todos os detectores univariados às 67 tags adicionais.
         */
        var tagsUnivariadas = TagsBenchmark.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var performance = new BenchmarkPerformanceMetrics
        {
            ClienteId = _options.ClienteId,
            InicioUtc = DateTime.UtcNow,
            TotalTagsLidas = tagsParaBenchmark.Length,
            TotalTagsUnivariadas = tagsUnivariadas.Count,
            TotalGruposConfigurados = gruposMultivariados.Count
        };

        var reader =
            new SqliteDatasetReader(
                databasePath);

        BenchmarkFastPlan? planoRapido =
            null;

        var totalLeiturasEsperadas =
            reader.GetTotalSamples(
                tagsParaBenchmark);

        if (modoRapido)
        {
            planoRapido =
                BenchmarkFastPlanBuilder.Build(
                    reader,
                    tagsParaBenchmark,
                    _options);

            totalLeiturasEsperadas =
                planoRapido.PlannedSampleCount;
        }

        var resultDirectory =
            Path.GetFullPath(
                _options.ResultDirectory);

        Directory.CreateDirectory(
            resultDirectory);

        var executionId =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss");

        var detectionsPath = Path.Combine(
            resultDirectory,
            $"{prefixoArquivo}_detections_{executionId}.jsonl");

        var errorsPath = Path.Combine(
            resultDirectory,
            $"{prefixoArquivo}_errors_{executionId}.jsonl");

        var summaryPath = Path.Combine(
            resultDirectory,
            $"{prefixoArquivo}_summary_{executionId}.json");

        var performancePath = Path.Combine(
            resultDirectory,
            $"{prefixoArquivo}_performance_{executionId}.json");

        var functionalDecisionPath =
            functionalExperimentalAnomalyDecisionEnabled
                ? Path.Combine(
                    resultDirectory,
                    $"functional_experimental_anomaly_decisions_{executionId}.jsonl")
                : null;

        var functionalOperationalSummaryPath =
            functionalExperimentalAnomalyDecisionEnabled
                ? Path.Combine(
                    resultDirectory,
                    $"functional_operational_summary_{executionId}.json")
                : null;

        var functionalNewEvidencePath =
            functionalExperimentalAnomalyDecisionEnabled
                ? Path.Combine(
                    resultDirectory,
                    $"functional_new_evidence_{executionId}.jsonl")
                : null;

        var functionalNewEvidenceSummaryPath =
            functionalExperimentalAnomalyDecisionEnabled
                ? Path.Combine(
                    resultDirectory,
                    $"functional_new_evidence_summary_{executionId}.json")
                : null;

        var fastPlanPath =
            modoRapido
                ? Path.Combine(
                    resultDirectory,
                    $"{prefixoArquivo}_plan_{executionId}.json")
                : null;

        var baselineDiagnosticsPath =
            baselineDiagnostics.Enabled
                ? Path.Combine(
                    resultDirectory,
                    $"b2_baseline_trace_{executionId}.csv")
                : null;

        if (modoRapido &&
            planoRapido is not null &&
            fastPlanPath is not null)
        {
            await File.WriteAllTextAsync(
                fastPlanPath,
                JsonSerializer.Serialize(
                    planoRapido,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }),
                cancellationToken);
        }

        await using var detectionsWriter =
            CriarWriter(detectionsPath);

        await using var errorsWriter =
            CriarWriter(errorsPath);

        await using var functionalDecisionWriter =
            functionalDecisionPath is not null
                ? CriarWriter(functionalDecisionPath)
                : null;

        await using var functionalNewEvidenceWriter =
            functionalNewEvidencePath is not null
                ? CriarWriter(functionalNewEvidencePath)
                : null;

        var functionalDecisionResolver =
            functionalExperimentalAnomalyDecisionEnabled
                ? new FunctionalExperimentalAnomalyDecisionResolver()
                : null;

        var operationalDecisionResolver =
            functionalExperimentalAnomalyDecisionEnabled
                ? new FunctionalOperationalAnomalyDecisionResolver()
                : null;

        var operationalRolloutPolicy =
            functionalExperimentalAnomalyDecisionEnabled
                ? new FunctionalOperationalRolloutPolicy()
                : null;

        var operationalCanarySelector =
            functionalExperimentalAnomalyDecisionEnabled
                ? new FunctionalOperationalCanarySelector()
                : null;

        var effectiveOperationalCanary =
            functionalOperationalCanary ??
            new FunctionalOperationalCanaryOptions
            {
                Enabled = false
            };

        await using var baselineDiagnosticsWriter =
            baselineDiagnosticsPath is not null
                ? new BaselineDiagnosticWriter(
                    baselineDiagnosticsPath)
                : null;

        await using var regimeShadowObserver =
RegimeShadowObserver.TryCreate(
    regimeShadow,
    resultDirectory,
    executionId,
    dualBaselineShadow,
    experimentalIncident,
    experimentalAnomalyDecision);

        await using var behaviorProfileObserver =
            BenchmarkBehaviorProfileObserver.TryCreate(
                behaviorProfileObservability,
                _options.HistoryWindowSize,
                _options.MinimumHistoryPoints,
                databasePath,
                resultDirectory,
                executionId);

        await using var persistenceCounterfactualObserver =
            persistenceCounterfactual.Enabled
                ? new PersistenceCounterfactualObserver(
                    resultDirectory, executionId)
                : null;

        await using var persistenceExperimentalObserver =
            persistenceTemporalExperiment
                ? new PersistenceExperimentalObserver(resultDirectory, executionId)
                : null;

        if (baselineDiagnosticsPath is not null)
        {
            Console.WriteLine(
                $"Diagnóstico B2.1: {baselineDiagnosticsPath}");
        }

        if (regimeShadowObserver is not null)
            Console.WriteLine($"Regime Shadow: {regimeShadowObserver.TracePath}");

        if (regimeShadowObserver?.DualBaselineTracePath is not null)
            Console.WriteLine($"Dual Baseline Shadow: {regimeShadowObserver.DualBaselineTracePath}");

        if (regimeShadowObserver?.DualBaselineDecisionTracePath is not null)
        {
            Console.WriteLine($"Dual Baseline Decision Counterfactual: {regimeShadowObserver.DualBaselineDecisionTracePath}");
            Console.WriteLine($"Dual Baseline Decision Summary: {regimeShadowObserver.DualBaselineDecisionSummaryPath}");
        }

        if (regimeShadowObserver?.StableDeviationIncidentTracePath is not null)
        {
            Console.WriteLine($"Stable Deviation Incident Shadow: {regimeShadowObserver.StableDeviationIncidentTracePath}");
            Console.WriteLine($"Stable Deviation Incident Summary: {regimeShadowObserver.StableDeviationIncidentSummaryPath}");
        }

        if (regimeShadowObserver?.ExperimentalIncidentEventPath is not null)
        {
            Console.WriteLine($"Experimental Incident Events: {regimeShadowObserver.ExperimentalIncidentEventPath}");
            Console.WriteLine($"Experimental Incident Summary: {regimeShadowObserver.ExperimentalIncidentSummaryPath}");
        }

        if (regimeShadowObserver?.ExperimentalAnomalyDecisionTracePath is not null)
{
    Console.WriteLine(
        $"Experimental Anomaly Decision Trace: " +
        $"{regimeShadowObserver.ExperimentalAnomalyDecisionTracePath}");

    Console.WriteLine(
        $"Experimental Anomaly Decision Summary: " +
        $"{regimeShadowObserver.ExperimentalAnomalyDecisionSummaryPath}");
}

        if (functionalDecisionPath is not null)
        {
            Console.WriteLine(
                $"Functional Experimental Decision Trace: " +
                $"{functionalDecisionPath}");
        }

        if (functionalNewEvidencePath is not null)
        {
            Console.WriteLine(
                $"Functional New Evidence: " +
                $"{functionalNewEvidencePath}");
        }

        if (behaviorProfileObserver is not null)
        {
            Console.WriteLine(
                $"Perfil comportamental paralelo: " +
                $"{behaviorProfileObserver.TracePath}");
            Console.WriteLine(
                $"Resumo Legacy x V2: " +
                $"{behaviorProfileObserver.SummaryPath}");
            if (behaviorProfileObserver.ApplicabilityCounterfactualTracePath is not null)
            {
                Console.WriteLine(
                    "Contrafactual de aplicabilidade: " +
                    behaviorProfileObserver.ApplicabilityCounterfactualTracePath);
            }
        }

        if (persistenceCounterfactualObserver is not null)
            Console.WriteLine("Persistence Counterfactual: " +
                              persistenceCounterfactualObserver.TracePath);

        if (persistenceExperimentalObserver is not null)
            Console.WriteLine("Persistence Temporal Experimental: " +
                              persistenceExperimentalObserver.TracePath);

        var summary = new BenchmarkExecutionSummary
        {
            ClienteId = _options.ClienteId,
            InicioExecucaoUtc = DateTime.UtcNow,
            BancoOrigem =
                Path.GetFullPath(
                    databasePath),
            DetectionsFile = detectionsPath,
            ErrorsFile = errorsPath
        };

        ExibirCabecalho(
            totalLeiturasEsperadas,
            gruposMultivariados.Count,
            tagsParaBenchmark.Length,
            databasePath,
            bancoLimpo,
            modoRapido,
            planoRapido);

        var stopwatch =
            Stopwatch.StartNew();

        long functionalLegacyAnomalies = 0;
        long functionalEmitAnomalies = 0;
        long functionalEmitNewEvidence = 0;
        long functionalContinuations = 0;
        long functionalCandidateMismatches = 0;
        long functionalDataQualityUncertain = 0;
        long functionalLegacyFallbacks = 0;
        long rolloutFunctionalAccepted = 0;
        long rolloutLegacyFallback = 0;
        long rolloutDataQualityFallback = 0;
        long rolloutUnavailableFallback = 0;
        long rolloutUpstreamFallback = 0;
        long canaryInScopeRecords = 0;
        long canaryOutOfScopeRecords = 0;

        long newEvidenceTotal = 0;
        long newEvidenceNewAnomalies = 0;
        long newEvidenceCandidateMismatches = 0;
        long newEvidenceDataQualityUncertain = 0;
        long newEvidenceLegacyFallbacks = 0;

        var janelasExecucao =
            modoRapido &&
            planoRapido is not null
                ? planoRapido.Windows
                : new[]
                {
                    new BenchmarkFastWindow
                    {
                        Index = 1,
                        StartMinuteInclusive = int.MinValue,
                        EndMinuteInclusive = int.MaxValue,
                        ExpectedSamples = totalLeiturasEsperadas
                    }
                };

        foreach (var janela in
                 janelasExecucao)
        {
            regimeShadowObserver?.ResetWindow(janela.Index);
            behaviorProfileObserver?.ResetWindow(janela.Index);
            persistenceCounterfactualObserver?.ResetWindow();
            persistenceExperimentalObserver?.ResetWindow();
            if (modoRapido)
            {
                /*
                 * Cada janela é uma mini-execução independente.
                 * Isso evita que o histórico, Persistência ou buffer
                 * multivariado "atravessem" semanas/meses não amostrados.
                 */
                _historicoService.LimparTudo();
                _multivariateBufferService.LimparCliente(
                    _options.ClienteId);

                Console.WriteLine(
                    $"FAST janela {janela.Index}/{janelasExecucao.Count}: " +
                    $"{janela.StartMinuteInclusive:N0}.." +
                    $"{janela.EndMinuteInclusive:N0} " +
                    $"(~{janela.ExpectedSamples:N0} leituras)");
            }

            var inicioJanela =
                modoRapido
                    ? janela.StartMinuteInclusive
                    : (int?)null;

            var fimJanela =
                modoRapido
                    ? janela.EndMinuteInclusive
                    : (int?)null;

            foreach (var sample in
                     reader.GetSamplesChronological(
                         tagsParaBenchmark,
                         inicioJanela,
                         fimJanela))
            {
            cancellationToken
                .ThrowIfCancellationRequested();

            summary.TotalLeiturasLidas++;

            var dataHora =
                _options.StartDateUtc
                    .AddMinutes(
                        sample.MinuteIndex);

            if (!TryConverterValor(
                    sample.Value,
                    out var valor))
            {
                summary.TotalLeiturasInvalidas++;

                await RegistrarErroAsync(
                    errorsWriter,
                    new BenchmarkErrorResult
                    {
                        ClienteId =
                            _options.ClienteId,
                        TagName =
                            sample.TagName,
                        MinuteIndex =
                            sample.MinuteIndex,
                        DataHora =
                            dataHora,
                        Valor = 0,
                        TipoErro =
                            "ValorForaDoIntervaloFloat",
                        Mensagem =
                            $"O valor {sample.Value} não pode " +
                            "ser convertido para float."
                    });

                ExibirProgressoSeNecessario(
                    summary,
                    totalLeiturasEsperadas,
                    stopwatch,
                    performance);

                continue;
            }

            if (behaviorProfileObserver is not null)
            {
                await behaviorProfileObserver.ObserveReadingAsync(
                    janela.Index,
                    sample.MinuteIndex,
                    _options.ClienteId,
                    sample.TagName,
                    dataHora,
                    valor,
                    TipoTagPadrao);
            }

            /*
             * A leitura entra no caminho multivariado antes da
             * validação de aquecimento univariado.
             *
             * Assim, o buffer recebe todas as leituras válidas e
             * não perde os primeiros minutos de cada tag.
             */
            try
            {
                var inicioMultivariado =
                    Stopwatch.GetTimestamp();

                var gruposFinalizados =
                    await _multivariateBufferService
                        .AdicionarLeituraAsync(
                            _options.ClienteId,
                            sample.TagName,
                            sample.MinuteIndex,
                            sample.Value,
                            cancellationToken);

                performance.TotalGruposFinalizados +=
                    gruposFinalizados.Count;

                await ProcessarGruposMultivariadosAsync(
                    gruposFinalizados,
                    detectionsWriter,
                    errorsWriter,
                    summary,
                    performance,
                    cancellationToken);

                performance.TicksMultivariado +=
                    Stopwatch.GetTimestamp() -
                    inicioMultivariado;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.TotalErros++;
                performance.TotalErrosMultivariados++;

                await RegistrarErroAsync(
                    errorsWriter,
                    new BenchmarkErrorResult
                    {
                        ClienteId =
                            _options.ClienteId,
                        TagName =
                            $"MULTIVARIATE/{sample.TagName}",
                        MinuteIndex =
                            sample.MinuteIndex,
                        DataHora =
                            dataHora,
                        Valor =
                            valor,
                        TipoErro =
                            ex.GetType().Name,
                        Mensagem =
                            $"Falha no buffer multivariado: " +
                            ex.Message
                    });
            }

            /*
             * Tags que existem apenas para alimentar os grupos não passam
             * pelos detectores univariados. Isso reduz bastante o custo sem
             * remover nenhum cálculo que já existia no benchmark anterior.
             */
            if (!tagsUnivariadas.Contains(sample.TagName))
            {
                performance.TotalLeiturasSomenteMultivariadas++;

                if (summary.TotalLeiturasLidas %
                    IntervaloFlush == 0)
                {
                    await detectionsWriter.FlushAsync();
                    await errorsWriter.FlushAsync();

                    if (functionalDecisionWriter is not null)
                        await functionalDecisionWriter.FlushAsync();

                    if (functionalNewEvidenceWriter is not null)
                        await functionalNewEvidenceWriter.FlushAsync();
                }

                ExibirProgressoSeNecessario(
                    summary,
                    totalLeiturasEsperadas,
                    stopwatch,
                    performance);

                continue;
            }

            performance.TotalLeiturasUnivariadas++;

            /*
             * Caminho univariado original.
             */
            if (_historicoService.ObterQuantidade(
                    _options.ClienteId,
                    sample.TagName) <
                _options.MinimumHistoryPoints)
            {
                _historicoService.AdicionarLeitura(
                    _options.ClienteId,
                    sample.TagName,
                    dataHora,
                    valor);

                summary.TotalLeiturasAquecimento++;

                ExibirProgressoSeNecessario(
                    summary,
                    totalLeiturasEsperadas,
                    stopwatch,
                    performance);

                continue;
            }

            var inicioUnivariado =
                Stopwatch.GetTimestamp();

            try
            {
                var snapshot =
                    _historicoService
                        .CriarSnapshotDetector(
                            _options.ClienteId,
                            sample.TagName);

                if (snapshot.Historico.Count <
                    _options.MinimumHistoryPoints)
                {
                    throw new InvalidOperationException(
                        $"Histórico insuficiente para a tag " +
                        $"'{sample.TagName}'. Disponível: " +
                        $"{snapshot.Historico.Count}. Necessário: " +
                        $"{_options.MinimumHistoryPoints}.");
                }

                var input = new DetectorInput
                {
                    ClienteId =
                        _options.ClienteId,
                    TagName =
                        sample.TagName,
                    TipoTag =
                        TipoTagPadrao,
                    ValorAtual =
                        valor,
                    DataHoraAtual =
                        dataHora,
                    MediaHistorica =
                        snapshot.Media,
                    DesvioPadraoHistorico =
                        snapshot.DesvioPadrao,
                    Historico =
                        snapshot.Historico,
                                        };

                var resultado =
                    _ensembleDetectionService
                        .Detectar(input);

                behaviorProfileObserver?.RecordOriginalDecision(
                    sample.TagName,
                    sample.MinuteIndex,
                    resultado);

                persistenceCounterfactualObserver?.Observe(
                    janela.Index,
                    sample.MinuteIndex,
                    sample.TagName,
                    resultado);

                persistenceExperimentalObserver?.Observe(
                    janela.Index,
                    sample.MinuteIndex,
                    input,
                    resultado);

                ExperimentalAnomalyDecision? experimentalDecision = null;

                if (regimeShadowObserver is not null)
                {
                    experimentalDecision =
                        await regimeShadowObserver.ObserveAsync(
                            janela.Index,
                            sample.MinuteIndex,
                            input,
                            resultado);
                }

                /*
                 * B3.16 — decisão funcional.
                 *
                 * resultado.EhAnomalia continua imutável e representa
                 * sempre a decisão Legacy original.
                 *
                 * A decisão operacional abaixo poderá controlar somente
                 * a emissão da detecção univariada quando a flag estiver ON.
                 */
                FunctionalOperationalAnomalyDecision?
                    operationalDecision = null;

                FunctionalOperationalRolloutDecision?
                    rolloutDecision = null;

                if (functionalDecisionResolver is not null &&
                    operationalDecisionResolver is not null &&
                    operationalRolloutPolicy is not null &&
                    operationalCanarySelector is not null &&
                    functionalDecisionWriter is not null)
                {
                    var functionalDecision =
                        functionalDecisionResolver.Resolve(
                            resultado.EhAnomalia,
                            experimentalDecision);

                    /*
                     * B3.16 — canais operacionais.
                     *
                     * EmitAnomaly controla somente a emissão univariada
                     * quando a feature flag está habilitada.
                     *
                     * EmitNewEvidence permanece em canal separado para
                     * preservar CandidateMismatch / B -> C.
                     *
                     * resultado.EhAnomalia, baseline e detectores Legacy
                     * continuam sem mutação.
                     */
                    operationalDecision =
                        operationalDecisionResolver.Resolve(
                            functionalDecision);

                    var canaryInScope =
                        operationalCanarySelector.IsInCanaryScope(
                            effectiveOperationalCanary,
                            janela.Index,
                            sample.TagName);

                    if (canaryInScope)
                        canaryInScopeRecords++;
                    else
                        canaryOutOfScopeRecords++;

                    rolloutDecision =
                        operationalRolloutPolicy.Resolve(
                            resultado.EhAnomalia,
                            operationalDecision,
                            canaryInScope);

                    if (rolloutDecision.Mode ==
                        FunctionalOperationalRolloutMode.UseFunctional)
                    {
                        rolloutFunctionalAccepted++;
                    }
                    else
                    {
                        rolloutLegacyFallback++;

                        if (rolloutDecision.Reasons.Contains(
                                "DataQualityUncertain"))
                        {
                            rolloutDataQualityFallback++;
                        }

                        if (rolloutDecision.Reasons.Contains(
                                "OperationalDecisionUnavailable"))
                        {
                            rolloutUnavailableFallback++;
                        }

                        if (rolloutDecision.Reasons.Contains(
                                "UpstreamLegacyFallback"))
                        {
                            rolloutUpstreamFallback++;
                        }
                    }

                    if (resultado.EhAnomalia)
                        functionalLegacyAnomalies++;

                    if (operationalDecision.EmitAnomaly)
                        functionalEmitAnomalies++;

                    if (operationalDecision.EmitNewEvidence)
                        functionalEmitNewEvidence++;

                    if (operationalDecision.UsedLegacyFallback)
                        functionalLegacyFallbacks++;

                    switch (operationalDecision.SourceDecision)
                    {
                        case ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation:
                            functionalContinuations++;
                            break;

                        case ExperimentalAnomalyDecisionKind.CandidateMismatch:
                            functionalCandidateMismatches++;
                            break;

                        case ExperimentalAnomalyDecisionKind.DataQualityUncertain:
                            functionalDataQualityUncertain++;
                            break;
                    }

                    /*
                     * B3.17 — canal operacional de nova evidência.
                     *
                     * Este arquivo contém somente eventos que realmente
                     * representam nova evidência operacional.
                     *
                     * NewAnomaly:
                     *   EmitAnomaly = true
                     *   EmitNewEvidence = true
                     *
                     * CandidateMismatch / B -> C:
                     *   EmitAnomaly = false
                     *   EmitNewEvidence = true
                     *
                     * Continuation:
                     *   EmitNewEvidence = false
                     *   não entra neste canal.
                     */
                    if (rolloutDecision.EmitNewEvidence &&
                        functionalNewEvidenceWriter is not null)
                    {
                        newEvidenceTotal++;

                        if (rolloutDecision.Mode ==
                            FunctionalOperationalRolloutMode.UseLegacyFallback)
                        {
                            newEvidenceLegacyFallbacks++;
                        }

                        switch (rolloutDecision.SourceDecision)
                        {
                            case ExperimentalAnomalyDecisionKind.NewAnomaly:
                                newEvidenceNewAnomalies++;
                                break;

                            case ExperimentalAnomalyDecisionKind.CandidateMismatch:
                                newEvidenceCandidateMismatches++;
                                break;

                            case ExperimentalAnomalyDecisionKind.DataQualityUncertain:
                                newEvidenceDataQualityUncertain++;
                                break;
                        }

                        var evidenceRecord = new
                        {
                            fastWindow = janela.Index,
                            minuteIndex = sample.MinuteIndex,
                            timestamp = dataHora,
                            tag = sample.TagName,
                            legacyFinalAnomaly = resultado.EhAnomalia,
                            legacyScore = resultado.Score,
                            emitAnomaly = rolloutDecision.EmitAnomaly,
                            emitNewEvidence = true,
                            sourceDecision = rolloutDecision.SourceDecision,
                            rolloutMode = rolloutDecision.Mode,
                            canaryEnabled = effectiveOperationalCanary.Enabled,
                            canaryInScope,
                            usedLegacyFallback =
                                rolloutDecision.Mode ==
                                FunctionalOperationalRolloutMode.UseLegacyFallback,
                            reasons = rolloutDecision.Reasons
                        };

                        await functionalNewEvidenceWriter.WriteLineAsync(
                            JsonSerializer.Serialize(
                                evidenceRecord,
                                _jsonOptions));
                    }

                    var shouldTraceFunctionalDecision =
                        resultado.EhAnomalia ||
                        rolloutDecision.EmitAnomaly ||
                        rolloutDecision.EmitNewEvidence ||
                        rolloutDecision.Mode ==
                            FunctionalOperationalRolloutMode.UseLegacyFallback ||
                        experimentalDecision?.Kind is
                            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation or
                            ExperimentalAnomalyDecisionKind.CandidateMismatch or
                            ExperimentalAnomalyDecisionKind.DataQualityUncertain;

                    if (shouldTraceFunctionalDecision)
                    {
                        var functionalRecord = new
                        {
                            fastWindow = janela.Index,
                            minuteIndex = sample.MinuteIndex,
                            timestamp = dataHora,
                            tag = sample.TagName,

                            legacyFinalAnomaly =
                                resultado.EhAnomalia,

                            legacyScore =
                                resultado.Score,

                            experimentalDecision =
                                experimentalDecision?.Kind,

                            functionalFinalAnomaly =
                                functionalDecision.FunctionalFinalAnomaly,

                            countsAsNewEvidence =
                                functionalDecision.CountsAsNewEvidence,

                            emitAnomaly =
                                rolloutDecision.EmitAnomaly,

                            emitNewEvidence =
                                rolloutDecision.EmitNewEvidence,

                            sourceDecision =
                                rolloutDecision.SourceDecision,

                            rolloutMode =
                                rolloutDecision.Mode,

                            canaryEnabled =
                                effectiveOperationalCanary.Enabled,

                            canaryInScope,

                            usedLegacyFallback =
                                rolloutDecision.Mode ==
                                FunctionalOperationalRolloutMode.UseLegacyFallback,

                            reasons =
                                rolloutDecision.Reasons
                        };

                        await functionalDecisionWriter.WriteLineAsync(
                            JsonSerializer.Serialize(
                                functionalRecord,
                                _jsonOptions));
                    }
                }

                if (baselineDiagnosticsWriter is not null &&
                    baselineDiagnostics.ShouldTrace(
                        sample.TagName,
                        sample.MinuteIndex))
                {
                    await baselineDiagnosticsWriter.WriteAsync(
                        janela.Index,
                        sample.MinuteIndex,
                        dataHora,
                        sample.TagName,
                        valor,
                        _options.HistoryWindowSize,
                        input,
                        resultado);
                }

                summary.TotalDeteccoesExecutadas++;

                /*
                 * B3.16 — ponto de ativação operacional.
                 *
                 * FLAG OFF:
                 *   comportamento Legacy exatamente como antes.
                 *
                 * FLAG ON:
                 *   EmitAnomaly controla se a detecção univariada
                 *   entra no arquivo de saída.
                 *
                 * Se, por qualquer motivo, a decisão operacional não
                 * estiver disponível, preservamos Legacy de forma
                 * conservadora.
                 *
                 * IMPORTANTE:
                 * resultado.EhAnomalia NÃO é alterado.
                 */
                var deveEmitirAnomalia =
                    functionalExperimentalAnomalyDecisionEnabled
                        ? rolloutDecision?.EmitAnomaly
                            ?? resultado.EhAnomalia
                        : resultado.EhAnomalia;

                if (deveEmitirAnomalia)
                {
                    summary.TotalAnomaliasDetectadas++;
                    performance.TotalLinhasUnivariadasGravadas++;

                    var detection =
                        CriarDetectionResult(
                            sample,
                            dataHora,
                            valor,
                            resultado);

                    await detectionsWriter
                        .WriteLineAsync(
                            JsonSerializer.Serialize(
                                detection,
                                _jsonOptions));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.TotalErros++;

                await RegistrarErroAsync(
                    errorsWriter,
                    new BenchmarkErrorResult
                    {
                        ClienteId =
                            _options.ClienteId,
                        TagName =
                            sample.TagName,
                        MinuteIndex =
                            sample.MinuteIndex,
                        DataHora =
                            dataHora,
                        Valor =
                            valor,
                        TipoErro =
                            ex.GetType().Name,
                        Mensagem =
                            ex.Message
                    });
            }
            finally
            {
                performance.TicksUnivariado +=
                    Stopwatch.GetTimestamp() -
                    inicioUnivariado;

                /*
                 * Mantém a regra original:
                 * a leitura atual entra no histórico apenas
                 * depois da detecção univariada.
                 */
                _historicoService.AdicionarLeitura(
                    _options.ClienteId,
                    sample.TagName,
                    dataHora,
                    valor);
            }

            if (summary.TotalLeiturasLidas %
                IntervaloFlush == 0)
            {
                await detectionsWriter.FlushAsync();
                await errorsWriter.FlushAsync();

                if (functionalDecisionWriter is not null)
                    await functionalDecisionWriter.FlushAsync();

                if (functionalNewEvidenceWriter is not null)
                    await functionalNewEvidenceWriter.FlushAsync();
            }

            ExibirProgressoSeNecessario(
                summary,
                totalLeiturasEsperadas,
                stopwatch,
                performance);
            }

            if (modoRapido)
            {
                /*
                 * Fecha o último minuto da janela antes de limpar o estado
                 * e avançar para outra região temporal do banco.
                 */
                var gruposPendentesJanela =
                    _multivariateBufferService
                        .FinalizarPendentes(
                            _options.ClienteId);

                performance.TotalGruposFinalizados +=
                    gruposPendentesJanela.Count;

                await ProcessarGruposMultivariadosAsync(
                    gruposPendentesJanela,
                    detectionsWriter,
                    errorsWriter,
                    summary,
                    performance,
                    cancellationToken);

                await detectionsWriter.FlushAsync();
                await errorsWriter.FlushAsync();

                if (functionalDecisionWriter is not null)
                    await functionalDecisionWriter.FlushAsync();

                if (functionalNewEvidenceWriter is not null)
                    await functionalNewEvidenceWriter.FlushAsync();

                if (regimeShadowObserver is not null)
                    await regimeShadowObserver.FinalizeWindowAsync(janela.Index);
            }
        }

        /*
         * Processa os grupos do último minuto, que não seriam
         * finalizados pela chegada de uma leitura posterior.
         */
        if (!modoRapido)
        {
            var gruposPendentes =
                _multivariateBufferService
                    .FinalizarPendentes(
                        _options.ClienteId);

            performance.TotalGruposFinalizados +=
                gruposPendentes.Count;

            await ProcessarGruposMultivariadosAsync(
                gruposPendentes,
                detectionsWriter,
                errorsWriter,
                summary,
                performance,
                cancellationToken);
        }

        stopwatch.Stop();

        summary.FimExecucaoUtc =
            DateTime.UtcNow;

        summary.DuracaoSegundos =
            stopwatch.Elapsed.TotalSeconds;

        await detectionsWriter.FlushAsync();
        await errorsWriter.FlushAsync();

        if (functionalDecisionWriter is not null)
            await functionalDecisionWriter.FlushAsync();

        if (functionalNewEvidenceWriter is not null)
            await functionalNewEvidenceWriter.FlushAsync();

        var summaryJson =
            JsonSerializer.Serialize(
                summary,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                });

        await File.WriteAllTextAsync(
            summaryPath,
            summaryJson,
            cancellationToken);

        if (functionalNewEvidenceSummaryPath is not null)
        {
            var newEvidenceSummary = new
            {
                generatedAtUtc = DateTime.UtcNow,
                enabled = true,
                evidenceFile = functionalNewEvidencePath,
                totalNewEvidence = newEvidenceTotal,
                newAnomalyEvidence = newEvidenceNewAnomalies,
                candidateMismatchEvidence = newEvidenceCandidateMismatches,
                dataQualityUncertainEvidence = newEvidenceDataQualityUncertain,
                legacyFallbackEvidence = newEvidenceLegacyFallbacks,
                invariants = new
                {
                    continuationExcluded = true,
                    candidateMismatchIncluded = true,
                    candidateMismatchDoesNotRequireEmitAnomaly = true,
                    legacyFallbackPreservesLegacy = true
                }
            };

            await File.WriteAllTextAsync(
                functionalNewEvidenceSummaryPath,
                JsonSerializer.Serialize(
                    newEvidenceSummary,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }),
                cancellationToken);
        }

        if (functionalOperationalSummaryPath is not null)
        {
            var suppressedLegacyAnomalies =
                Math.Max(
                    0,
                    functionalLegacyAnomalies -
                    functionalEmitAnomalies);

            var reductionPercentage =
                functionalLegacyAnomalies == 0
                    ? 0
                    : 100.0 *
                      suppressedLegacyAnomalies /
                      functionalLegacyAnomalies;

            var functionalOperationalSummary = new
            {
                generatedAtUtc = DateTime.UtcNow,
                enabled = true,
                legacyAnomalies = functionalLegacyAnomalies,
                functionalEmittedAnomalies = functionalEmitAnomalies,
                functionalNewEvidence = functionalEmitNewEvidence,
                suppressedLegacyAnomalies,
                continuationRecords = functionalContinuations,
                candidateMismatchRecords = functionalCandidateMismatches,
                dataQualityUncertainRecords = functionalDataQualityUncertain,
                legacyFallbackRecords = functionalLegacyFallbacks,
                rolloutFunctionalAccepted,
                rolloutLegacyFallback,
                rolloutDataQualityFallback,
                rolloutUnavailableFallback,
                rolloutUpstreamFallback,
                canaryEnabled = effectiveOperationalCanary.Enabled,
                canaryInScopeRecords,
                canaryOutOfScopeRecords,
                reductionPercentage,
                newEvidenceFile = functionalNewEvidencePath,
                newEvidenceSummaryFile = functionalNewEvidenceSummaryPath,
                invariants = new
                {
                    legacyDetectorResultMutated = false,
                    candidateMismatchPreservedAsNewEvidence = true,
                    criticalGapPreservesLegacyConservatively = true,
                    legacyFallbackEnabled = true,
                    canaryOutOfScopeUsesLegacy = true,
                    canarySelectionDeterministic = true,
                    canaryEmptyScopeFailsClosed = true
                }
            };

            await File.WriteAllTextAsync(
                functionalOperationalSummaryPath,
                JsonSerializer.Serialize(
                    functionalOperationalSummary,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }),
                cancellationToken);
        }

        performance.FimUtc = DateTime.UtcNow;
        performance.DuracaoTotalSegundos =
            stopwatch.Elapsed.TotalSeconds;
        performance.SegundosUnivariado =
            TicksParaSegundos(
                performance.TicksUnivariado);
        performance.SegundosMultivariado =
            TicksParaSegundos(
                performance.TicksMultivariado);

        await File.WriteAllTextAsync(
            performancePath,
            JsonSerializer.Serialize(
                performance,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                }),
            cancellationToken);

        _multivariateBufferService.LimparCliente(
            _options.ClienteId);

        if (functionalNewEvidenceSummaryPath is not null)
        {
            Console.WriteLine();
            Console.WriteLine(
                "=== B3.17 NEW EVIDENCE SUMMARY ===");
            Console.WriteLine(
                $"Total new evidence: {newEvidenceTotal:N0}");
            Console.WriteLine(
                $"NewAnomaly evidence: {newEvidenceNewAnomalies:N0}");
            Console.WriteLine(
                $"CandidateMismatch evidence: {newEvidenceCandidateMismatches:N0}");
            Console.WriteLine(
                $"DataQualityUncertain evidence: {newEvidenceDataQualityUncertain:N0}");
            Console.WriteLine(
                $"LegacyFallback evidence: {newEvidenceLegacyFallbacks:N0}");
            Console.WriteLine(
                $"New evidence summary: {functionalNewEvidenceSummaryPath}");
        }

        if (functionalExperimentalAnomalyDecisionEnabled)
        {
            Console.WriteLine();
            Console.WriteLine(
                "=== B3.17 ROLLOUT SAFETY SUMMARY ===");
            Console.WriteLine(
                $"Functional accepted: {rolloutFunctionalAccepted:N0}");
            Console.WriteLine(
                $"Legacy rollback: {rolloutLegacyFallback:N0}");
            Console.WriteLine(
                $"DataQuality rollback: {rolloutDataQualityFallback:N0}");
            Console.WriteLine(
                $"Unavailable rollback: {rolloutUnavailableFallback:N0}");
            Console.WriteLine(
                $"Upstream rollback: {rolloutUpstreamFallback:N0}");
            Console.WriteLine(
                $"Canary enabled: {effectiveOperationalCanary.Enabled}");
            Console.WriteLine(
                $"Canary in scope: {canaryInScopeRecords:N0}");
            Console.WriteLine(
                $"Canary out of scope: {canaryOutOfScopeRecords:N0}");
        }

        if (functionalOperationalSummaryPath is not null)
        {
            var suppressedLegacyAnomalies =
                Math.Max(
                    0,
                    functionalLegacyAnomalies -
                    functionalEmitAnomalies);

            var reductionPercentage =
                functionalLegacyAnomalies == 0
                    ? 0
                    : 100.0 *
                      suppressedLegacyAnomalies /
                      functionalLegacyAnomalies;

            Console.WriteLine();
            Console.WriteLine(
                "=== B3.17 FUNCTIONAL OPERATIONAL SUMMARY ===");
            Console.WriteLine(
                $"Legacy anomalies: {functionalLegacyAnomalies:N0}");
            Console.WriteLine(
                $"Functional emitted anomalies: {functionalEmitAnomalies:N0}");
            Console.WriteLine(
                $"New evidence: {functionalEmitNewEvidence:N0}");
            Console.WriteLine(
                $"Suppressed Legacy anomalies: {suppressedLegacyAnomalies:N0}");
            Console.WriteLine(
                $"Continuations: {functionalContinuations:N0}");
            Console.WriteLine(
                $"Candidate mismatches: {functionalCandidateMismatches:N0}");
            Console.WriteLine(
                $"Data quality uncertain: {functionalDataQualityUncertain:N0}");
            Console.WriteLine(
                $"Legacy fallbacks: {functionalLegacyFallbacks:N0}");
            Console.WriteLine(
                $"Reduction: {reductionPercentage:F4}%");
            Console.WriteLine(
                $"Operational summary: {functionalOperationalSummaryPath}");
        }

        ExibirResumo(
            summary,
            detectionsPath,
            errorsPath,
            summaryPath,
            performancePath,
            performance);

        return summary;
    }

    private async Task ProcessarGruposMultivariadosAsync(
        IReadOnlyList<BufferedGroupReading> grupos,
        StreamWriter detectionsWriter,
        StreamWriter errorsWriter,
        BenchmarkExecutionSummary summary,
        BenchmarkPerformanceMetrics performance,
        CancellationToken cancellationToken)
    {
        foreach (var grupo in grupos)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                var resultado =
                    await _multivariateDetector
                        .DetectarAsync(
                            grupo,
                            cancellationToken:
                                cancellationToken);

                /*
                 * Conta somente análises realmente aplicáveis.
                 * Resultados sem modelo ou com tags insuficientes
                 * não entram como detecção executada.
                 */
                if (!resultado.Aplicavel)
                {
                    performance.TotalGruposNaoAplicaveis++;
                    continue;
                }

                performance.TotalGruposAnalisados++;
                summary.TotalDeteccoesExecutadas++;

                if (!resultado.EhAnomalia)
                {
                    performance.TotalGruposNormais++;
                    continue;
                }

                performance.TotalEventosMultivariadosAnomalos++;

                /*
                 * O avaliador compara as detecções por tag.
                 * Portanto, um evento multivariado é registrado
                 * uma vez para cada tag usada no grupo.
                 *
                 * Todas as linhas apontam para o mesmo grupo,
                 * minuto, score e explicação.
                 */
                foreach (var tagName in
                         resultado.TagsUtilizadas)
                {
                    if (!grupo.Valores.TryGetValue(
                            tagName,
                            out var valorAtual))
                    {
                        continue;
                    }

                    summary.TotalAnomaliasDetectadas++;
                    performance.TotalLinhasMultivariadasGravadas++;

                    var detection =
                        CriarMultivariateDetectionResult(
                            grupo,
                            tagName,
                            valorAtual,
                            resultado);

                    await detectionsWriter
                        .WriteLineAsync(
                            JsonSerializer.Serialize(
                                detection,
                                _jsonOptions));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary.TotalErros++;
                performance.TotalErrosMultivariados++;

                await RegistrarErroAsync(
                    errorsWriter,
                    new BenchmarkErrorResult
                    {
                        ClienteId =
                            grupo.ClienteId,
                        TagName =
                            $"MULTIVARIATE/{grupo.GroupName}",
                        MinuteIndex =
                            checked((int)grupo.MinuteIndex),
                        DataHora =
                            _options.StartDateUtc.AddMinutes(
                                grupo.MinuteIndex),
                        Valor = 0,
                        TipoErro =
                            ex.GetType().Name,
                        Mensagem =
                            ex.Message
                    });
            }
        }
    }

    private BenchmarkDetectionResult
        CriarMultivariateDetectionResult(
            BufferedGroupReading grupo,
            string tagName,
            double valorAtual,
            MultivariateDetectionResult resultado)
    {
        var contribution =
            resultado.Contribuicoes
                .FirstOrDefault(item =>
                    string.Equals(
                        item.TagName,
                        tagName,
                        StringComparison.OrdinalIgnoreCase));

        var detectorExplanation =
            contribution is null
                ? resultado.Explicacao
                : resultado.Explicacao +
                  $" Contribuição da tag: " +
                  $"valor={contribution.ValorAtual:F4}, " +
                  $"média={contribution.MediaTreinamento:F4}, " +
                  $"|z|={contribution.ZScoreAbsoluto:F2}.";

        return new BenchmarkDetectionResult
        {
            ClienteId =
                grupo.ClienteId,
            TagName =
                tagName,
            MinuteIndex =
                checked((int)grupo.MinuteIndex),
            DataHora =
                _options.StartDateUtc.AddMinutes(
                    grupo.MinuteIndex),
            Valor =
                ConverterParaFloatSeguro(
                    valorAtual),
            EhAnomalia =
                true,
            Score =
                (float)resultado.ScoreNormalizado,
            Mensagem =
                detectorExplanation,
            Severidade =
                null,
            InterpretacaoGeradaPorLlm =
                false,
            Detectores =
            [
                new BenchmarkDetectorResult
                {
                    Nome =
                        $"Multivariado/{grupo.GroupName}",
                    Aplicavel =
                        true,
                    VotouAnomalia =
                        true,
                    ScoreNormalizado =
                        resultado.ScoreNormalizado,
                    Peso =
                        PesoDetectorMultivariado,
                    Explicacao =
                        detectorExplanation
                }
            ]
        };
    }

    private static StreamWriter CriarWriter(
        string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1 << 20,
            options:
                FileOptions.SequentialScan);

        return new StreamWriter(
            stream,
            bufferSize: 1 << 20);
    }

    private static bool TryConverterValor(
        double origem,
        out float valor)
    {
        if (!double.IsFinite(origem) ||
            origem > float.MaxValue ||
            origem < float.MinValue)
        {
            valor = 0;
            return false;
        }

        valor = (float)origem;

        return float.IsFinite(valor);
    }

    private static float ConverterParaFloatSeguro(
        double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (float)Math.Clamp(
            value,
            float.MinValue,
            float.MaxValue);
    }

    private BenchmarkDetectionResult CriarDetectionResult(
        DatasetSample sample,
        DateTime dataHora,
        float valor,
        DetectorResult resultado)
    {
        var detectores =
            new List<BenchmarkDetectorResult>(
                resultado.DetalhesDetectores.Count);

        foreach (var detector in
                 resultado.DetalhesDetectores)
        {
            detectores.Add(
                new BenchmarkDetectorResult
                {
                    Nome =
                        detector.Nome,
                    Aplicavel =
                        detector.Aplicavel,
                    VotouAnomalia =
                        detector.VotouAnomalia,
                    ScoreNormalizado =
                        detector.ScoreNormalizado,
                    Peso =
                        detector.Peso,
                    Explicacao =
                        detector.Explicacao
                });
        }

        return new BenchmarkDetectionResult
        {
            ClienteId =
                _options.ClienteId,
            TagName =
                sample.TagName,
            MinuteIndex =
                sample.MinuteIndex,
            DataHora =
                dataHora,
            Valor =
                valor,
            EhAnomalia =
                true,
            Score =
                (float)resultado.ScoreNormalizado,
            Mensagem =
                resultado.Explicacao,
            Severidade =
                null,
            InterpretacaoGeradaPorLlm =
                false,
            Detectores =
                detectores
        };
    }

    private async Task RegistrarErroAsync(
        StreamWriter writer,
        BenchmarkErrorResult error)
    {
        await writer.WriteLineAsync(
            JsonSerializer.Serialize(
                error,
                _jsonOptions));
    }

    private void ExibirCabecalho(
        long total,
        int totalGrupos,
        int totalTags,
        string databasePath,
        bool bancoLimpo,
        bool modoRapido,
        BenchmarkFastPlan? planoRapido)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "       EXECUÇÃO DO BENCHMARK");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Cliente: {_options.ClienteId}");

        Console.WriteLine(
            $"Modo: {(modoRapido ? "FAST / " : string.Empty)}" +
            $"{(bancoLimpo ? "BANCO LIMPO" : "BANCO COM ANOMALIAS")}");

        if (modoRapido &&
            planoRapido is not null)
        {
            Console.WriteLine(
                $"Amostra FAST: {planoRapido.PlannedSampleCount:N0} " +
                $"de {planoRapido.FullSampleCount:N0} leituras");

            Console.WriteLine(
                $"Janelas FAST: {planoRapido.WindowCount} x " +
                $"~{planoRapido.WindowLengthMinutes:N0} minutos");
        }

        Console.WriteLine(
            $"Banco: {databasePath}");

        Console.WriteLine(
            $"Leituras previstas: {total:N0}");

        Console.WriteLine(
            $"Tags analisadas: {totalTags}");

        Console.WriteLine(
            $"Grupos multivariados: {totalGrupos}");

        Console.WriteLine(
            $"Histórico mínimo: " +
            $"{_options.MinimumHistoryPoints}");

        Console.WriteLine(
            $"Janela histórica: " +
            $"{_options.HistoryWindowSize}");

        Console.WriteLine();
    }

    private static void ExibirProgressoSeNecessario(
        BenchmarkExecutionSummary summary,
        long totalEsperado,
        Stopwatch stopwatch,
        BenchmarkPerformanceMetrics performance)
    {
        if (summary.TotalLeiturasLidas %
            IntervaloProgresso != 0)
        {
            return;
        }

        var segundos =
            stopwatch.Elapsed.TotalSeconds;

        var velocidade =
            segundos > 0
                ? summary.TotalLeiturasLidas /
                  segundos
                : 0;

        var percentual =
            totalEsperado > 0
                ? summary.TotalLeiturasLidas *
                  100.0 /
                  totalEsperado
                : 0;

        var restantes =
            Math.Max(
                0,
                totalEsperado -
                summary.TotalLeiturasLidas);

        var eta =
            velocidade > 0
                ? TimeSpan.FromSeconds(
                    restantes /
                    velocidade)
                : TimeSpan.Zero;

        Console.WriteLine(
            $"Progresso: {percentual:F2}% | " +
            $"{summary.TotalLeiturasLidas:N0}/" +
            $"{totalEsperado:N0} | " +
            $"{velocidade:N0} leituras/s | " +
            $"ETA {eta:hh\\:mm\\:ss} | " +
            $"Anomalias " +
            $"{summary.TotalAnomaliasDetectadas:N0} | " +
            $"MV grupos {performance.TotalGruposAnalisados:N0} | " +
            $"MV anomalias {performance.TotalEventosMultivariadosAnomalos:N0} | " +
            $"Erros {summary.TotalErros:N0}");
    }

    private static void ExibirResumo(
        BenchmarkExecutionSummary summary,
        string detectionsPath,
        string errorsPath,
        string summaryPath,
        string performancePath,
        BenchmarkPerformanceMetrics performance)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "       BENCHMARK FINALIZADO");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Leituras lidas: " +
            $"{summary.TotalLeiturasLidas:N0}");

        Console.WriteLine(
            $"Aquecimento: " +
            $"{summary.TotalLeiturasAquecimento:N0}");

        Console.WriteLine(
            $"Detecções executadas: " +
            $"{summary.TotalDeteccoesExecutadas:N0}");

        Console.WriteLine(
            $"Anomalias gravadas: " +
            $"{summary.TotalAnomaliasDetectadas:N0}");

        Console.WriteLine(
            $"Erros: " +
            $"{summary.TotalErros:N0}");

        Console.WriteLine(
            $"Duração: " +
            $"{summary.DuracaoSegundos:N2} segundos");

        Console.WriteLine(
            $"Detecções: {detectionsPath}");

        Console.WriteLine(
            $"Erros: {errorsPath}");

        Console.WriteLine(
            $"Grupos multivariados analisados: " +
            $"{performance.TotalGruposAnalisados:N0}");

        Console.WriteLine(
            $"Eventos multivariados anômalos: " +
            $"{performance.TotalEventosMultivariadosAnomalos:N0}");

        Console.WriteLine(
            $"Linhas univariadas gravadas: " +
            $"{performance.TotalLinhasUnivariadasGravadas:N0}");

        Console.WriteLine(
            $"Linhas multivariadas gravadas: " +
            $"{performance.TotalLinhasMultivariadasGravadas:N0}");

        Console.WriteLine(
            $"Tempo univariado: " +
            $"{performance.SegundosUnivariado:N2} s");

        Console.WriteLine(
            $"Tempo multivariado: " +
            $"{performance.SegundosMultivariado:N2} s");

        Console.WriteLine(
            $"Resumo: {summaryPath}");

        Console.WriteLine(
            $"Performance: {performancePath}");
    }

    private static double TicksParaSegundos(
        long ticks)
    {
        return ticks /
            (double)Stopwatch.Frequency;
    }
}

public sealed class BenchmarkPerformanceMetrics
{
    public string ClienteId { get; set; } = "";

    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    public double DuracaoTotalSegundos { get; set; }

    public int TotalTagsLidas { get; set; }

    public int TotalTagsUnivariadas { get; set; }

    public int TotalGruposConfigurados { get; set; }

    public long TotalLeiturasUnivariadas { get; set; }

    public long TotalLeiturasSomenteMultivariadas { get; set; }

    public long TotalGruposFinalizados { get; set; }

    public long TotalGruposAnalisados { get; set; }

    public long TotalGruposNaoAplicaveis { get; set; }

    public long TotalGruposNormais { get; set; }

    public long TotalEventosMultivariadosAnomalos { get; set; }

    public long TotalLinhasUnivariadasGravadas { get; set; }

    public long TotalLinhasMultivariadasGravadas { get; set; }

    public long TotalErrosMultivariados { get; set; }

    public long TicksUnivariado { get; set; }

    public long TicksMultivariado { get; set; }

    public double SegundosUnivariado { get; set; }

    public double SegundosMultivariado { get; set; }
}