using IAApi.Services.AnomalyDetection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using IAApi.Services.AnomalyDetection.Orchestration;

namespace IAApi.Tests;

public sealed class PredictionServiceDeadbandWiringTests
{
    [Fact]
    public async Task PredictionService_UsaDeadbandPersistidoDoPerfilNoDetectorInput()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        /*
         * Configuração real exigida pelo PredictionService.
         */
        context.TagsIaConfig.Add(
            new TagIaConfig
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",
                IaAtiva = true
            });

        /*
         * Perfil já treinado.
         *
         * Este é o dado que queremos provar que chega
         * até o DetectorInput usado pelos detectores.
         */
        context.PerfisIa.Add(
            new TagPerfilIa
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",

                Media = 100,
                DesvioPadrao = 2,

                VariacaoMinimaRelevante = 16,
                DeadbandSugerido = 16,
                ConfiancaDeadband = 94,

                VariacaoMinimaCalculadaAutomaticamente = true,

                DataTreinamento =
                    new DateTime(
                        2026, 8, 21, 10, 0, 0,
                        DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();

        /*
         * Histórico com diversidade suficiente para o
         * TagBehaviorProfileResolver classificá-lo como contínuo
         * e permitir a execução do detector capturador.
         */
        var history =
            Enumerable.Range(0, 40)
                .Select(i =>
                    new LeituraBruta
                    {
                        DataHora =
                            new DateTime(
                                2026, 8, 21, 12, 0, 0,
                                DateTimeKind.Utc)
                            .AddMinutes(i),

                        Valor =
                            (float)(
                                95.0 +
                                (i * 0.27) +
                                ((i % 3) * 0.013))
                    })
                .ToList();

        var historicoService =
            new FakeHistoricoService(
                history);

        /*
         * Detector fake usado SOMENTE para capturar o
         * DetectorInput que o PredictionService entrega
         * ao EnsembleDetectionService real.
         */
        var captureDetector =
            new CaptureDetector();

        var ensemble =
            CreateEnsemble(
                captureDetector);

        var processoAnaliseService =
            new ProcessoAnaliseService(
                context);

        /*
         * As demais dependências não são necessárias para
         * caracterizar este wiring.
         *
         * A execução será interrompida propositalmente logo
         * depois do ensemble, quando o PredictionService tentar
         * acessar o RuntimeFunctionalDecisionContextProvider nulo.
         *
         * Nesse momento o CaptureDetector já terá recebido
         * o DetectorInput real criado pelo PredictionService.
         */
        var predictionService =
            CreatePredictionService(
                context,
                processoAnaliseService,
                ensemble,
                historicoService);

        var request =
            new AnomaliaRequest
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",

                Valor = 101,

                DataHora =
                    new DateTime(
                        2026, 8, 21, 13, 0, 0,
                        DateTimeKind.Utc)
            };

        /*
         * O NullReferenceException é esperado neste teste,
         * porque propositalmente não montamos a cadeia posterior
         * ao ensemble.
         *
         * O objeto que queremos auditar já foi capturado antes.
         */
        await Assert.ThrowsAsync<NullReferenceException>(
            async () =>
                await predictionService.Detectar(
                    request));

        Assert.NotNull(
            captureDetector.CapturedInput);

        var captured =
            captureDetector.CapturedInput!;

        /*
         * CONTRATO ESPERADO DE PRODUÇÃO:
         *
         * O perfil treinado possui deadband 16 e confiança 94.
         * Esses dados precisam chegar aos detectores.
         */
        Assert.Equal(
            16,
            captured.VariacaoMinimaRelevante);

        Assert.Equal(
            94,
            captured.ConfiancaVariacaoMinima);

        Assert.False(
            captured.VariacaoMinimaDefinidaPeloCliente);

        /*
         * Também validamos a consequência funcional:
         *
         * com o perfil corretamente conectado,
         * BaselineProtectionProfile deve reconhecer a origem
         * como "profile", e não recalcular "history-p95".
         */
        var protection =
            BaselineProtectionProfile.Build(
                captured,
                captured.MediaHistorica);

        Assert.Equal(
            "profile",
            protection.DeadbandSource);

        Assert.Equal(
            16,
            protection.Deadband);

        Assert.Equal(
            0.94,
            protection.DeadbandConfidence,
            precision: 2);
    }

    [Fact]
    public async Task PredictionService_SemConfiguracaoDeRegime_MantemBaselineGlobalMesmoComProvider()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.TagsIaConfig.Add(
            new TagIaConfig
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",
                IaAtiva = true
            });

        context.PerfisIa.Add(
            new TagPerfilIa
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",

                Media = 100,
                DesvioPadrao = 2,

                VariacaoMinimaRelevante = 16,
                DeadbandSugerido = 16,
                ConfiancaDeadband = 94,

                VariacaoMinimaCalculadaAutomaticamente = true,
                DataTreinamento = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var history =
            Enumerable.Range(0, 40)
                .Select(i =>
                    new LeituraBruta
                    {
                        DataHora =
                            new DateTime(
                                2026, 8, 21, 12, 0, 0,
                                DateTimeKind.Utc)
                            .AddMinutes(i),

                        Valor =
                            (float)(
                                95.0 +
                                (i * 0.27) +
                                ((i % 3) * 0.013))
                    })
                .ToList();

        var historicoService =
            new FakeHistoricoService(
                history);

        var regimeProvider =
            CreateRegimeProvider(
                context,
                historicoService);

        var captureDetector =
            new CaptureDetector();

        var ensemble =
            CreateEnsemble(
                captureDetector);

        var processoAnaliseService =
            new ProcessoAnaliseService(
                context);

        var predictionService =
            CreatePredictionService(
                context,
                processoAnaliseService,
                ensemble,
                historicoService,
                regimeProvider);

        var request =
            new AnomaliaRequest
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",
                Valor = 101,

                DataHora =
                    new DateTime(
                        2026, 8, 21, 13, 0, 0,
                        DateTimeKind.Utc)
            };

        await Assert.ThrowsAsync<NullReferenceException>(
            async () =>
                await predictionService.Detectar(
                    request));

        Assert.NotNull(
            captureDetector.CapturedInput);

        var captured =
            captureDetector.CapturedInput!;

        /*
         * O provider existe, como acontecerá em produção,
         * porém TAG1 não possui TagRegimeOperacionalIa.
         *
         * Portanto o fluxo global deve permanecer intacto.
         */
        Assert.Equal(
            100,
            captured.MediaHistorica);

        Assert.Equal(
            2,
            captured.DesvioPadraoHistorico);

        Assert.Equal(
            16,
            captured.VariacaoMinimaRelevante);

        Assert.Equal(
            94,
            captured.ConfiancaVariacaoMinima);

        Assert.Equal(
            40,
            captured.Historico.Count);
    }

    [Fact]
    public async Task PredictionService_ComRegimeAplicado_EntregaBaselineCondicionadoAoDetectorInput()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.TagsIaConfig.Add(
            new TagIaConfig
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",
                IaAtiva = true
            });

        /*
         * Perfil global propositalmente muito diferente
         * do comportamento do regime OFF.
         *
         * Se o wiring falhar, estes valores chegarão ao detector
         * e o teste detectará a regressão.
         */
        context.PerfisIa.Add(
            new TagPerfilIa
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1",

                Media = 100,
                DesvioPadrao = 2,

                VariacaoMinimaRelevante = 16,
                DeadbandSugerido = 16,
                ConfiancaDeadband = 94,

                VariacaoMinimaCalculadaAutomaticamente = true,
                DataTreinamento = DateTime.UtcNow
            });

        context.TagsRegimesOperacionaisIa.Add(
            new TagRegimeOperacionalIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                TagRegime =
                    "PUMP1",

                Ativo =
                    true,

                LagMinutos =
                    1,

                ToleranciaAlinhamentoMinutos =
                    2,

                ToleranciaEstado =
                    0.001,

                MinimoAmostrasPorRegime =
                    10
            });

        await context.SaveChangesAsync();

        /*
         * TAG1 possui dois regimes saudáveis:
         *
         * OFF -> valores próximos de 10
         * ON  -> valores próximos de 30
         *
         * Targets:
         * 12:01 .. 12:20 -> OFF
         * 12:21 .. 12:40 -> ON
         */
        var targetHistory =
            new List<LeituraBruta>();

        for (var index = 0;
             index < 40;
             index++)
        {
            var isOff =
                index < 20;

            var baseValue =
                isOff
                    ? 10.0
                    : 30.0;

            targetHistory.Add(
                new LeituraBruta
                {
                    DataHora =
                        new DateTime(
                            2026, 8, 21, 12, 1, 0,
                            DateTimeKind.Utc)
                        .AddMinutes(index),

                    Valor =
                        (float)(
                            baseValue +
                            ((index % 5) * 0.05) +
                            ((index % 3) * 0.005))
                });
        }

        /*
         * PUMP1:
         *
         * 12:00 .. 12:19 -> OFF (0)
         * 12:20 .. 12:39 -> ON  (1)
         *
         * Com Lag=1:
         * target 12:01 usa regime 12:00,
         * target 12:20 usa regime 12:19, etc.
         */
        var regimeHistory =
            new List<LeituraBruta>();

        for (var index = 0;
             index < 40;
             index++)
        {
            regimeHistory.Add(
                new LeituraBruta
                {
                    DataHora =
                        new DateTime(
                            2026, 8, 21, 12, 0, 0,
                            DateTimeKind.Utc)
                        .AddMinutes(index),

                    Valor =
                        index < 20
                            ? 0
                            : 1
                });
        }

        var historicoService =
            new FakeHistoricoService(
                targetHistory,
                "PUMP1",
                regimeHistory);

        var regimeProvider =
            CreateRegimeProvider(
                context,
                historicoService);

        /*
         * Calculamos a saída esperada pelo provider já testado
         * separadamente. Este teste passa a verificar somente se
         * PredictionService encaminha esses valores ao DetectorInput.
         */
        var requestTimestamp =
            new DateTime(
                2026, 8, 21, 12, 20, 0,
                DateTimeKind.Utc);

        var expectedBaseline =
            regimeProvider.Resolve(
                "CLIENTE-A",
                "TAG1",
                targetHistory,
                requestTimestamp);

        Assert.True(
            expectedBaseline.Applied,
            expectedBaseline.Reason);

        Assert.NotNull(
            expectedBaseline.Statistics);

        Assert.Equal(
            0,
            expectedBaseline.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            20,
            expectedBaseline.SelectedSamples);

        var captureDetector =
            new CaptureDetector();

        var ensemble =
            CreateEnsemble(
                captureDetector);

        var processoAnaliseService =
            new ProcessoAnaliseService(
                context);

        var predictionService =
            CreatePredictionService(
                context,
                processoAnaliseService,
                ensemble,
                historicoService,
                regimeProvider);

        var request =
            new AnomaliaRequest
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Valor =
                    10.10f,

                DataHora =
                    requestTimestamp
            };

        await Assert.ThrowsAsync<NullReferenceException>(
            async () =>
                await predictionService.Detectar(
                    request));

        Assert.NotNull(
            captureDetector.CapturedInput);

        var captured =
            captureDetector.CapturedInput!;

        var expectedStatistics =
            expectedBaseline.Statistics!;

        /*
         * O DetectorInput deve refletir o baseline OFF,
         * e não o perfil global persistido (100 / 2 / 16 / 94).
         */
        Assert.Equal(
            expectedStatistics.Media,
            captured.MediaHistorica,
            precision: 10);

        Assert.Equal(
            expectedStatistics.DesvioPadrao,
            captured.DesvioPadraoHistorico,
            precision: 10);

        Assert.Equal(
            expectedStatistics.DeadbandRecomendado,
            captured.VariacaoMinimaRelevante,
            precision: 10);

        Assert.Equal(
            expectedStatistics.ConfiancaDeadband,
            captured.ConfiancaVariacaoMinima,
            precision: 10);

        Assert.Equal(
            20,
            captured.Historico.Count);

        Assert.All(
            captured.Historico,
            point =>
                Assert.InRange(
                    point.Valor,
                    9.9,
                    11.0));

        Assert.NotEqual(
            100,
            captured.MediaHistorica);

        Assert.NotEqual(
            16,
            captured.VariacaoMinimaRelevante);

        var protection =
            BaselineProtectionProfile.Build(
                captured,
                captured.MediaHistorica);

        Assert.Equal(
            "profile",
            protection.DeadbandSource);

        Assert.Equal(
            captured.VariacaoMinimaRelevante,
            protection.Deadband,
            precision: 10);
    }

    private static EnsembleDetectionService CreateEnsemble(
        CaptureDetector captureDetector)
    {
        return new EnsembleDetectionService(
            new IAnomalyDetector[]
            {
                captureDetector
            },
            Options.Create(
                new AnomalyDetectionOptions()),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());
    }

    private static OperationalRegimeBaselineProvider CreateRegimeProvider(
        AppDbContext context,
        IHistoricoService historicoService)
    {
        var historyResolver =
            new OperationalRegimeHistoryResolver();

        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var baselineService =
            new OperationalRegimeBaselineService(
                historyResolver,
                statisticsCalculator);

        return new OperationalRegimeBaselineProvider(
            context,
            historicoService,
            baselineService);
    }

    private static PredictionService CreatePredictionService(
        AppDbContext context,
        ProcessoAnaliseService processoAnaliseService,
        EnsembleDetectionService ensemble,
        IHistoricoService historicoService,
        OperationalRegimeBaselineProvider? regimeProvider = null)
    {

var predictionContextLoader =
    new PredictionContextLoader(
        context,
        processoAnaliseService);

var predictionDetectionPreparationService =
    new PredictionDetectionPreparationService(
        historicoService,
        regimeProvider,
        behaviorRuntimeBaselineProvider: null);

return new PredictionService(
    predictionContextLoader,
    null!,
    null!,
    ensemble,
    predictionDetectionPreparationService,
    null!,
    null!,
    null!);
    }

    private sealed class CaptureDetector :
        IAnomalyDetector
    {
        public string Nome =>
            "CaptureDetector";

        public double Peso =>
            1.0;

        public DetectorInput? CapturedInput
        {
            get;
            private set;
        }

        public DetectorResult Detectar(
            DetectorInput input)
        {
            CapturedInput =
                input;

            /*
             * O ensemble já possui fail-safe para exceções
             * individuais dos detectores.
             *
             * Não precisamos construir um DetectorResult artificial:
             * nosso único objetivo é capturar o input recebido.
             */
            throw new InvalidOperationException(
                "Fim intencional do detector capturador.");
        }
    }

    private sealed class FakeHistoricoService :
        IHistoricoService
    {
        private readonly List<LeituraBruta>
            _targetHistory;

        private readonly string?
            _regimeTagName;

        private readonly List<LeituraBruta>?
            _regimeHistory;

        public FakeHistoricoService(
            List<LeituraBruta> targetHistory,
            string? regimeTagName = null,
            List<LeituraBruta>? regimeHistory = null)
        {
            _targetHistory =
                targetHistory;

            _regimeTagName =
                regimeTagName;

            _regimeHistory =
                regimeHistory;
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return _targetHistory;
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            var source =
                !string.IsNullOrWhiteSpace(
                    _regimeTagName) &&
                string.Equals(
                    tagName,
                    _regimeTagName,
                    StringComparison.OrdinalIgnoreCase) &&
                _regimeHistory is not null
                    ? _regimeHistory
                    : _targetHistory;

            return source
                .Where(x =>
                    x.DataHora >= inicio &&
                    x.DataHora <= fim)
                .OrderBy(x =>
                    x.DataHora)
                .ToList();
        }
    }
}
