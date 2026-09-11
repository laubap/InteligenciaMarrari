using System.Text.Json;
using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class OperationalMultivariateQualificationServiceTests
{
    [Fact]
    public async Task DadosEstaveisSemRevisoesPersistidas_DeveFicarPendente()
    {
        using var fixture =
            await Fixture.CreateAsync();

        var result =
            await fixture.Service
                .QualificarAsync(
                    "PK7",
                    "GRUPO-AB",
                    Fixture.ReferenceStart,
                    Fixture.ReferenceEnd,
                    Fixture.HoldoutStart,
                    Fixture.HoldoutEnd);

        Assert.True(
            result.Qualification
                .TechnicalCriteriaPassed);

        Assert.False(
            result.Qualification
                .EligibleForExplicitApproval);

        Assert.Equal(
            MultivariateOperationalQualificationStatuses
                .TechnicallyQualifiedPendingIndustrialValidation,
            result.Qualification.Status);

        Assert.Contains(
            "ValidacaoIndustrialNaoConfirmada",
            result.Qualification
                .GovernanceBlockers);

        Assert.Equal(
            1000,
            result.ReferenceSummary
                .ApplicableResults);

        Assert.Equal(
            500,
            result.HoldoutSummary
                .ApplicableResults);

        Assert.NotNull(
            result.IndustrialValidationEvidence);

        Assert.True(
            result.IndustrialValidationEvidence
                .EvidenceIsCompleteForObservedAnomalies);

        Assert.Equal(
            result.ReferenceSummary.AnomalyCount +
            result.HoldoutSummary.AnomalyCount,
            result.IndustrialValidationEvidence
                .EvidenceEventCount);
    }

    [Fact]
    public async Task SemEventosAnomalos_DeveContinuarPendenteMesmoComInfraestruturaDeRevisao()
    {
        using var fixture =
            await Fixture.CreateAsync();

        var result =
            await fixture.Service
                .QualificarAsync(
                    "PK7",
                    "GRUPO-AB",
                    Fixture.ReferenceStart,
                    Fixture.ReferenceEnd,
                    Fixture.HoldoutStart,
                    Fixture.HoldoutEnd);

        Assert.True(
            result.Qualification
                .TechnicalCriteriaPassed);

        Assert.False(
            result.Qualification
                .EligibleForExplicitApproval);

        Assert.Equal(
            MultivariateOperationalQualificationStatuses
                .TechnicallyQualifiedPendingIndustrialValidation,
            result.Qualification.Status);

        Assert.Contains(
            "NenhumEventoAnomaloParaRevisao",
            result.IndustrialReviewAssessment
                .Blockers);

        Assert.Contains(
            "ValidacaoIndustrialNaoConfirmada",
            result.Qualification
                .GovernanceBlockers);
    }

    [Fact]
    public async Task RevisoesPersistidasCompletas_DeveDerivarValidacaoIndustrial()
    {
        using var fixture =
            await Fixture.CreateAsync(
                includeAnomaly:
                    true);

        var before =
            await fixture.Service
                .QualificarAsync(
                    "PK7",
                    "GRUPO-AB",
                    Fixture.ReferenceStart,
                    Fixture.ReferenceEnd,
                    Fixture.HoldoutStart,
                    Fixture.HoldoutEnd);

        var evidence =
            Assert.Single(
                before.IndustrialValidationEvidence
                    .Events);

        Assert.False(
            before.IndustrialReviewAssessment
                .IndustrialValidationConfirmed);

        await fixture.ReviewStore
            .UpsertAsync(
                new MultivariateIndustrialEventReviewUpsert
                {
                    ClienteId =
                        evidence.ClienteId,

                    GroupName =
                        evidence.GroupName,

                    MinuteIndex =
                        evidence.MinuteIndex,

                    SourceWindow =
                        evidence.SourceWindow,

                    ModelVersion =
                        evidence.ModelVersion,

                    ModelTrainedAtUtc =
                        evidence.ModelTrainedAtUtc,

                    Threshold =
                        evidence.Threshold,

                    ReviewStatus =
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent,

                    Reviewer =
                        "Engenharia",

                    EvidenceReference =
                        "RELATORIO-INDUSTRIAL-001"
                });

        var after =
            await fixture.Service
                .QualificarAsync(
                    "PK7",
                    "GRUPO-AB",
                    Fixture.ReferenceStart,
                    Fixture.ReferenceEnd,
                    Fixture.HoldoutStart,
                    Fixture.HoldoutEnd);

        Assert.True(
            after.IndustrialReviewAssessment
                .IndustrialValidationConfirmed);

        Assert.True(
            after.Qualification
                .IndustrialValidationConfirmed);

        Assert.True(
            after.Qualification
                .EligibleForExplicitApproval);

        Assert.Equal(
            MultivariateOperationalQualificationStatuses
                .QualifiedForExplicitApproval,
            after.Qualification.Status);
    }

    [Fact]
    public async Task HoldoutSobrepostoAReferencia_DeveFalharAntesDaAnalise()
    {
        using var fixture =
            await Fixture.CreateAsync();

        var exception =
            await Assert.ThrowsAsync<
                ArgumentException>(
                () =>
                    fixture.Service
                        .QualificarAsync(
                            "PK7",
                            "GRUPO-AB",
                            Fixture.ReferenceStart,
                            Fixture.ReferenceEnd,
                            Fixture.ReferenceEnd
                                .AddMinutes(-1),
                            Fixture.HoldoutEnd));

        Assert.Contains(
            "sobrepor",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModeloAusente_DeveFalharAntesDosBacktests()
    {
        using var fixture =
            await Fixture.CreateAsync(
                writeModel:
                    false);

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    fixture.Service
                        .QualificarAsync(
                            "PK7",
                            "GRUPO-AB",
                            Fixture.ReferenceStart,
                            Fixture.ReferenceEnd,
                            Fixture.HoldoutStart,
                            Fixture.HoldoutEnd));

        Assert.Contains(
            "Nenhum modelo",
            exception.Message);
    }

    private sealed class Fixture
        : IDisposable
    {
        public static readonly DateTime ReferenceStart =
            new(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        public static readonly DateTime ReferenceEnd =
            ReferenceStart.AddMinutes(
                999);

        public static readonly DateTime HoldoutStart =
            new(
                2026,
                9,
                4,
                0,
                0,
                0,
                DateTimeKind.Utc);

        public static readonly DateTime HoldoutEnd =
            HoldoutStart.AddMinutes(
                499);

        private readonly string _tempRoot;

        private readonly AppDbContext
            _dbContext;

        private Fixture(
            string tempRoot,
            AppDbContext dbContext,
            MultivariateIndustrialEventReviewStore reviewStore,
            OperationalMultivariateQualificationService service)
        {
            _tempRoot =
                tempRoot;

            _dbContext =
                dbContext;

            ReviewStore =
                reviewStore;

            Service =
                service;
        }

        public MultivariateIndustrialEventReviewStore
            ReviewStore { get; }

        public OperationalMultivariateQualificationService
            Service { get; }

        public static async Task<Fixture> CreateAsync(
            bool writeModel = true,
            bool includeAnomaly = false)
        {
            var tempRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "IAApiTests",
                    "MV08_4B",
                    Guid.NewGuid()
                        .ToString("N"));

            Directory.CreateDirectory(
                tempRoot);

            var environment =
                new TestWebHostEnvironment
                {
                    ContentRootPath =
                        tempRoot
                };

            var persistence =
                new MultivariateModelPersistenceService(
                    environment);

            if (writeModel)
            {
                var path =
                    persistence
                        .ObterCaminhoModelo(
                            "PK7",
                            "GRUPO-AB");

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        path)!);

                await File.WriteAllTextAsync(
                    path,
                    JsonSerializer.Serialize(
                        CreateModel()));
            }

            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
                    {
                        Id =
                            1,

                        Nome =
                            "GRUPO-AB",

                        MinimumAvailableTags =
                            2,

                        MaxLagMinutes =
                            2,

                        Tags =
                            ["TAG-A", "TAG-B"]
                    });

            var history =
                new FakeHistoricoService();

            /*
             * Referência: 1000 vetores.
             * Holdout:     500 vetores.
             *
             * Valores iguais às médias do modelo para criar um cenário
             * estável e determinístico, sem depender de ground truth.
             */
            var holdoutA =
                CreateReadings(
                    HoldoutStart,
                    500,
                    10)
                    .ToArray();

            var holdoutB =
                CreateReadings(
                    HoldoutStart,
                    500,
                    20)
                    .ToArray();

            if (includeAnomaly)
            {
                holdoutA[0].Valor =
                    12;

                holdoutB[0].Valor =
                    22;
            }

            history.Set(
                "TAG-A",
                CreateReadings(
                    ReferenceStart,
                    1000,
                    10)
                    .Concat(
                        holdoutA)
                    .ToArray());

            history.Set(
                "TAG-B",
                CreateReadings(
                    ReferenceStart,
                    1000,
                    20)
                    .Concat(
                        holdoutB)
                    .ToArray());

            var analyzer =
                new MultivariateHistoricalBacktestAnalyzer();

            var backtestService =
                new OperationalMultivariateBacktestService(
                    provider,
                    history,
                    persistence,
                    analyzer);

            var evaluator =
                new MultivariateOperationalQualificationEvaluator();

            var dbOptions =
                new DbContextOptionsBuilder<
                    AppDbContext>()
                    .UseSqlite(
                        $"Data Source={Path.Combine(tempRoot, "reviews.db")};Pooling=False")
                    .Options;

            var dbContext =
                new AppDbContext(
                    dbOptions);

            await dbContext.Database
                .EnsureCreatedAsync();

            var reviewStore =
                new MultivariateIndustrialEventReviewStore(
                    dbContext);

            var reviewAssessmentService =
                new MultivariateIndustrialReviewAssessmentService();

            var service =
                new OperationalMultivariateQualificationService(
                    persistence,
                    backtestService,
                    evaluator,
                    reviewStore,
                    reviewAssessmentService);

            return new Fixture(
                tempRoot,
                dbContext,
                reviewStore,
                service);
        }

        public void Dispose()
        {
            _dbContext.Dispose();

            if (Directory.Exists(
                    _tempRoot))
            {
                Directory.Delete(
                    _tempRoot,
                    recursive:
                        true);
            }
        }

        private static MultivariateGroupModel
            CreateModel()
        {
            return new MultivariateGroupModel
            {
                Version =
                    1,

                ClienteId =
                    "PK7",

                GroupName =
                    "GRUPO-AB",

                TrainedAtUtc =
                    new DateTime(
                        2026,
                        9,
                        3,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc),

                Algorithm =
                    "OnlineMeanCovariance",

                SampleCount =
                    1000,

                MinimumAvailableTags =
                    2,

                MaxLagMinutes =
                    2,

                Tags =
                    ["TAG-A", "TAG-B"],

                Means =
                    [10, 20],

                StandardDeviations =
                    [1, 1],

                CovarianceMatrix =
                [
                    [1, 0],
                    [0, 1]
                ],

                CorrelationMatrix =
                [
                    [1, 0],
                    [0, 1]
                ]
            };
        }

        private static IEnumerable<LeituraBruta>
            CreateReadings(
                DateTime start,
                int count,
                float value)
        {
            for (var index = 0;
                 index < count;
                 index++)
            {
                yield return new LeituraBruta
                {
                    DataHora =
                        start.AddMinutes(
                            index),

                    Valor =
                        value
                };
            }
        }
    }

    private sealed class StaticGrupoProvider
        : IGrupoAnaliseProvider
    {
        private readonly List<GrupoAnaliseDto>
            _groups;

        public StaticGrupoProvider(
            params GrupoAnaliseDto[] groups)
        {
            _groups =
                groups.ToList();
        }

        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            return Task.FromResult(
                _groups
                    .Select(group =>
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
                        })
                    .ToList());
        }
    }

    private sealed class FakeHistoricoService
        : IHistoricoService
    {
        private readonly Dictionary<
            string,
            List<LeituraBruta>> _history =
                new(
                    StringComparer.OrdinalIgnoreCase);

        public void Set(
            string tag,
            params LeituraBruta[] readings)
        {
            _history[tag] =
                readings.ToList();
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return BuscarHistoricoPorPeriodo(
                clienteId,
                tagName,
                DateTime.MinValue,
                DateTime.MaxValue);
        }

        public List<LeituraBruta>
            BuscarHistoricoPorPeriodo(
                string clienteId,
                string tagName,
                DateTime inicio,
                DateTime fim)
        {
            if (!_history.TryGetValue(
                    tagName,
                    out var readings))
            {
                return [];
            }

            return readings
                .Where(item =>
                    item.DataHora >= inicio &&
                    item.DataHora <= fim)
                .Select(item =>
                    new LeituraBruta
                    {
                        DataHora =
                            item.DataHora,

                        Valor =
                            item.Valor
                    })
                .ToList();
        }
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } =
            "IAApi.Tests";

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        public string? WebRootPath { get; set; }

        public string EnvironmentName { get; set; } =
            "Development";

        public string ContentRootPath { get; set; } =
            "";

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
