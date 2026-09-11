using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class MultivariateIndustrialEventReviewControllerTests
{
    [Fact]
    public async Task RevisaoValida_DevePersistirUsandoClienteGrupoEMinuteIndexDaRota()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        var action =
            await fixture.Controller
                .UpsertAsync(
                    "PK7",
                    "GRUPO-AB",
                    1065402040,
                    Request(
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent),
                    CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                action.Result);

        var review =
            Assert.IsType<
                MultivariateIndustrialEventReviewIa>(
                    ok.Value);

        Assert.Equal(
            "PK7",
            review.ClienteId);

        Assert.Equal(
            "GRUPO-AB",
            review.GroupName);

        Assert.Equal(
            1065402040,
            review.MinuteIndex);

        Assert.Equal(
            1,
            await fixture.Context
                .MultivariateIndustrialEventReviews
                .CountAsync());
    }

    [Fact]
    public async Task StatusInvalido_DeveRetornarBadRequest()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        var action =
            await fixture.Controller
                .UpsertAsync(
                    "PK7",
                    "GRUPO-AB",
                    10,
                    Request(
                        "Aprovado"),
                    CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            action.Result);

        Assert.Equal(
            0,
            await fixture.Context
                .MultivariateIndustrialEventReviews
                .CountAsync());
    }

    [Fact]
    public async Task SemEvidenceReference_DeveRetornarBadRequest()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        var request =
            Request(
                MultivariateIndustrialReviewStatuses
                    .Inconclusive);

        request.EvidenceReference =
            "";

        var action =
            await fixture.Controller
                .UpsertAsync(
                    "PK7",
                    "GRUPO-AB",
                    10,
                    request,
                    CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            action.Result);
    }

    [Fact]
    public async Task Listagem_DeveRetornarSomenteRevisoesDaIdentidadeSolicitada()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        await fixture.Store
            .UpsertAsync(
                new MultivariateIndustrialEventReviewUpsert
                {
                    ClienteId =
                        "PK7",

                    GroupName =
                        "GRUPO-AB",

                    MinuteIndex =
                        10,

                    SourceWindow =
                        MultivariateIndustrialEvidenceWindows
                            .Holdout,

                    ModelVersion =
                        1,

                    ModelTrainedAtUtc =
                        Fixture.ModelTrainedAtUtc,

                    Threshold =
                        75,

                    ReviewStatus =
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation,

                    Reviewer =
                        "Engenharia",

                    EvidenceReference =
                        "REL-001"
                });

        await fixture.Store
            .UpsertAsync(
                new MultivariateIndustrialEventReviewUpsert
                {
                    ClienteId =
                        "OUTRO",

                    GroupName =
                        "GRUPO-AB",

                    MinuteIndex =
                        20,

                    SourceWindow =
                        MultivariateIndustrialEvidenceWindows
                            .Holdout,

                    ModelVersion =
                        1,

                    ModelTrainedAtUtc =
                        Fixture.ModelTrainedAtUtc,

                    Threshold =
                        75,

                    ReviewStatus =
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation,

                    Reviewer =
                        "Engenharia",

                    EvidenceReference =
                        "REL-002"
                });

        var action =
            await fixture.Controller
                .ListAsync(
                    "PK7",
                    "GRUPO-AB",
                    1,
                    Fixture.ModelTrainedAtUtc,
                    75,
                    CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                action.Result);

        var reviews =
            Assert.IsAssignableFrom<
                IReadOnlyList<MultivariateIndustrialEventReviewIa>>(
                    ok.Value);

        var review =
            Assert.Single(
                reviews);

        Assert.Equal(
            "PK7",
            review.ClienteId);

        Assert.Equal(
            10,
            review.MinuteIndex);
    }

    private static MultivariateIndustrialEventReviewRequest
        Request(
            string status)
    {
        return new MultivariateIndustrialEventReviewRequest
        {
            SourceWindow =
                MultivariateIndustrialEvidenceWindows
                    .Holdout,

            ModelVersion =
                1,

            ModelTrainedAtUtc =
                Fixture.ModelTrainedAtUtc,

            Threshold =
                75,

            ReviewStatus =
                status,

            Reviewer =
                "Engenharia",

            Notes =
                "Revisão de teste.",

            EvidenceReference =
                "MV08.4D2B-TEST"
        };
    }

    private sealed class Fixture
        : IAsyncDisposable
    {
        public static readonly DateTime ModelTrainedAtUtc =
            new(
                2026,
                9,
                3,
                19,
                7,
                2,
                DateTimeKind.Utc);

        private readonly SqliteConnection
            _connection;

        private Fixture(
            SqliteConnection connection,
            AppDbContext context,
            MultivariateIndustrialEventReviewStore store,
            MultivariateIndustrialEventReviewController controller)
        {
            _connection =
                connection;

            Context =
                context;

            Store =
                store;

            Controller =
                controller;
        }

        public AppDbContext Context { get; }

        public MultivariateIndustrialEventReviewStore Store { get; }

        public MultivariateIndustrialEventReviewController Controller { get; }

        public static async Task<Fixture>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:");

            await connection
                .OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                    AppDbContext>()
                    .UseSqlite(
                        connection)
                    .Options;

            var context =
                new AppDbContext(
                    options);

            await context.Database
                .EnsureCreatedAsync();

            var store =
                new MultivariateIndustrialEventReviewStore(
                    context);

            var controller =
                new MultivariateIndustrialEventReviewController(
                    store);

            return new Fixture(
                connection,
                context,
                store,
                controller);
        }

        public async ValueTask DisposeAsync()
        {
            await Context
                .DisposeAsync();

            await _connection
                .DisposeAsync();
        }
    }
}
