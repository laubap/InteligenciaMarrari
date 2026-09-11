using IAApi.Services.AnomalyDetection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

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
            new EnsembleDetectionService(
                new IAnomalyDetector[]
                {
                    captureDetector
                },
                Options.Create(
                    new AnomalyDetectionOptions()),
                new TagBehaviorProfileResolver(),
                new DetectorApplicabilityPolicy());

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
            new PredictionService(
                context,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                processoAnaliseService,
                null!,
                null!,
                ensemble,
                historicoService,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);

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
         *
         * No código atual esperamos que estas assertions falhem,
         * demonstrando o bug de wiring.
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
            _history;

        public FakeHistoricoService(
            List<LeituraBruta> history)
        {
            _history =
                history;
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return _history;
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return _history
                .Where(x =>
                    x.DataHora >= inicio &&
                    x.DataHora <= fim)
                .ToList();
        }
    }
}