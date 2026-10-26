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
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IAApi.Tests.IntelligenceLab;

/// <summary>
/// Intelligence Lab 2.2G.2 a 2.2G.4: exercita o ciclo completo de
/// previsão com SQLite isolado, detectores reais e somente a
/// fronteira de aquisição de histórico simulada.
///
/// Os seis cenários originais executam detectores estatísticos; o cenário
/// adicional integra Isolation Forest com um modelo temporário de teste.
/// A decisão funcional avançada e a multivariada não são habilitadas,
/// e as opções hospedadas não são carregadas por esta fixture.
/// </summary>
public sealed class IntelligenceLabBehaviorBaselineEndToEndTests
{
    private const string ClienteId = "IL-BASELINE";
    private const string TagName = "BOMBA.PRESSAO";
    private const string RegimeTagName = "BOMBA.REGIME";

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

    private static async Task<PredictionRun> ExecutePredictionAsync(
        AppDbContext context,
        IReadOnlyList<LeituraBruta> history,
        InMemoryHistorySource? historySource = null,
        BehaviorRuntimeBaselineProvider? behaviorProvider = null,
        PredictionDetectionPreparationService? preparation = null,
        IAnomalyDetector? additionalDetector = null)
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
            new RuntimeAnomalyDecisionOptions { Enabled = false },
            new RuntimeDecisionMetrics());

        var decision = new PredictionDecisionOrchestrationService(
            ensemble,
            interpretation,
            runtimeContext,
            runtimeDecision);

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
            new FeedbackAnomaliaAnaliseService(context));

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
            multivariateShadowRuntimeCoordinator: null,
            diagnosticCollector: new InMemoryDiagnosticCollector(buffer),
            diagnosticVersion: new DiagnosticVersionMetadata(
                "intelligence-lab-2.2G.2",
                "test-build",
                "test-commit",
                "production-defaults"),
            diagnosticHealth: buffer.Health);

        var response = await service.Detectar(CreateRequest(history));
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
