using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.Services.AnomalyDetection;
using IAApi.Benchmark.BehaviorProfiles.Metadata;
using IAApi.Benchmark.BehaviorProfiles.Diagnostics;
using IAApi.Benchmark.DetectorApplicabilityCounterfactual;

using BenchmarkBehaviorProfileV2SummaryCollector =
    IAApi.Benchmark.BehaviorProfiles.V2.BehaviorProfileV2SummaryCollector;

using BenchmarkBehaviorProfileMaturityStateStore =
    IAApi.Benchmark.BehaviorProfiles.V2.Maturity.BehaviorProfileMaturityStateStore;

using BenchmarkBehaviorProfileMaturitySummaryCollector =
    IAApi.Benchmark.BehaviorProfiles.V2.Maturity.BehaviorProfileMaturitySummaryCollector;

using BenchmarkBehaviorProfileReadinessStateStore =
    IAApi.Benchmark.BehaviorProfiles.V2.Readiness.BehaviorProfileReadinessStateStore;

using ServiceBehaviorProfileMaturityOptions =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity.BehaviorProfileMaturityOptions;

using ServiceBehaviorProfileTemporalStabilizer =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity.BehaviorProfileTemporalStabilizer;

using ServiceBehaviorProfileMaturityLifecycle =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity.BehaviorProfileMaturityLifecycle;

using ServiceBehaviorProfileStructuralEvidenceEvaluator =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural.BehaviorProfileStructuralEvidenceEvaluator;

using ServiceStructuralAmbiguityResolver =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural.StructuralAmbiguityResolver;

using ServiceBehaviorProfileDecisionReadinessEvaluator =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness.BehaviorProfileDecisionReadinessEvaluator;

namespace IAApi.Benchmark.BehaviorProfiles;

/// <summary>
/// Writer observacional fail-safe. Falhas nesta camada nunca interrompem o
/// benchmark nem retornam informacao ao pipeline principal.
/// </summary>
public sealed class BenchmarkBehaviorProfileObserver : IAsyncDisposable
{
    private readonly BenchmarkBehaviorProfileOptions _options;

    private readonly BenchmarkBehaviorProfileAdapter _adapter = new();

    private readonly BenchmarkBehaviorProfileV2SummaryCollector _summaryCollector =
        new();

    private readonly ServiceBehaviorProfileMaturityOptions _maturityOptions =
        new();

    private readonly ServiceBehaviorProfileTemporalStabilizer _stabilizer =
        new();

    private readonly ServiceBehaviorProfileStructuralEvidenceEvaluator _structural =
        new();

    private readonly ServiceStructuralAmbiguityResolver _ambiguityResolver =
        new();

    private readonly ServiceBehaviorProfileDecisionReadinessEvaluator _readinessEvaluator =
        new();

    private readonly BenchmarkBehaviorProfileReadinessStateStore _readinessStateStore =
        new();

    private readonly ManifestMetadataAdapter? _manifestMetadata;

    private readonly BehaviorProfileClosureEpisodeCollector _closureCollector =
        new();

    private readonly BenchmarkBehaviorProfileMaturityStateStore _maturityStateStore =
        new();

    private readonly BenchmarkBehaviorProfileMaturitySummaryCollector
        _maturitySummaryCollector;

    private readonly DetectorApplicabilityCounterfactualObserver?
        _applicabilityCounterfactual;

    private readonly SQLiteHistoricoService _shadowHistory;

    private readonly int _minimumHistoryPoints;

    private readonly StreamWriter _writer;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            DefaultIgnoreCondition =
                JsonIgnoreCondition.Never,

            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private bool _disabledAfterFailure;

