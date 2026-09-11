using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class MultivariateIndustrialEventReviewStoreTests
{
    [Fact]
    public async Task DevePersistirRevisaoIndustrial()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        var review =
            await fixture.Store
                .UpsertAsync(
                    Input(
                        reviewStatus:
                            MultivariateIndustrialReviewStatuses
                                .ConfirmedIndustrialEvent));

        Assert.True(
            review.Id > 0);

        Assert.Equal(
            "PK7",
            review.ClienteId);

        Assert.Equal(
            MultivariateIndustrialReviewStatuses
                .ConfirmedIndustrialEvent,
            review.ReviewStatus);

        Assert.Equal(
            "Engenharia",
            review.Reviewer);
    }

    [Fact]
    public async Task MesmoEventoMesmoModelo_DeveAtualizarSemDuplicar()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        var first =
            await fixture.Store
                .UpsertAsync(
                    Input(
                        reviewStatus:
                            MultivariateIndustrialReviewStatuses
                                .Inconclusive));

        var second =
            await fixture.Store
                .UpsertAsync(
                    Input(
                        reviewStatus:
                            MultivariateIndustrialReviewStatuses
                                .NormalOperation,
                        notes:
                            "Operação normal confirmada."));

        Assert.Equal(
            first.Id,
            second.Id);

        Assert.Equal(
            MultivariateIndustrialReviewStatuses
                .NormalOperation,
            second.ReviewStatus);

        Assert.Equal(
            1,
            await fixture.Context
                .MultivariateIndustrialEventReviews
                .CountAsync());
    }

    [Fact]
    public async Task StatusInvalido_DeveFalhar()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        await Assert.ThrowsAsync<
            ArgumentException>(
                () =>
                    fixture.Store
                        .UpsertAsync(
                            Input(
                                reviewStatus:
                                    "Aprovado")));
    }

    [Fact]
    public async Task Listagem_DeveIsolarCliente()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        await fixture.Store
            .UpsertAsync(
                Input(
                    reviewStatus:
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation));

        await fixture.Store
            .UpsertAsync(
                Input(
                    reviewStatus:
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent,
                    clienteId:
                        "OUTRO"));

        var pk7 =
            await fixture.Store
                .ListAsync(
                    "PK7",
                    "PK7_TESTE_MULTIVARIADO",
                    1,
                    Fixture.ModelTrainedAtUtc,
                    75);

        var review =
            Assert.Single(
                pk7);

        Assert.Equal(
            "PK7",
            review.ClienteId);
    }

    [Fact]
    public async Task MesmoMinuteIndexEmOutroModelo_NaoDeveReutilizarRevisao()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        await fixture.Store
            .UpsertAsync(
                Input(
                    reviewStatus:
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation));

        await fixture.Store
            .UpsertAsync(
                Input(
                    reviewStatus:
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent,
                    modelTrainedAtUtc:
                        Fixture.ModelTrainedAtUtc
                            .AddDays(1)));

        Assert.Equal(
            2,
            await fixture.Context
                .MultivariateIndustrialEventReviews
                .CountAsync());
    }

    private static MultivariateIndustrialEventReviewUpsert
        Input(
            string reviewStatus,
            string clienteId = "PK7",
            DateTime? modelTrainedAtUtc = null,
            string? notes = null)
    {
        return new MultivariateIndustrialEventReviewUpsert
        {
            ClienteId =
                clienteId,

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

            MinuteIndex =
                1065402040,

            SourceWindow =
                MultivariateIndustrialEvidenceWindows
                    .Holdout,

            ModelVersion =
                1,

            ModelTrainedAtUtc =
                modelTrainedAtUtc ??
                Fixture.ModelTrainedAtUtc,

            Threshold =
                75,

            ReviewStatus =
                reviewStatus,

            Reviewer =
                "Engenharia",

            Notes =
                notes,

            EvidenceReference =
                "MV08.4C-EVIDENCE",

            ReviewedAtUtc =
                new DateTime(
                    2026,
                    9,
                    10,
                    17,
                    0,
                    0,
                    DateTimeKind.Utc)
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
            MultivariateIndustrialEventReviewStore store)
        {
            _connection =
                connection;

            Context =
                context;

            Store =
                store;
        }

        public AppDbContext Context { get; }

        public MultivariateIndustrialEventReviewStore Store { get; }

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

            return new Fixture(
                connection,
                context,
                store);
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
