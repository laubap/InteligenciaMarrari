using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IAApi.Tests.IntelligenceLab;

/// <summary>
/// Intelligence Lab 2.2G.2 a 2.2G.4: exercita o ciclo completo de
/// previsão com SQLite isolado, detectores reais e somente a
/// fronteira de aquisição de histórico simulada.
///
/// Os seis cenários originais executam detectores estatísticos; o cenário
/// adicional integra Isolation Forest com um modelo temporário de teste.
/// O runtime funcional é testado em cenários dedicados. A multivariada
/// é exercitada em shadow e em promoção estritamente isolada por fixture,
/// sem carregar opções hospedadas nem habilitar clientes reais.
/// </summary>
public sealed class IntelligenceLabBehaviorBaselineEndToEndTests
{
    private const string ClienteId = "IL-BASELINE";
    private const string TagName = "BOMBA.PRESSAO";
    private const string RegimeTagName = "BOMBA.REGIME";
    private const string CompanionTagName = "BOMBA.VAZAO";
    private const string MultivariateGroupName = "IL-BOMBA-PRESSAO-VAZAO";

    private static readonly DateTime ReadingTime =
        new(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc);

    private static readonly string[] StatisticalDetectorNames =
    [
        "Z-Score",
        "Mediana e MAD",
        "EWMA",
        "Taxa de variação",
        "Persistência",
        "Mudança de patamar"
    ];

    [Fact]
    public async Task GlobalBaseline_StableHistory_CompletesRealPredictionWithoutCreatingBehaviorMemory()
    {
        var history = CreateStableHistory(center: 100);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            history);