    private BenchmarkBehaviorProfileObserver(
        BenchmarkBehaviorProfileOptions options,
        int historyWindowSize,
        int minimumHistoryPoints,
        string databasePath,
        string path,
        string summaryPath,
        string closurePath)
    {
        _options =
            options;

        _maturitySummaryCollector =
            new BenchmarkBehaviorProfileMaturitySummaryCollector(
                _maturityOptions);

        _shadowHistory =
            new SQLiteHistoricoService(
                historyWindowSize);

        _minimumHistoryPoints =
            minimumHistoryPoints;

        var manifestPath =
            databasePath +
            ".manifest.json";

        _manifestMetadata =
            File.Exists(manifestPath)
                ? ManifestMetadataAdapter.Load(
                    manifestPath)
                : null;

        _applicabilityCounterfactual =
            options.ApplicabilityCounterfactualEnabled
                ? new DetectorApplicabilityCounterfactualObserver(
                    Path.GetDirectoryName(path)!,
                    Path.GetFileNameWithoutExtension(path)
                        .Replace(
                            "tag_behavior_profile_trace_",
                            "",
                            StringComparison.Ordinal))
                : null;

        TracePath =
            path;

        SummaryPath =
            summaryPath;

        ClosurePath =
            closurePath;

        _writer =
            new StreamWriter(
                new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.SequentialScan),
                new UTF8Encoding(false));
    }

    public string TracePath { get; }

    public string SummaryPath { get; }

    public string ClosurePath { get; }

    public string? ApplicabilityCounterfactualTracePath =>
        _applicabilityCounterfactual?.TracePath;

    public string? ApplicabilityCounterfactualSummaryPath =>
        _applicabilityCounterfactual?.SummaryPath;

    public void ResetWindow(
        int fastWindowIndex)
    {
        _maturityStateStore.ResetWindow(
            fastWindowIndex);

        _readinessStateStore.ResetWindow();

        _closureCollector.ResetWindow();

        _shadowHistory.LimparTudo();
    }

    public static BenchmarkBehaviorProfileObserver? TryCreate(
        BenchmarkBehaviorProfileOptions options,
        int historyWindowSize,
        int minimumHistoryPoints,
        string databasePath,
        string resultDirectory,
        string executionId)
    {
        if (!options.Enabled)
        {
            return null;
        }

        try
        {
            return new BenchmarkBehaviorProfileObserver(
                options,
                historyWindowSize,
                minimumHistoryPoints,
                databasePath,
                Path.Combine(
                    resultDirectory,
                    $"tag_behavior_profile_trace_{executionId}.jsonl"),
                Path.Combine(
                    resultDirectory,
                    $"tag_behavior_profile_summary_{executionId}.json"),
                Path.Combine(
                    resultDirectory,
                    $"tag_behavior_profile_closure_{executionId}.json"));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Observabilidade de perfil desativada: {exception.Message}");

            return null;
        }
    }

    /// <summary>
    /// Caminho Shadow para observar inclusive tags que nao participam do
    /// ensemble univariado. O historico pertence somente ao observer, usa a
    /// mesma capacidade do benchmark e nunca e devolvido ao pipeline ativo.
    /// A leitura atual e adicionada somente depois da inferencia.
    /// </summary>
    public async ValueTask ObserveReadingAsync(
        int fastWindowIndex,
        int minuteIndex,
        string clientId,
        string tag,
        DateTime timestamp,
        float currentValue,
        string legacyTipoTag)
    {
        if (
            _disabledAfterFailure ||
            !_options.Tags.Contains(tag))
        {
            return;
        }

        try
        {
            if (
                _shadowHistory.ObterQuantidade(
                    clientId,
                    tag) >=
                _minimumHistoryPoints)
            {
                var snapshot =
                    _shadowHistory.CriarSnapshotDetector(
                        clientId,
                        tag);

                await ObserveAsync(
                    fastWindowIndex,
                    minuteIndex,
                    new DetectorInput
                    {
                        ClienteId =
                            clientId,

                        TagName =
                            tag,

                        TipoTag =
                            legacyTipoTag,

                        ValorAtual =
                            currentValue,

                        DataHoraAtual =
                            timestamp,

                        MediaHistorica =
                            snapshot.Media,

                        DesvioPadraoHistorico =
                            snapshot.DesvioPadrao,

                        Historico =
                            snapshot.Historico
                    });
            }
        }
        catch (Exception exception)
        {
            _disabledAfterFailure =
                true;

            Console.Error.WriteLine(
                "Observabilidade de perfil desativada apos falha: " +
                exception.Message);
        }
        finally
        {
            _shadowHistory.AdicionarLeitura(
                clientId,
                tag,
                timestamp,
                currentValue);
        }
    }

    public async ValueTask ObserveAsync(
        int fastWindowIndex,
        int minuteIndex,
        DetectorInput legacyInput)
    {
        if (
            _disabledAfterFailure ||
            !_options.Tags.Contains(
                legacyInput.TagName))
        {
            return;
        }

        try
        {
            var manifestMetadata =
                _manifestMetadata?.Adapt(
                    legacyInput.TagName);

            var observation =
                _adapter.Adapt(
                    fastWindowIndex,
                    minuteIndex,
                    legacyInput,
                    manifestMetadata?.Declaration);

            manifestMetadata ??=
                new ManifestDeclaredTagMetadata
                {
                    Tag =
                        legacyInput.TagName,

                    Source =
                        "BenchmarkManifest",

                    MetadataWasAbsent =
                        true,

                    Declaration =
                        observation.DeclaredTagType
                };

            var previous =
                _maturityStateStore.Get(
                    legacyInput.ClienteId,
                    legacyInput.TagName);

            var maturity =
                _stabilizer.Evaluate(
                    previous,
                    observation.InferredBehaviorProfileV2,
                    legacyInput.DataHoraAtual,
                    minuteIndex,
                    _maturityOptions);

            _maturityStateStore.Set(
                legacyInput.ClienteId,
                legacyInput.TagName,
                maturity.NextState);

            var structural =
                _structural.Evaluate(
                    observation.InferredBehaviorProfileV2,
                    legacyInput.Historico,
                    maturity.Observation.MaturityState ==
                    ServiceBehaviorProfileMaturityLifecycle.Stable
                    &&
                    maturity.Observation.StableProfile ==
                    observation.InferredBehaviorProfileV2.Type);

            var resolution =
                _ambiguityResolver.Resolve(
                    observation.InferredBehaviorProfileV2,
                    structural,
                    legacyInput.Historico,
                    observation.DeclaredTagType);

            var readinessState =
                _readinessStateStore.Get(
                    legacyInput.ClienteId,
                    legacyInput.TagName);

            var readiness =
                _readinessEvaluator.Evaluate(
                    readinessState,
                    observation.DeclaredTagType,
                    observation.InferredBehaviorProfileV2,
                    maturity.Observation,
                    structural,
                    resolution,
                    legacyInput.DataHoraAtual,
                    minuteIndex);

            _readinessStateStore.Set(
                legacyInput.ClienteId,
                legacyInput.TagName,
                readiness.NextState);

            observation =
                observation with
                {
                    StableBehaviorProfile =
                        maturity.Observation,

                    StructuralEvidence =
                        structural,

                    StructuralAmbiguityResolution =
                        resolution,

                    ManifestMetadata =
                        manifestMetadata,

                    DecisionReadiness =
                        readiness.Readiness
                };

            _summaryCollector.Add(
                observation);

            _maturitySummaryCollector.Add(
                observation);

            _closureCollector.Add(
                observation);

            _applicabilityCounterfactual?.Observe(
                observation);

            if (
                _options.ShouldTrace(
                    legacyInput.TagName,
                    minuteIndex))
            {
                await _writer.WriteLineAsync(
                    JsonSerializer.Serialize(
                        observation,
                        _jsonOptions));
            }
        }
        catch (Exception exception)
        {
            _disabledAfterFailure =
                true;

            Console.Error.WriteLine(
                "Observabilidade de perfil desativada apos falha: " +
                exception.Message);
        }
    }

    public void RecordOriginalDecision(
        string tag,
        int minuteIndex,
        DetectorResult result) =>
        _applicabilityCounterfactual?.RecordOriginalDecision(
            tag,
            minuteIndex,
            result);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _writer.DisposeAsync();

            if (
                _applicabilityCounterfactual is not null)
            {
                await _applicabilityCounterfactual.DisposeAsync();
            }

            await File.WriteAllTextAsync(
                SummaryPath,
                JsonSerializer.Serialize(
                    _summaryCollector.Build(
                        _maturitySummaryCollector.Build()),
                    new JsonSerializerOptions(
                        _jsonOptions)
                    {
                        WriteIndented =
                            true
                    }));

            await File.WriteAllTextAsync(
                ClosurePath,
                JsonSerializer.Serialize(
                    _closureCollector.Build(),
                    new JsonSerializerOptions(
                        _jsonOptions)
                    {
                        WriteIndented =
                            true
                    }));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "Falha ao finalizar observabilidade de perfil: " +
                exception.Message);
        }
    }
}