        Assert.NotNull(run.Response);
        Assert.Equal(ClienteId, run.Response.ClienteId);
        Assert.Equal(TagName, run.Response.TagName);
        Assert.True(double.IsFinite(run.Response.Score));
        Assert.NotNull(run.Response.Interpretacao);

        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.Global, run.Diagnostic.Baseline!.Kind);
        Assert.True(run.Diagnostic.Baseline.Applied);
        Assert.Equal(100, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(2, run.Diagnostic.Baseline.StandardDeviation, precision: 6);
        Assert.Null(run.Diagnostic.Baseline.BehaviorCode);
        Assert.Null(run.Diagnostic.Baseline.BehaviorSnapshotVersion);

        AssertRealStatisticalDetectorsExecuted(run);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking()
            .ToListAsync();

        var operationalResult = Assert.Single(persisted);
        Assert.Equal(ClienteId, operationalResult.ClienteId);
        Assert.Equal(TagName, operationalResult.TagName);
        Assert.Equal(run.Response.EhAnomalia, operationalResult.EhAnomalia);
        Assert.Equal((double)run.Response.Score, operationalResult.Score, precision: 5);

        Assert.Empty(await fixture.Context.TagsComportamentosIa.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Context.TagsComportamentosSnapshotsIa.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task KnownNormalBehavior_AppliesBehaviorBaselineToDetectorAndPreservesMemory()
    {
        var behaviorHistory = CreateStableHistory(center: 40);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        var snapshot = await SeedKnownBehaviorAsync(
            fixture.Context,
            behaviorHistory,
            TagComportamentoIa.ClassificacaoNormal,
            "BEH-001");

        var memoryBefore = await CaptureBehaviorMemoryAsync(fixture.Context);

        var historySource = new InMemoryHistorySource(behaviorHistory);
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var preparation = CreatePreparationService(
            fixture.Context,
            historySource,
            behaviorProvider);

        // Confere diretamente o contrato que será consumido pelo serviço de
        // produção: reconhecimento, baseline e o DetectorInput resultante.
        var prepared = await preparation.PrepararAsync(
            CreateRequest(behaviorHistory),
            await fixture.Context.PerfisIa.SingleAsync(),
            "continuous",
            ReadingTime);

        Assert.True(prepared.ComportamentoReconhecido);
        Assert.True(prepared.BaselineComportamentalAplicado);
        Assert.Equal("BEH-001", prepared.CodigoComportamentoReconhecido);
        Assert.Equal(TagComportamentoIa.ClassificacaoNormal, prepared.ClassificacaoComportamentoReconhecido);
        Assert.NotNull(prepared.BaselineEfetivo);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, prepared.BaselineEfetivo!.Kind);
        Assert.Equal("BEH-001", prepared.BaselineEfetivo.BehaviorCode);
        Assert.Equal(snapshot.Versao, prepared.BaselineEfetivo.BehaviorSnapshotVersion);
        Assert.Equal(snapshot.Media, prepared.EntradaDetector.MediaHistorica, precision: 6);
        Assert.Equal(snapshot.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico, precision: 6);
        Assert.Equal(snapshot.DeadbandRecomendado, prepared.EntradaDetector.VariacaoMinimaRelevante, precision: 6);
        Assert.Equal(snapshot.ConfiancaDeadband, prepared.EntradaDetector.ConfiancaVariacaoMinima, precision: 6);

        var globalProfile = await fixture.Context.PerfisIa.SingleAsync();
        Assert.NotEqual(globalProfile.Media, prepared.EntradaDetector.MediaHistorica);
        Assert.NotEqual(globalProfile.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico);
        Assert.NotEqual(globalProfile.VariacaoMinimaRelevante, prepared.EntradaDetector.VariacaoMinimaRelevante);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            behaviorHistory,
            historySource,
            behaviorProvider,
            preparation);

        Assert.NotNull(run.Response);
        Assert.Equal(ClienteId, run.Response.ClienteId);
        Assert.Equal(TagName, run.Response.TagName);
        Assert.True(double.IsFinite(run.Response.Score));
        Assert.NotNull(run.Response.Interpretacao);
        Assert.Equal(snapshot.Media, run.Response.Perfil.MediaHistorica, precision: 6);

        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, run.Diagnostic.Baseline!.Kind);
        Assert.True(run.Diagnostic.Baseline.Applied);
        Assert.Equal("BEH-001", run.Diagnostic.Baseline.BehaviorCode);
        Assert.Equal(snapshot.Versao, run.Diagnostic.Baseline.BehaviorSnapshotVersion);
        Assert.Equal(snapshot.Media, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(behaviorHistory.Length, run.Diagnostic.Baseline.SampleCount);
        Assert.NotEqual(globalProfile.Media, run.Diagnostic.Baseline.Mean);
        Assert.NotEqual(globalProfile.DesvioPadrao, run.Diagnostic.Baseline.StandardDeviation);
        Assert.NotEqual(globalProfile.VariacaoMinimaRelevante, run.Diagnostic.Baseline.Deadband);

        AssertRealStatisticalDetectorsExecuted(run);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking()
            .ToListAsync();

        var operationalResult = Assert.Single(persisted);
        Assert.Equal(ClienteId, operationalResult.ClienteId);
        Assert.Equal(TagName, operationalResult.TagName);
        Assert.Equal(run.Response.EhAnomalia, operationalResult.EhAnomalia);
        Assert.Equal((double)run.Response.Score, operationalResult.Score, precision: 5);

        // O provider B3.25 é read-only inclusive quando consultado mais de
        // uma vez (preparação explícita e previsão completa). A comparação
        // cobre a identidade, o lifecycle e todas as estatísticas persistidas
        // no snapshot, não apenas a quantidade de registros.
        Assert.Equal(memoryBefore, await CaptureBehaviorMemoryAsync(fixture.Context));
    }

    [Fact]
    public async Task KnownNormalBehavior_SevenRealDetectors_ReachOnePredictionAndDiagnostic()
    {
        // O Isolation Forest requer pelo menos 50 pontos. Nao modificar os
        // seis cenarios anteriores (que usam apenas 40 pontos).
        var behaviorHistory = CreateStableHistory(center: 40, count: 96);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        var snapshot = await SeedKnownBehaviorAsync(
            fixture.Context,
            behaviorHistory,
            TagComportamentoIa.ClassificacaoNormal,
            "BEH-IF-E2E");
        var memoryBefore = await CaptureBehaviorMemoryAsync(fixture.Context);

        var historySource = new InMemoryHistorySource(behaviorHistory);
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var preparation = CreatePreparationService(
            fixture.Context, historySource, behaviorProvider);
        var prepared = await preparation.PrepararAsync(
            CreateRequest(behaviorHistory),
            await fixture.Context.PerfisIa.SingleAsync(),
            "continuous",
            ReadingTime);

        Assert.True(prepared.ComportamentoReconhecido);
        Assert.True(prepared.BaselineComportamentalAplicado);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, prepared.BaselineEfetivo!.Kind);
        Assert.Equal("BEH-IF-E2E", prepared.CodigoComportamentoReconhecido);
        Assert.Equal(snapshot.Media, prepared.EntradaDetector.MediaHistorica, precision: 6);
        Assert.True(prepared.EntradaDetector.Historico.Count >= 50,
            "O baseline B3.25 reduziu o historico abaixo do minimo do Isolation Forest.");
        Assert.All(prepared.EntradaDetector.Historico,
            point => Assert.True(point.DataHora <= ReadingTime));

        // Usa exclusivamente um modelo efemero, sem tocar nos modelos reais.
        // O treino serve para exercitar o caminho E2E, NAO para medir acuracia.
        using var modelFixture = new E2EIsolationForestModelFixture();
        var forestDetector = await modelFixture.TrainAndCreateDetectorAsync(
            prepared.EntradaDetector);
        var run = await ExecutePredictionAsync(
            fixture.Context,
            behaviorHistory,
            historySource,
            behaviorProvider,
            preparation,
            forestDetector);

        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, run.Diagnostic.Baseline!.Kind);
        Assert.Equal("BEH-IF-E2E", run.Diagnostic.Baseline.BehaviorCode);
        Assert.Equal(snapshot.Versao, run.Diagnostic.Baseline.BehaviorSnapshotVersion);
        Assert.Equal(snapshot.Media, run.Response.Perfil.MediaHistorica, precision: 6);

        Assert.Equal(7, run.Response.Detectores.Count);
        Assert.Equal(7, run.Diagnostic.Detectors.Count);
        Assert.NotNull(run.Diagnostic.Ensemble);
        Assert.Equal(7, run.Diagnostic.Ensemble!.RegisteredDetectorCount);
        Assert.Equal(7, run.Diagnostic.Ensemble.ApplicableDetectorCount);
        foreach (var name in StatisticalDetectorNames)
        {
            Assert.Contains(run.Response.Detectores,
                detector => detector.Nome == name && detector.Aplicavel);
            Assert.Contains(run.Diagnostic.Detectors,
                detector => detector.Name == name &&
                            detector.Status == DetectorDiagnosticStatus.Executed);
        }

        var isolationResponse = Assert.Single(run.Response.Detectores,
            detector => detector.Nome == "Isolation Forest");
        Assert.True(isolationResponse.Aplicavel);
        var isolationDiagnostic = Assert.Single(run.Diagnostic.Detectors,
            detector => detector.Name == "Isolation Forest");
        Assert.Equal(DetectorDiagnosticStatus.Executed, isolationDiagnostic.Status);
        Assert.NotNull(isolationDiagnostic.OriginalScore);
        Assert.NotNull(isolationDiagnostic.NormalizedScore);
        Assert.True(double.IsFinite(isolationDiagnostic.OriginalScore!.Value));
        Assert.InRange(isolationDiagnostic.NormalizedScore!.Value, 0, 100);
        Assert.Equal(modelFixture.Options.Weight, isolationDiagnostic.Weight, precision: 6);
        Assert.Equal(modelFixture.Options.AnomalyThreshold,
            isolationDiagnostic.Metrics["threshold"], precision: 6);
        Assert.Equal(modelFixture.Options.TreeCount,
            isolationDiagnostic.Metrics["quantidadeArvores"]);
        Assert.Equal(1, isolationDiagnostic.Metrics["versaoModelo"]);

        AssertValidPersistedPrediction(run,
            await fixture.Context.AnomaliasDetectadas.AsNoTracking().ToListAsync());
        Assert.Equal(memoryBefore, await CaptureBehaviorMemoryAsync(fixture.Context));
    }

    [Theory]
    [InlineData("disabled", true, "LegacyFeatureDisabled")]
    [InlineData("excluded", true, "LegacyCanaryExcluded")]
    [InlineData("allowed", false, null)]
    public async Task RuntimeDecisionCanary_RealPredictionRespectsRolloutAndPreservesAudit(
        string mode,
        bool expectedLegacyFallback,
        string? expectedFallbackSource)
    {
        // Testa a rota real Ensemble -> contexto funcional -> resolver ->
        // finalizacao -> diagnostico. Um historico estavel evita afirmar
        // artificialmente que a decisao funcional precise divergir do Legacy.
        var history = CreateStableHistory(center: 100);
        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);

        var rollout = new RuntimeAnomalyDecisionOptions
        {
            Enabled = mode != "disabled",
            CanaryOnly = mode != "disabled",
            AllowedClientIds = mode == "allowed" ? [ClienteId] : ["OTHER-CLIENT"]
        };

        var run = await ExecutePredictionAsync(
            fixture.Context,
            history,
            runtimeDecisionOptions: rollout);

        var runtime = Assert.IsType<RuntimeDecisionDiagnostic>(run.Diagnostic.RuntimeDecision);
        Assert.Equal(expectedLegacyFallback, runtime.UsedLegacyFallback);
        Assert.Equal(run.Response.EhAnomalia, runtime.FinalAnomaly);
        Assert.False(string.IsNullOrWhiteSpace(runtime.Source));

        if (expectedFallbackSource is not null)
        {
            Assert.Equal(expectedFallbackSource, runtime.Source);
            Assert.Equal(runtime.LegacyAnomaly, runtime.FinalAnomaly);
            Assert.Contains("LegacyPreserved", runtime.Reasons);
            Assert.Contains(
                mode == "disabled"
                    ? "RuntimeDecisionDisabled"
                    : "ClientNotEnabledForRuntimeDecision",
                runtime.Reasons);
        }
        else
        {
            // A decisao funcional foi aceita, mas NAO e obrigatorio mudar
            // o voto em um historico normal; isso nao e uma falha do runtime.
            Assert.NotEqual("LegacyFeatureDisabled", runtime.Source);
            Assert.NotEqual("LegacyCanaryExcluded", runtime.Source);
            Assert.Contains("RuntimeFunctionalDecisionAccepted", runtime.Reasons);
        }

        // Prova integrada do fluxo resolver -> PredictionService -> captura.
        // Os codigos acima sao fixos e classificados para telemetria segura.
        Assert.DoesNotContain(
            "RuntimeDecisionReasons",
            run.Diagnostic.Completeness.MissingSections);

        AssertRealStatisticalDetectorsExecuted(run);
        AssertValidPersistedPrediction(run,
            await fixture.Context.AnomaliasDetectadas.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task FrozenSensor_FunctionalDecisionPromotesRealLegacyNormalAndAuditsReasons()
    {
        // O sinal varia normalmente e passa a ficar exatamente congelado
        // durante os minutos finais. Os valores continuam dentro do baseline:
        // o evento de qualidade nao deve ser fabricado por um detector fake.
        var history = CreateStableHistory(center: 100, count: 60);
        for (var index = history.Length - 12; index < history.Length; index++)
        {
            history[index].Valor = 100f;
        }

        // Contrafactual: mesmos dados e detectores reais, runtime desligado.
        await using var legacyFixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(legacyFixture.Context);
        var legacyRun = await ExecutePredictionAsync(
            legacyFixture.Context,
            history,
            runtimeDecisionOptions: new RuntimeAnomalyDecisionOptions { Enabled = false });

        var legacy = Assert.IsType<RuntimeDecisionDiagnostic>(
            legacyRun.Diagnostic.RuntimeDecision);
        Assert.True(legacy.UsedLegacyFallback);
        Assert.Equal(legacy.LegacyAnomaly, legacy.FinalAnomaly);
        Assert.False(legacy.LegacyAnomaly);
        Assert.False(legacyRun.Response.EhAnomalia);
        AssertRealStatisticalDetectorsExecuted(legacyRun);
        AssertValidPersistedPrediction(legacyRun,
            await legacyFixture.Context.AnomaliasDetectadas.AsNoTracking().ToListAsync());

        // Mesmo historico, nova fixture SQLite isolada e runtime autorizado.
        await using var enabledFixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(enabledFixture.Context);
        var enabledRun = await ExecutePredictionAsync(
            enabledFixture.Context,
            history,
            runtimeDecisionOptions: new RuntimeAnomalyDecisionOptions
            {
                Enabled = true,
                CanaryOnly = true,
                AllowedClientIds = [ClienteId]
            });

        var runtime = Assert.IsType<RuntimeDecisionDiagnostic>(
            enabledRun.Diagnostic.RuntimeDecision);
        Assert.False(runtime.LegacyAnomaly);
        Assert.True(runtime.FinalAnomaly);
        Assert.False(runtime.UsedLegacyFallback);
        Assert.Equal(RuntimeFunctionalDecisionSources.DataQualityUncertain, runtime.Source);
        Assert.Contains("FrozenSensorDetected", runtime.Reasons);
        Assert.Contains("IndependentDataQualityEvidence", runtime.Reasons);
        Assert.Contains("RuntimeFunctionalDecisionAccepted", runtime.Reasons);
        Assert.DoesNotContain("RuntimeDecisionReasons",
            enabledRun.Diagnostic.Completeness.MissingSections);
        Assert.True(enabledRun.Response.EhAnomalia);
        AssertRealStatisticalDetectorsExecuted(enabledRun);
        AssertValidPersistedPrediction(enabledRun,
            await enabledFixture.Context.AnomaliasDetectadas.AsNoTracking().ToListAsync());

        // O modelo de runtime nao pode transformar evidencia de congelamento
        // em alteracao dos algoritmos Legacy: compara somente a sua decisao.
        Assert.Equal(legacy.LegacyAnomaly, runtime.LegacyAnomaly);
    }

    [Fact]
    public Task KnownAbnormalBehavior_RecognizedButNotApplied_FallsBackToGlobalBaseline()
    {
        return AssertKnownNonNormalBehaviorFallsBackToGlobalBaselineAsync(
            TagComportamentoIa.ClassificacaoKnownAbnormal,
            "BEH-ABNORMAL");
    }

    [Fact]
    public Task KnownUndefinedBehavior_RecognizedButNotApplied_FallsBackToGlobalBaseline()
    {
        return AssertKnownNonNormalBehaviorFallsBackToGlobalBaselineAsync(
            TagComportamentoIa.ClassificacaoUndefined,
            "BEH-UNDEFINED");
    }

    [Fact]
    public async Task OperationalRegimeBaseline_TakesPriorityOverKnownNormalBehavior()
    {
        const int regimeZeroSamples = 8;
        const int regimeOneSamples = 32;
        var targetHistory = CreateTwoRegimeTargetHistory();
        var expectedRegimeOneHistory = targetHistory
            .Skip(regimeZeroSamples)
            .ToArray();
        var regimeHistory = CreateRegimeHistory(
            targetHistory,
            regimeZeroSamples);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        var behaviorSnapshot = await SeedKnownBehaviorAsync(
            fixture.Context,
            targetHistory,
            TagComportamentoIa.ClassificacaoNormal,
            "BEH-REGIME-PRIORITY");
        await SeedOperationalRegimeConfigurationAsync(fixture.Context, active: true);

        var memoryBefore = await CaptureBehaviorMemoryAsync(fixture.Context);
        var regimeBefore = await CaptureOperationalRegimeAsync(fixture.Context);
        var historySource = new InMemoryHistorySource(
            new HistorySourceEntry(ClienteId, TagName, targetHistory),
            new HistorySourceEntry(ClienteId, RegimeTagName, regimeHistory));
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var regimeProvider = CreateOperationalRegimeProvider(fixture.Context, historySource);
        var preparation = CreatePreparationService(
            fixture.Context,
            historySource,
            behaviorProvider,
            regimeProvider);
        var globalProfile = await fixture.Context.PerfisIa.SingleAsync();
        var statisticsCalculator = new TagProfileStatisticsCalculator(
            new HistoricoLimpezaService());
        var fullHistoryStatistics = statisticsCalculator.Calculate(targetHistory);
        var expectedRegimeStatistics = statisticsCalculator.Calculate(expectedRegimeOneHistory);

        Assert.Equal(40, targetHistory.Length);
        Assert.Equal(regimeOneSamples, expectedRegimeOneHistory.Length);
        Assert.All(targetHistory, reading =>
            Assert.True(reading.DataHora <= ReadingTime));
        Assert.All(regimeHistory, reading =>
            Assert.True(reading.DataHora <= ReadingTime));

        var behaviorResolution = await behaviorProvider.ResolveAsync(
            ClienteId,
            TagName,
            targetHistory,
            ReadingTime);

        Assert.True(behaviorResolution.Recognized, behaviorResolution.Reason);
        Assert.True(behaviorResolution.CanApplyBaseline, behaviorResolution.Reason);
        Assert.Equal("BEH-REGIME-PRIORITY", behaviorResolution.BehaviorCode);
        Assert.Equal(behaviorSnapshot.Versao, behaviorResolution.SnapshotVersion);
        Assert.NotNull(behaviorResolution.Media);
        Assert.Equal(behaviorSnapshot.Media, behaviorResolution.Media.Value, precision: 6);

        var regimeResolution = regimeProvider.Resolve(
            ClienteId,
            TagName,
            targetHistory,
            ReadingTime);

        Assert.True(regimeResolution.Applied, regimeResolution.Reason);
        Assert.NotNull(regimeResolution.Statistics);
        Assert.NotNull(regimeResolution.CurrentRegimeValue);
        Assert.Equal(40, regimeResolution.OriginalTargetSamples);
        Assert.Equal(regimeOneSamples, regimeResolution.SelectedSamples);
        Assert.Equal(regimeOneSamples, regimeResolution.History.Count);
        Assert.Equal(1, regimeResolution.CurrentRegimeValue.Value, precision: 6);
        Assert.Equal(
            expectedRegimeOneHistory.Select(reading => reading.DataHora),
            regimeResolution.History.Select(reading => reading.DataHora));
        Assert.Equal(
            expectedRegimeOneHistory.Select(reading => Convert.ToDouble(reading.Valor)),
            regimeResolution.History.Select(reading => reading.Valor));
        Assert.DoesNotContain(
            regimeResolution.History,
            reading => reading.DataHora < expectedRegimeOneHistory[0].DataHora);
        Assert.All(regimeResolution.History, reading =>
            Assert.True(reading.DataHora <= ReadingTime));
        Assert.Equal(expectedRegimeStatistics.Media, regimeResolution.Statistics.Media, precision: 6);
        Assert.NotEqual(fullHistoryStatistics.Media, regimeResolution.Statistics.Media);

        var prepared = await preparation.PrepararAsync(
            CreateRequest(targetHistory),
            globalProfile,
            "continuous",
            ReadingTime);

        Assert.False(prepared.BaselineComportamentalAplicado);
        Assert.NotNull(prepared.BaselineEfetivo);
        Assert.Equal(BaselineDiagnosticKind.OperationalRegime, prepared.BaselineEfetivo!.Kind);
        Assert.Equal(expectedRegimeStatistics.Media, prepared.EntradaDetector.MediaHistorica, precision: 6);
        Assert.Equal(regimeResolution.Statistics.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico, precision: 6);
        Assert.Equal(regimeResolution.Statistics.DeadbandRecomendado, prepared.EntradaDetector.VariacaoMinimaRelevante, precision: 6);
        Assert.Equal(regimeResolution.Statistics.ConfiancaDeadband, prepared.EntradaDetector.ConfiancaVariacaoMinima, precision: 6);
        Assert.Equal(regimeOneSamples, prepared.EntradaDetector.Historico.Count);
        Assert.Equal(
            expectedRegimeOneHistory.Select(reading => reading.DataHora),
            prepared.EntradaDetector.Historico.Select(reading => reading.DataHora));
        Assert.Equal(
            expectedRegimeOneHistory.Select(reading => Convert.ToDouble(reading.Valor)),
            prepared.EntradaDetector.Historico.Select(reading => reading.Valor));
        Assert.DoesNotContain(
            prepared.EntradaDetector.Historico,
            reading => reading.DataHora < expectedRegimeOneHistory[0].DataHora);
        Assert.All(prepared.EntradaDetector.Historico, reading =>
            Assert.True(reading.DataHora <= ReadingTime));
        Assert.NotEqual(fullHistoryStatistics.Media, prepared.EntradaDetector.MediaHistorica);
        Assert.NotEqual(globalProfile.Media, prepared.EntradaDetector.MediaHistorica);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            targetHistory,
            historySource,
            behaviorProvider,
            preparation);

        AssertValidPersistedPrediction(run, await fixture.Context.AnomaliasDetectadas.AsNoTracking().ToListAsync());
        AssertRealStatisticalDetectorsExecuted(run);
        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.OperationalRegime, run.Diagnostic.Baseline!.Kind);
        Assert.NotNull(run.Diagnostic.Baseline.RegimeValue);
        Assert.Equal(
            regimeResolution.CurrentRegimeValue.Value,
            run.Diagnostic.Baseline.RegimeValue.Value,
            precision: 6);
        Assert.Equal(regimeResolution.Statistics.Media, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(regimeOneSamples, run.Diagnostic.Baseline.SampleCount);
        Assert.Null(run.Diagnostic.Baseline.BehaviorCode);
        Assert.Null(run.Diagnostic.Baseline.BehaviorSnapshotVersion);
        Assert.Equal(regimeResolution.Statistics.Media, run.Response.Perfil.MediaHistorica, precision: 6);

        Assert.Equal(memoryBefore, await CaptureBehaviorMemoryAsync(fixture.Context));
        Assert.Equal(regimeBefore, await CaptureOperationalRegimeAsync(fixture.Context));
    }

    [Fact]
    public async Task DisabledOperationalRegime_FallsBackToKnownNormalBehaviorBaseline()
    {
        var behaviorHistory = CreateStableHistory(center: 40);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        var snapshot = await SeedKnownBehaviorAsync(
            fixture.Context,
            behaviorHistory,
            TagComportamentoIa.ClassificacaoNormal,
            "BEH-REGIME-FALLBACK");
        await SeedOperationalRegimeConfigurationAsync(fixture.Context, active: false);

        var memoryBefore = await CaptureBehaviorMemoryAsync(fixture.Context);
        var regimeBefore = await CaptureOperationalRegimeAsync(fixture.Context);
        var historySource = new InMemoryHistorySource(behaviorHistory);
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var regimeProvider = CreateOperationalRegimeProvider(fixture.Context, historySource);
        var preparation = CreatePreparationService(
            fixture.Context,
            historySource,
            behaviorProvider,
            regimeProvider);
        var globalProfile = await fixture.Context.PerfisIa.SingleAsync();

        var regimeResolution = regimeProvider.Resolve(
            ClienteId,
            TagName,
            behaviorHistory,
            ReadingTime);
        Assert.False(regimeResolution.Applied);
        Assert.Contains("desativada", regimeResolution.Reason, StringComparison.OrdinalIgnoreCase);

        var behaviorResolution = await behaviorProvider.ResolveAsync(
            ClienteId,
            TagName,
            behaviorHistory,
            ReadingTime);
        Assert.True(behaviorResolution.Recognized);
        Assert.True(behaviorResolution.CanApplyBaseline);
        Assert.Equal("BEH-REGIME-FALLBACK", behaviorResolution.BehaviorCode);

        var prepared = await preparation.PrepararAsync(
            CreateRequest(behaviorHistory),
            globalProfile,
            "continuous",
            ReadingTime);

        Assert.True(prepared.ComportamentoReconhecido);
        Assert.True(prepared.BaselineComportamentalAplicado);
        Assert.Equal("BEH-REGIME-FALLBACK", prepared.CodigoComportamentoReconhecido);
        Assert.NotNull(prepared.BaselineEfetivo);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, prepared.BaselineEfetivo!.Kind);
        Assert.Equal(snapshot.Media, prepared.EntradaDetector.MediaHistorica, precision: 6);
        Assert.Equal(snapshot.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico, precision: 6);
        Assert.Equal(snapshot.DeadbandRecomendado, prepared.EntradaDetector.VariacaoMinimaRelevante, precision: 6);
        Assert.Equal(snapshot.ConfiancaDeadband, prepared.EntradaDetector.ConfiancaVariacaoMinima, precision: 6);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            behaviorHistory,
            historySource,
            behaviorProvider,
            preparation);

        AssertValidPersistedPrediction(run, await fixture.Context.AnomaliasDetectadas.AsNoTracking().ToListAsync());
        AssertRealStatisticalDetectorsExecuted(run);
        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, run.Diagnostic.Baseline!.Kind);
        Assert.Equal("BEH-REGIME-FALLBACK", run.Diagnostic.Baseline.BehaviorCode);
        Assert.Equal(snapshot.Versao, run.Diagnostic.Baseline.BehaviorSnapshotVersion);
        Assert.Equal(snapshot.Media, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(snapshot.Media, run.Response.Perfil.MediaHistorica, precision: 6);

        Assert.Equal(memoryBefore, await CaptureBehaviorMemoryAsync(fixture.Context));
        Assert.Equal(regimeBefore, await CaptureOperationalRegimeAsync(fixture.Context));
    }

    private static async Task AssertKnownNonNormalBehaviorFallsBackToGlobalBaselineAsync(
        string classification,
        string behaviorCode)
    {
        var behaviorHistory = CreateStableHistory(center: 40);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        var snapshot = await SeedKnownBehaviorAsync(
            fixture.Context,
            behaviorHistory,
            classification,
            behaviorCode);
        var memoryBefore = await CaptureBehaviorMemoryAsync(fixture.Context);
        var globalProfile = await fixture.Context.PerfisIa.SingleAsync();

        var historySource = new InMemoryHistorySource(behaviorHistory);
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var preparation = CreatePreparationService(
            fixture.Context,
            historySource,
            behaviorProvider);

        var resolution = await behaviorProvider.ResolveAsync(
            ClienteId,
            TagName,
            behaviorHistory,
            ReadingTime);

        Assert.True(resolution.Recognized);
        Assert.False(resolution.CanApplyBaseline);
        Assert.Equal(behaviorCode, resolution.BehaviorCode);
        Assert.Equal(classification, resolution.BehaviorClassification);

        var prepared = await preparation.PrepararAsync(
            CreateRequest(behaviorHistory),
            globalProfile,
            "continuous",
            ReadingTime);

        // Reconhecimento e autorização são contratos distintos: Known não
        // autoriza baseline quando a classificação não é Normal.
        Assert.True(prepared.ComportamentoReconhecido);
        Assert.False(prepared.BaselineComportamentalAplicado);
        Assert.Equal(behaviorCode, prepared.CodigoComportamentoReconhecido);
        Assert.Equal(classification, prepared.ClassificacaoComportamentoReconhecido);
        Assert.NotNull(prepared.BaselineEfetivo);
        Assert.Equal(BaselineDiagnosticKind.Global, prepared.BaselineEfetivo!.Kind);
        Assert.True(prepared.BaselineEfetivo.Applied);
        Assert.Null(prepared.BaselineEfetivo.BehaviorCode);
        Assert.Null(prepared.BaselineEfetivo.BehaviorSnapshotVersion);

        // O DetectorInput recebe o fallback global efetivamente selecionado,
        // e não as estatísticas do snapshot reconhecido, porém não autorizado.
        Assert.Equal(globalProfile.Media, prepared.EntradaDetector.MediaHistorica, precision: 6);
        Assert.Equal(globalProfile.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico, precision: 6);
        Assert.Equal(globalProfile.VariacaoMinimaRelevante, prepared.EntradaDetector.VariacaoMinimaRelevante, precision: 6);
        Assert.Equal(globalProfile.ConfiancaDeadband, prepared.EntradaDetector.ConfiancaVariacaoMinima, precision: 6);
        Assert.NotEqual(snapshot.Media, prepared.EntradaDetector.MediaHistorica);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            behaviorHistory,
            historySource,
            behaviorProvider,
            preparation);

        Assert.NotNull(run.Response);
        Assert.Equal(ClienteId, run.Response.ClienteId);
        Assert.Equal(TagName, run.Response.TagName);
        Assert.True(double.IsFinite(run.Response.Score));
        Assert.NotNull(run.Response.Interpretacao);
        Assert.Equal(globalProfile.Media, run.Response.Perfil.MediaHistorica, precision: 6);

        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.Global, run.Diagnostic.Baseline!.Kind);
        Assert.True(run.Diagnostic.Baseline.Applied);
        Assert.Equal(globalProfile.Media, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(globalProfile.DesvioPadrao, run.Diagnostic.Baseline.StandardDeviation, precision: 6);
        Assert.Equal(globalProfile.VariacaoMinimaRelevante, run.Diagnostic.Baseline.Deadband, precision: 6);
        Assert.Equal(globalProfile.ConfiancaDeadband, run.Diagnostic.Baseline.DeadbandConfidence, precision: 6);
        Assert.Null(run.Diagnostic.Baseline.BehaviorCode);
        Assert.Null(run.Diagnostic.Baseline.BehaviorSnapshotVersion);
        Assert.NotEqual(snapshot.Media, run.Diagnostic.Baseline.Mean);

        AssertRealStatisticalDetectorsExecuted(run);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking()
            .ToListAsync();

        var operationalResult = Assert.Single(persisted);
        Assert.Equal(ClienteId, operationalResult.ClienteId);
        Assert.Equal(TagName, operationalResult.TagName);
        Assert.Equal(run.Response.EhAnomalia, operationalResult.EhAnomalia);
        Assert.Equal((double)run.Response.Score, operationalResult.Score, precision: 5);

        Assert.Equal(memoryBefore, await CaptureBehaviorMemoryAsync(fixture.Context));
    }

    [Fact]
    public async Task MultivariateShadow_RealPredictionsCloseVectorWithoutChangingLegacyDecision()
    {
        // O coordenador só finaliza o vetor do minuto T quando a primeira
        // tag reaparece em T+1. Executar três previsões reais, sem invocar
        // ProcessarLeituraAsync diretamente, comprova essa ligação.
        var pressureHistory = CreateStableHistory(center: 100, count: 60);
        var flowHistory = CreateStableHistory(center: 20, count: 60);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        await SeedMultivariateCompanionTagAsync(fixture.Context);

        var historySource = new InMemoryHistorySource(
            new HistorySourceEntry(ClienteId, TagName, pressureHistory),
            new HistorySourceEntry(ClienteId, CompanionTagName, flowHistory));
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var preparation = CreatePreparationService(
            fixture.Context, historySource, behaviorProvider);

        using var modelFixture = new E2EMultivariateModelFixture();
        var persistence = await modelFixture.CreatePersistenceWithModelAsync();
        var recorder = new StructuredMultivariateLogger();
        var state = new MultivariateRuntimeStateStore();
        var coordinator = new MultivariateShadowRuntimeCoordinator(
            new MultivariateGroupBufferService(
                new GrupoAnaliseService(new StaticMultivariateGroupProvider()), state),
            new MultivariateDetector(persistence),
            recorder);

        var atT = ReadingTime;
        var atTPlusOne = ReadingTime.AddMinutes(1);
        var pressureAtT = new AnomaliaRequest
        {
            ClienteId = ClienteId, TagName = TagName,
            DataHora = atT, Valor = 100
        };
        var flowAtT = new AnomaliaRequest
        {
            ClienteId = ClienteId, TagName = CompanionTagName,
            DataHora = atT, Valor = 20
        };
        var pressureAtTPlusOne = new AnomaliaRequest
        {
            ClienteId = ClienteId, TagName = TagName,
            DataHora = atTPlusOne, Valor = 100
        };

        var first = await ExecutePredictionAsync(
            fixture.Context, pressureHistory, historySource,
            behaviorProvider, preparation,
            multivariateCoordinator: coordinator,
            requestOverride: pressureAtT);
        var second = await ExecutePredictionAsync(
            fixture.Context, flowHistory, historySource,
            behaviorProvider, preparation,
            multivariateCoordinator: coordinator,
            requestOverride: flowAtT);

        Assert.Empty(recorder.Results);
        Assert.Equal(1, state.ObterQuantidadeBuffers());

        var completed = await ExecutePredictionAsync(
            fixture.Context, pressureHistory, historySource,
            behaviorProvider, preparation,
            multivariateCoordinator: coordinator,
            requestOverride: pressureAtTPlusOne);

        // O logger prova a execucao no coordenador real; o diagnostico
        // abaixo prova a propagacao de resultados, sem alteracao operacional.
        var multivariate = Assert.Single(recorder.Results);
        Assert.Equal(ClienteId, multivariate["ClienteId"]);
        Assert.Equal(MultivariateGroupName, multivariate["GroupName"]);
        Assert.Equal(
            MultivariateShadowRuntimeCoordinator.CriarMinuteIndex(atT),
            Convert.ToInt64(multivariate["MinuteIndex"]));
        Assert.Equal(true, multivariate["Applicable"]);
        Assert.Equal(2, Convert.ToInt32(multivariate["TagCount"]));
        Assert.True(double.IsFinite(Convert.ToDouble(multivariate["Score"])));
        Assert.True(double.IsFinite(Convert.ToDouble(multivariate["Threshold"])));
        Assert.True(double.IsFinite(Convert.ToDouble(multivariate["MahalanobisSquared"])));
        // O vetor finalizado aparece no diagnostico da leitura que o fechou.
        var captured = Assert.Single(completed.Diagnostic.Multivariate);
        Assert.DoesNotContain("Multivariate", completed.Diagnostic.Completeness.MissingSections);
        Assert.Equal(MultivariateGroupName, captured.GroupName);
        Assert.True(captured.Applicable);
        Assert.Equal(MultivariateShadowRuntimeCoordinator.CriarMinuteIndex(atT), captured.MinuteIndex);
        Assert.Equal(1, captured.AgeMinutes);
        Assert.Equal(new[] { TagName, CompanionTagName }.OrderBy(x => x),
            captured.Tags.OrderBy(x => x));
        Assert.NotNull(captured.NormalizedScore);
        Assert.NotNull(captured.Threshold);
        Assert.NotNull(captured.MahalanobisSquared);
        Assert.True(double.IsFinite(captured.NormalizedScore.Value));
        Assert.True(double.IsFinite(captured.Threshold.Value));
        Assert.True(double.IsFinite(captured.MahalanobisSquared.Value));
        Assert.Null(captured.Reason);
        Assert.Empty(first.Diagnostic.Multivariate);
        Assert.Empty(second.Diagnostic.Multivariate);
        Assert.DoesNotContain("Multivariate", first.Diagnostic.Completeness.MissingSections);
        Assert.DoesNotContain("Multivariate", second.Diagnostic.Completeness.MissingSections);

        // A observação shadow não pode modificar alerta nem score univariado.
        // Roda o mesmo request, na mesma fixture, com o coordenador desligado.
        var control = await ExecutePredictionAsync(
            fixture.Context, pressureHistory, historySource,
            behaviorProvider, preparation,
            requestOverride: pressureAtTPlusOne);
        Assert.Equal(control.Response.EhAnomalia, completed.Response.EhAnomalia);
        Assert.Equal(control.Response.Score, completed.Response.Score, precision: 6);
        Assert.Equal(control.Diagnostic.RuntimeDecision.Source,
            completed.Diagnostic.RuntimeDecision.Source);
        Assert.Equal(control.Diagnostic.Ensemble!.RegisteredDetectorCount,
            completed.Diagnostic.Ensemble!.RegisteredDetectorCount);
        AssertRealStatisticalDetectorsExecuted(first);
        AssertRealStatisticalDetectorsExecuted(second);
        AssertRealStatisticalDetectorsExecuted(completed);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking().ToListAsync();
        Assert.Equal(4, persisted.Count);
        Assert.Equal(3, persisted.Count(x => x.TagName == TagName));
        Assert.Single(persisted, x => x.TagName == CompanionTagName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultivariateOperationalPromotion_RealPredictionRespectsReadiness(
        bool explicitlyApproved)
    {
        // A anomalia é calculada pelo Mahalanobis real: pressão permanece
        // normal (100) e vazão desvia do modelo de 20 para 26. Não injetamos
        // resultados multivariados nem forçamos a decisão do Ensemble.
        var pressureHistory = CreateStableHistory(center: 100, count: 60);
        var flowHistory = CreateStableHistory(center: 20, count: 60);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        await SeedMultivariateCompanionTagAsync(fixture.Context);

        var historySource = new InMemoryHistorySource(
            new HistorySourceEntry(ClienteId, TagName, pressureHistory),
            new HistorySourceEntry(ClienteId, CompanionTagName, flowHistory));
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var preparation = CreatePreparationService(
            fixture.Context, historySource, behaviorProvider);

        using var modelFixture = new E2EMultivariateModelFixture();
        var persistence = await modelFixture.CreatePersistenceWithModelAsync();
        var recorder = new StructuredMultivariateLogger();
        var coordinator = new MultivariateShadowRuntimeCoordinator(
            new MultivariateGroupBufferService(
                new GrupoAnaliseService(new StaticMultivariateGroupProvider()),
                new MultivariateRuntimeStateStore()),
            new MultivariateDetector(persistence),
            recorder);

        var readinessOptions = new MultivariateOperationalReadinessOptions
        {
            RequireExplicitApproval = true,
            MinimumTrainingSamples = 100,
            // A fixture trabalha com uma data histórica fixa e o serviço
            // operacional usa DateTime.UtcNow; janela larga só neste teste.
            // Isto NÃO altera configurações ou critérios de produção.
            MaxModelAgeDays = 3650,
            ApprovedGroups = explicitlyApproved
                ? [$"{ClienteId}::{MultivariateGroupName}"]
                : []
        };
        var readinessService = new MultivariateOperationalReadinessService(
            new StaticMultivariateGroupProvider(),
            persistence,
            new MultivariateOperationalReadinessEvaluator());

        // Confirma que o bloqueio ou a autorização provém da regra real
        // de readiness, antes de exercitar a previsão completa.
        var readiness = await readinessService.AvaliarAsync(
            ClienteId, MultivariateGroupName, readinessOptions,
            nowUtc: ReadingTime);
        Assert.Equal(explicitlyApproved, readiness.ReadyForOperationalAuthority);
        if (explicitlyApproved)
        {
            Assert.Empty(readiness.Blockers);
        }
        else
        {
            Assert.Contains(
                "GrupoNaoAprovadoParaAutoridadeOperacional",
                readiness.Blockers);
        }

        var counterfactualObserver = new MultivariateDecisionCounterfactualObserver(
            new MultivariateDecisionCounterfactualEvaluator(),
            Options.Create(new MultivariateDecisionFusionOptions
            {
                Enabled = true, CanaryOnly = true,
                AllowedClientIds = [ClienteId], MaxEvidenceAgeMinutes = 2
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                MultivariateDecisionCounterfactualObserver>.Instance);
        var multivariateDecision = new PredictionMultivariateDecisionService(
            counterfactualObserver,
            new MultivariateOperationalFusionResolver(),
            Options.Create(new MultivariateOperationalPromotionOptions
            {
                Enabled = true, CanaryOnly = true,
                AllowedClientIds = [ClienteId], MaxEvidenceAgeMinutes = 2
            }),
            readinessService,
            Options.Create(readinessOptions));

        var atT = ReadingTime;
        var pressureAtT = new AnomaliaRequest
        {
            ClienteId = ClienteId, TagName = TagName,
            DataHora = atT, Valor = 100
        };
        var flowAtT = new AnomaliaRequest
        {
            ClienteId = ClienteId, TagName = CompanionTagName,
            DataHora = atT, Valor = 26
        };
        var pressureAtTPlusOne = new AnomaliaRequest
        {
            ClienteId = ClienteId, TagName = TagName,
            DataHora = atT.AddMinutes(1), Valor = 100
        };

        // Todos os passos usam PredictionService.Detectar, sem chamar
        // diretamente o detector ou fabricar resultados de fusão.
        await ExecutePredictionAsync(
            fixture.Context, pressureHistory, historySource,
            behaviorProvider, preparation,
            multivariateCoordinator: coordinator,
            multivariateDecisionService: multivariateDecision,
            multivariateInterpretationEnabled: true,
            requestOverride: pressureAtT);
        await ExecutePredictionAsync(
            fixture.Context, flowHistory, historySource,
            behaviorProvider, preparation,
            multivariateCoordinator: coordinator,
            multivariateDecisionService: multivariateDecision,
            multivariateInterpretationEnabled: true,
            requestOverride: flowAtT);
        var completed = await ExecutePredictionAsync(
            fixture.Context, pressureHistory, historySource,
            behaviorProvider, preparation,
            multivariateCoordinator: coordinator,
            multivariateDecisionService: multivariateDecision,
            multivariateInterpretationEnabled: true,
            requestOverride: pressureAtTPlusOne);

        var detected = Assert.Single(recorder.Results);
        Assert.Equal(true, detected["Applicable"]);
        Assert.True(Convert.ToDouble(detected["Score"]) >=
                    Convert.ToDouble(detected["Threshold"]));

        var multivariateDiagnostic = Assert.Single(completed.Diagnostic.Multivariate);
        Assert.True(multivariateDiagnostic.Applicable);
        Assert.Equal(MultivariateGroupName, multivariateDiagnostic.GroupName);
        Assert.NotNull(multivariateDiagnostic.NormalizedScore);
        Assert.NotNull(multivariateDiagnostic.Threshold);
        Assert.True(multivariateDiagnostic.NormalizedScore.Value >=
                    multivariateDiagnostic.Threshold.Value);
        Assert.Equal(1, multivariateDiagnostic.AgeMinutes);

        // Sem multivariada, a mesma leitura não produz alerta Legacy.
        var control = await ExecutePredictionAsync(
            fixture.Context, pressureHistory, historySource,
            behaviorProvider, preparation,
            requestOverride: pressureAtTPlusOne);
        Assert.False(control.Response.EhAnomalia);
        Assert.Equal(control.Response.Score, completed.Response.Score, precision: 6);

        // A autorização de rollout não basta: readiness é o último gate.
        Assert.Equal(explicitlyApproved, completed.Response.EhAnomalia);
        Assert.Equal(
            explicitlyApproved ? "MultivariateNewAnomaly" :
                control.Diagnostic.RuntimeDecision.Source,
            completed.Diagnostic.RuntimeDecision.Source);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking().ToListAsync();
        Assert.Equal(4, persisted.Count);
        Assert.Equal(
            explicitlyApproved ? 1 : 0,
            persisted.Count(x => x.TagName == TagName && x.EhAnomalia));
        Assert.Contains(persisted, item =>
            item.TagName == TagName &&
            item.EhAnomalia == completed.Response.EhAnomalia &&
            Math.Abs(item.Score - completed.Response.Score) < 0.00001);
        AssertRealStatisticalDetectorsExecuted(completed);

        // Lacuna intencional: o bloqueio detalhado de readiness é hoje
        // obtido do serviço/log, não de uma seção própria do diagnóstico.
        // Não confundir shadow anômalo com alerta operacional emitido.
    }

    private static async Task<PredictionRun> ExecutePredictionAsync(
        AppDbContext context,
        IReadOnlyList<LeituraBruta> history,
        InMemoryHistorySource? historySource = null,
        BehaviorRuntimeBaselineProvider? behaviorProvider = null,
        PredictionDetectionPreparationService? preparation = null,
        IAnomalyDetector? additionalDetector = null,
        RuntimeAnomalyDecisionOptions? runtimeDecisionOptions = null,
        MultivariateShadowRuntimeCoordinator? multivariateCoordinator = null,
        AnomaliaRequest? requestOverride = null,
        PredictionMultivariateDecisionService? multivariateDecisionService = null,
        bool multivariateInterpretationEnabled = false)
    {
        historySource ??= new InMemoryHistorySource(history);
        behaviorProvider ??= CreateBehaviorProvider(context);
        preparation ??= CreatePreparationService(context, historySource, behaviorProvider);

        var options = new AnomalyDetectionOptions();
        var detectors = CreateRealStatisticalDetectors(options);
        if (additionalDetector is not null)
        {
            detectors = [.. detectors, additionalDetector];
        }

        var ensemble = new EnsembleDetectionService(
            detectors,
            Options.Create(options),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());

        var interpretation = new PredictionInterpretationService(
            new AnomalyInterpretationContextBuilder(new TagContextoIaService(context)),
            new RuleBasedAnomalyInterpretationService());

        var runtimeContext = new RuntimeFunctionalDecisionContextProvider(
            new RuntimeFunctionalDecisionStateStore(),
            Options.Create(options));

        var runtimeDecision = new PredictionRuntimeDecisionService(
            new RuntimeFunctionalDecisionCandidateFactory(),
            new RuntimeAnomalyDecisionResolver(),
            runtimeDecisionOptions ?? new RuntimeAnomalyDecisionOptions { Enabled = false },
            new RuntimeDecisionMetrics());

        var decision = new PredictionDecisionOrchestrationService(
            ensemble,
            interpretation,
            runtimeContext,
            runtimeDecision,
            multivariateDecisionService);

        // O contrato atual depende da classe concreta BridgeValorAtualService,
        // sem interface nem método virtual para um fake. A fronteira é segura
        // neste cenário porque não há dependências cadastradas: o serviço real
        // retorna antes de chamar BuscarValoresAtuais. Se esse fluxo mudar, a
        // chamada tentará localhost e o teste falhará; esta fixture não deve
        // ser entendida como cobertura do Bridge.
        Assert.Empty(context.TagDependenciasIa.AsNoTracking().ToList());
        var postDecision = new PredictionPostDecisionAnalysisService(
            new AnaliseRelacionadasService(context, new BridgeValorAtualService()),
            new PredictionRiskAnalysisService(
                new RiskScoreService(),
                new ExplicacaoRiscoService()),
            new PredictionTrendAnalysisService(
                new TendenciaRiscoService(context),
                new TendenciaValorService(historySource)),
            new FeedbackAnomaliaAnaliseService(context),
            multivariateInterpretationEnabled
                ? new MultivariateOperationalInterpretationFactory()
                : null);

        var finalization = new PredictionFinalizationService(
            new AnomaliaHistoricoService(context),
            new PredictionResponseBuilder(
                new PredictionDependencyMapper(
                    new InterpretacaoDependenciasService())));

        var buffer = new BoundedDiagnosticBuffer(capacity: 10, enabled: true);
        var service = new PredictionService(
            new PredictionContextLoader(context, new ProcessoAnaliseService(context)),
            postDecision,
            finalization,
            decision,
            preparation,
            multivariateShadowRuntimeCoordinator: multivariateCoordinator,
            diagnosticCollector: new InMemoryDiagnosticCollector(buffer),
            diagnosticVersion: new DiagnosticVersionMetadata(
                "intelligence-lab-2.2G.2",
                "test-build",
                "test-commit",
                "production-defaults"),
            diagnosticHealth: buffer.Health);

        var response = await service.Detectar(requestOverride ?? CreateRequest(history));
        var diagnostic = Assert.Single(buffer.Snapshot());

        return new PredictionRun(response, diagnostic);
    }

    private static PredictionDetectionPreparationService CreatePreparationService(
        AppDbContext context,
        IHistoricoService historySource,
        BehaviorRuntimeBaselineProvider behaviorProvider,
        OperationalRegimeBaselineProvider? regimeProvider = null)
    {
        regimeProvider ??= CreateOperationalRegimeProvider(
            context,
            historySource);

        return new PredictionDetectionPreparationService(
            historySource,
            regimeProvider,
            behaviorProvider);
    }

    private static OperationalRegimeBaselineProvider CreateOperationalRegimeProvider(
        AppDbContext context,
        IHistoricoService historySource)
    {
        return new OperationalRegimeBaselineProvider(
            context,
            historySource,
            new OperationalRegimeBaselineService(
                new OperationalRegimeHistoryResolver(),
                new TagProfileStatisticsCalculator(
                    new HistoricoLimpezaService())));
    }

    private static BehaviorRuntimeBaselineProvider CreateBehaviorProvider(AppDbContext context)
    {
        var statistics = new TagProfileStatisticsCalculator(new HistoricoLimpezaService());
        var characterizer = new BehaviorCandidateEpisodeCharacterizer(statistics);
        var planBuilder = new BehaviorMemoryPersistencePlanBuilder();

        return new BehaviorRuntimeBaselineProvider(
            context,
            new BehaviorHistorySegmentationService(),
            new BehaviorChangePointDetector(),
            new BehaviorCandidateEpisodeBuilder(),
            characterizer,
            new BehaviorMemoryReconciliationService(context, planBuilder));
    }

    private static IAnomalyDetector[] CreateRealStatisticalDetectors(
        AnomalyDetectionOptions options) =>
    [
        new StatisticalDetector(options),
        new MadDetector(options),
        new EwmaDetector(options),
        new RateOfChangeDetector(options),
        new PersistenceDetector(options),
        new LevelShiftDetector(options)
    ];

    private static void AssertRealStatisticalDetectorsExecuted(PredictionRun run)
    {
        Assert.Equal(6, run.Response.Detectores.Count);
        Assert.Equal(6, run.Diagnostic.Detectors.Count);
        Assert.Equal(6, run.Diagnostic.Ensemble!.RegisteredDetectorCount);

        foreach (var name in StatisticalDetectorNames)
        {
            Assert.Contains(run.Response.Detectores, detector => detector.Nome == name && detector.Aplicavel);
            Assert.Contains(run.Diagnostic.Detectors, detector =>
                detector.Name == name && detector.Status == DetectorDiagnosticStatus.Executed);
        }
    }

    private static async Task SeedPredictionRequirementsAsync(AppDbContext context)
    {
        context.TagsIaConfig.Add(new TagIaConfig
        {
            ClienteId = ClienteId,
            TagName = TagName,
            IaAtiva = true
        });

        // O perfil global permanece propositalmente distante do BEH do
        // segundo cenário para tornar observável a troca real de baseline.
        context.PerfisIa.Add(new TagPerfilIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            Media = 100,
            DesvioPadrao = 2,
            VariacaoMinimaRelevante = 5,
            DeadbandSugerido = 5,
            ConfiancaDeadband = 94,
            VariacaoMinimaCalculadaAutomaticamente = true,
            DataTreinamento = ReadingTime.AddDays(-1)
        });

        context.TagsContextoIa.Add(new TagContextoIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            TipoRepresentacao = "continuous"
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedMultivariateCompanionTagAsync(AppDbContext context)
    {
        context.TagsIaConfig.Add(new TagIaConfig
        {
            ClienteId = ClienteId, TagName = CompanionTagName, IaAtiva = true
        });
        context.PerfisIa.Add(new TagPerfilIa
        {
            ClienteId = ClienteId, TagName = CompanionTagName,
            Media = 20, DesvioPadrao = 2,
            VariacaoMinimaRelevante = 5, DeadbandSugerido = 5,
            ConfiancaDeadband = 94,
            VariacaoMinimaCalculadaAutomaticamente = true,
            DataTreinamento = ReadingTime.AddDays(-1)
        });
        context.TagsContextoIa.Add(new TagContextoIa
        {
            ClienteId = ClienteId, TagName = CompanionTagName,
            TipoRepresentacao = "continuous"
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedOperationalRegimeConfigurationAsync(
        AppDbContext context,
        bool active)
    {
        context.TagsRegimesOperacionaisIa.Add(new TagRegimeOperacionalIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            TagRegime = RegimeTagName,
            Ativo = active,
            LagMinutos = 1,
            ToleranciaAlinhamentoMinutos = 0,
            ToleranciaEstado = 0,
            MinimoAmostrasPorRegime = 30,
            DataCriacao = ReadingTime.AddDays(-1)
        });

        await context.SaveChangesAsync();
    }

    private static async Task<TagComportamentoSnapshotIa> SeedKnownBehaviorAsync(
        AppDbContext context,
        IReadOnlyList<LeituraBruta> history,
        string classification,
        string behaviorCode)
    {
        var behavior = new TagComportamentoIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            Codigo = behaviorCode,
            Status = TagComportamentoIa.StatusKnown,
            Origem = TagComportamentoIa.OrigemAutomatic,
            Classificacao = classification,
            PrimeiraObservacao = history[0].DataHora,
            UltimaObservacao = history[^1].DataHora,
            QuantidadeOcorrencias = 1,
            TotalAmostras = history.Count,
            Confianca = 90,
            DataCriacao = DateTime.UtcNow
        };

        context.TagsComportamentosIa.Add(behavior);
        await context.SaveChangesAsync();

        var characterizer = new BehaviorCandidateEpisodeCharacterizer(
            new TagProfileStatisticsCalculator(new HistoricoLimpezaService()));
        var characteristics = characterizer.Characterize(new BehaviorCandidateEpisode
        {
            SourceSegmentIndex = 0,
            EpisodeIndex = 0,
            StartReadingIndex = 0,
            EndReadingIndex = history.Count - 1,
            Inicio = history[0].DataHora,
            Fim = history[^1].DataHora,
            Historico = history
        });

        Assert.True(characteristics.IsCharacterizable, characteristics.Reason);

        var snapshot = new BehaviorMemoryPersistencePlanBuilder().BuildSnapshotPreview(
            [characteristics],
            version: 1,
            createdAtUtc: DateTime.UtcNow);
        snapshot.TagComportamentoIaId = behavior.Id;

        context.TagsComportamentosSnapshotsIa.Add(snapshot);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return snapshot;
    }

    private static async Task<BehaviorMemoryState> CaptureBehaviorMemoryAsync(
        AppDbContext context)
    {
        var behavior = await context.TagsComportamentosIa
            .AsNoTracking()
            .SingleAsync();
        var snapshot = await context.TagsComportamentosSnapshotsIa
            .AsNoTracking()
            .SingleAsync();

        return new BehaviorMemoryState(
            behavior.Id,
            behavior.ClienteId,
            behavior.TagName,
            behavior.Codigo,
            behavior.Status,
            behavior.Origem,
            behavior.Classificacao,
            behavior.PrimeiraObservacao,
            behavior.UltimaObservacao,
            behavior.QuantidadeOcorrencias,
            behavior.TotalAmostras,
            behavior.Confianca,
            behavior.DataCriacao,
            behavior.DataAtualizacao,
            snapshot.Id,
            snapshot.TagComportamentoIaId,
            snapshot.Versao,
            snapshot.Media,
            snapshot.DesvioPadrao,
            snapshot.Mediana,
            snapshot.PrimeiroQuartil,
            snapshot.TerceiroQuartil,
            snapshot.IntervaloInterquartil,
            snapshot.RobustScale,
            snapshot.TypicalAbsoluteStep,
            snapshot.Minimo,
            snapshot.Maximo,
            snapshot.Amplitude,
            snapshot.PercentualZeros,
            snapshot.VariacaoMedia,
            snapshot.QuantidadePicos,
            snapshot.TotalRegistrosHistorico,
            snapshot.TotalRegistrosUsados,
            snapshot.TotalOutliersRemovidos,
            snapshot.DeadbandRecomendado,
            snapshot.ConfiancaDeadband,
            snapshot.JustificativaDeadband,
            snapshot.PeriodoInicio,
            snapshot.PeriodoFim,
            snapshot.DataCriacao);
    }

    private static async Task<OperationalRegimeState> CaptureOperationalRegimeAsync(
        AppDbContext context)
    {
        var regime = await context.TagsRegimesOperacionaisIa
            .AsNoTracking()
            .SingleAsync();

        return new OperationalRegimeState(
            regime.Id,
            regime.ClienteId,
            regime.TagName,
            regime.TagRegime,
            regime.Ativo,
            regime.LagMinutos,
            regime.ToleranciaAlinhamentoMinutos,
            regime.ToleranciaEstado,
            regime.MinimoAmostrasPorRegime,
            regime.DataCriacao,
            regime.DataAtualizacao);
    }

    private static void AssertValidPersistedPrediction(
        PredictionRun run,
        IReadOnlyList<AnomaliaDetectada> persisted)
    {
        Assert.NotNull(run.Response);
        Assert.Equal(ClienteId, run.Response.ClienteId);
        Assert.Equal(TagName, run.Response.TagName);
        Assert.True(double.IsFinite(run.Response.Score));
        Assert.NotNull(run.Response.Interpretacao);

        var operationalResult = Assert.Single(persisted);
        Assert.Equal(ClienteId, operationalResult.ClienteId);
        Assert.Equal(TagName, operationalResult.TagName);
        Assert.Equal(run.Response.EhAnomalia, operationalResult.EhAnomalia);
        Assert.Equal((double)run.Response.Score, operationalResult.Score, precision: 5);
    }

    private static AnomaliaRequest CreateRequest(IReadOnlyList<LeituraBruta> history) => new()
    {
        ClienteId = ClienteId,
        TagName = TagName,
        Valor = history[^1].Valor,
        DataHora = ReadingTime
    };

    private static LeituraBruta[] CreateStableHistory(double center, int count = 40)
    {
        double[] offsets = [0.00, 0.12, -0.08, 0.18, -0.14, 0.05, 0.16, -0.11, 0.09, -0.04];

        return Enumerable.Range(0, count)
            .Select(index => new LeituraBruta
            {
                DataHora = ReadingTime.AddMinutes(index - count),
                Valor = Convert.ToSingle(center + offsets[index % offsets.Length])
            })
            .ToArray();
    }

    private static LeituraBruta[] CreateTwoRegimeTargetHistory()
    {
        float[] regimeZeroValues = [62, 63, 64, 65, 66, 67, 68, 69];
        float[] regimeOneOffsets = [-4, -3, -2, -1, 1, 2, 3, 4];

        return Enumerable.Range(0, 40)
            .Select(index => new LeituraBruta
            {
                DataHora = ReadingTime.AddMinutes(index - 40),
                Valor = index < regimeZeroValues.Length
                    ? regimeZeroValues[index]
                    : 70 + regimeOneOffsets[(index - regimeZeroValues.Length) % regimeOneOffsets.Length]
            })
            .ToArray();
    }

    private static LeituraBruta[] CreateRegimeHistory(
        IReadOnlyList<LeituraBruta> targetHistory,
        int regimeZeroSamples)
    {
        var start = targetHistory[0].DataHora.AddMinutes(-1);
        var end = ReadingTime.AddMinutes(-1);

        return Enumerable.Range(
                0,
                (int)(end - start).TotalMinutes + 1)
            .Select(index => new LeituraBruta
            {
                DataHora = start.AddMinutes(index),
                Valor = index < regimeZeroSamples ? 0 : 1
            })
            .ToArray();
    }

    private sealed record PredictionRun(
        AnomaliaResponse Response,
        IntelligenceLabReadingDiagnostic Diagnostic);

    private sealed record BehaviorMemoryState(
        int BehaviorId,
        string ClienteId,
        string TagName,
        string Codigo,
        string Status,
        string Origem,
        string Classificacao,
        DateTime PrimeiraObservacao,
        DateTime UltimaObservacao,
        int QuantidadeOcorrencias,
        long TotalAmostras,
        double Confianca,
        DateTime DataCriacao,
        DateTime? DataAtualizacao,
        int SnapshotId,
        int SnapshotBehaviorId,
        int SnapshotVersao,
        double SnapshotMedia,
        double SnapshotDesvioPadrao,
        double? SnapshotMediana,
        double? SnapshotPrimeiroQuartil,
        double? SnapshotTerceiroQuartil,
        double? SnapshotIntervaloInterquartil,
        double? SnapshotRobustScale,
        double? SnapshotTypicalAbsoluteStep,
        double SnapshotMinimo,
        double SnapshotMaximo,
        double SnapshotAmplitude,
        double SnapshotPercentualZeros,
        double SnapshotVariacaoMedia,
        int SnapshotQuantidadePicos,
        int SnapshotTotalRegistrosHistorico,
        int SnapshotTotalRegistrosUsados,
        int SnapshotTotalOutliersRemovidos,
        double SnapshotDeadbandRecomendado,
        double SnapshotConfiancaDeadband,
        string? SnapshotJustificativaDeadband,
        DateTime SnapshotPeriodoInicio,
        DateTime SnapshotPeriodoFim,
        DateTime SnapshotDataCriacao);

    private sealed record OperationalRegimeState(
        int Id,
        string ClienteId,
        string TagName,
        string TagRegime,
        bool Ativo,
        int LagMinutos,
        int ToleranciaAlinhamentoMinutos,
        double ToleranciaEstado,
        int MinimoAmostrasPorRegime,
        DateTime DataCriacao,
        DateTime? DataAtualizacao);

    private sealed record HistorySourceEntry(
        string ClienteId,
        string TagName,
        IReadOnlyList<LeituraBruta> Readings);

    private sealed class InMemoryHistorySource : IHistoricoService
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<LeituraBruta>> _histories;

        public InMemoryHistorySource(IReadOnlyList<LeituraBruta> readings)
            : this(new HistorySourceEntry(ClienteId, TagName, readings))
        {
        }

        public InMemoryHistorySource(params HistorySourceEntry[] histories)
        {
            _histories = histories.ToDictionary(
                entry => BuildKey(entry.ClienteId, entry.TagName),
                entry => entry.Readings,
                StringComparer.Ordinal);
        }

        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag) =>
            FindHistory(clienteId, tagName).Select(Clone).ToList();

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim) =>
            FindHistory(clienteId, tagName)
                .Where(item => item.DataHora >= inicio && item.DataHora <= fim)
                .Select(Clone)
                .ToList();

        private IReadOnlyList<LeituraBruta> FindHistory(
            string clienteId,
            string tagName) =>
            _histories.TryGetValue(BuildKey(clienteId, tagName), out var readings)
                ? readings
                : Array.Empty<LeituraBruta>();

        private static string BuildKey(string clienteId, string tagName) =>
            $"{clienteId}\u001f{tagName}";

        private static LeituraBruta Clone(LeituraBruta reading) => new()
        {
            DataHora = reading.DataHora,
            Valor = reading.Valor
        };
    }

    private sealed class E2EIsolationForestModelFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "IAApi-IntelligenceLab-IF-E2E",
            Guid.NewGuid().ToString("N"));

        public IsolationForestOptions Options { get; } = new()
        {
            Weight = 0.25,
            TreeCount = 16,
            SampleSize = 32,
            MinimumHistoryPoints = 50,
            FeatureWindowSize = 10,
            AnomalyThreshold = 0.62,
            RandomSeed = 42
        };

        public async Task<IsolationForestDetector> TrainAndCreateDetectorAsync(
            DetectorInput input)
        {
            var extractor = new IsolationForestFeatureExtractor();
            var vectors = extractor.ExtrairTreinamento(input, Options.FeatureWindowSize)
                .Select(feature => (IReadOnlyList<double>)feature.ParaVetor().ToArray())
                .ToList();
            Assert.True(vectors.Count >= 2);

            var model = new IsolationForestModel(
                quantidadeArvores: Options.TreeCount,
                tamanhoAmostra: Options.SampleSize,
                sementeAleatoria: Options.RandomSeed);
            model.Treinar(vectors);

            var snapshot = model.ExportarSnapshot(
                input.ClienteId,
                input.TagName,
                Options.FeatureWindowSize,
                Options.AnomalyThreshold,
                ReadingTime.AddDays(-1));
            snapshot.VersaoModelo = 1;
            snapshot.QuantidadePontosTreinamento = input.Historico.Count;

            var persistence = new IsolationForestPersistenceService(
                new E2EIsolationForestEnvironment(_root));
            await persistence.SalvarAsync(snapshot);
            IsolationForestDetector.RemoverDoCache(input.ClienteId, input.TagName);

            return new IsolationForestDetector(
                Microsoft.Extensions.Options.Options.Create(Options),
                extractor,
                persistence);
        }

        public void Dispose()
        {
            IsolationForestDetector.RemoverDoCache(ClienteId, TagName);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class E2EIsolationForestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IAApi.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StaticMultivariateGroupProvider : IGrupoAnaliseProvider
    {
        public Task<List<GrupoAnaliseDto>> CarregarGruposAsync(string clienteId) =>
            Task.FromResult(new List<GrupoAnaliseDto>
            {
                new()
                {
                    Id = 1,
                    Nome = MultivariateGroupName,
                    MinimumAvailableTags = 2,
                    MaxLagMinutes = 2,
                    Tags = [TagName, CompanionTagName]
                }
            });
    }

    private sealed class E2EMultivariateModelFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "IAApi-IntelligenceLab-MV-E2E",
            Guid.NewGuid().ToString("N"));

        public async Task<MultivariateModelPersistenceService>
            CreatePersistenceWithModelAsync()
        {
            var persistence = new MultivariateModelPersistenceService(
                new E2EIsolationForestEnvironment(_root));
            var path = persistence.ObterCaminhoModelo(
                ClienteId, MultivariateGroupName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var model = new MultivariateGroupModel
            {
                Version = 1,
                ClienteId = ClienteId,
                GroupName = MultivariateGroupName,
                TrainedAtUtc = ReadingTime.AddDays(-1),
                Algorithm = "Mahalanobis",
                SampleCount = 100,
                MinimumAvailableTags = 2,
                MaxLagMinutes = 2,
                Tags = [TagName, CompanionTagName],
                Means = [100, 20],
                StandardDeviations = [1, 1],
                CovarianceMatrix = [[1, 0], [0, 1]],
                CorrelationMatrix = [[1, 0], [0, 1]]
            };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(model));
            persistence.InvalidarCache(ClienteId, MultivariateGroupName);
            return persistence;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class StructuredMultivariateLogger
        : ILogger<MultivariateShadowRuntimeCoordinator>
    {
        public List<IReadOnlyDictionary<string, object?>> Results { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            EmptyScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) ||
                state is not IEnumerable<KeyValuePair<string, object?>> properties)
                return;

            var data = properties
                .Where(pair => pair.Key != "{OriginalFormat}")
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Results.Add(data);
        }

        private sealed class EmptyScope : IDisposable
        {
            public static readonly EmptyScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class ScenarioFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ScenarioFixture(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<ScenarioFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync();

            return new ScenarioFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
