using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public sealed class BehaviorMemorySnapshotRebuildServiceTests
{
    [Fact]
    public async Task RebuildAsync_DeveAgregarOcorrenciasPersistidasENovas()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await SeedBehaviorAsync(
                fixture.Context);

        var existing =
            CreateOccurrence(
                behavior.Id,
                new DateTime(
                    2026, 9, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: true);

        fixture.Context.TagsComportamentosOcorrenciasIa.Add(
            existing);

        await fixture.Context.SaveChangesAsync();

        var pending =
            CreateOccurrence(
                0,
                new DateTime(
                    2026, 9, 2, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: true);

        var service =
            CreateService(
                fixture.Context);

        var snapshot =
            await service.RebuildAsync(
                behavior.Id,
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    pending
                });

        Assert.Equal(
            40,
            snapshot.TotalRegistrosUsados);

        Assert.Equal(
            existing.DataHoraInicio,
            snapshot.PeriodoInicio);

        Assert.Equal(
            pending.DataHoraFim,
            snapshot.PeriodoFim);

        Assert.Contains(
            "2 ocorrências",
            snapshot.JustificativaDeadband);
    }

    [Fact]
    public async Task RebuildAsync_OcorrenciaDesabilitada_NaoDeveEntrarNoSnapshot()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await SeedBehaviorAsync(
                fixture.Context);

        var enabled =
            CreateOccurrence(
                behavior.Id,
                new DateTime(
                    2026, 9, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: true);

        var disabled =
            CreateOccurrence(
                behavior.Id,
                new DateTime(
                    2026, 9, 2, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: false);

        fixture.Context.TagsComportamentosOcorrenciasIa.AddRange(
            enabled,
            disabled);

        await fixture.Context.SaveChangesAsync();

        var service =
            CreateService(
                fixture.Context);

        var snapshot =
            await service.RebuildAsync(
                behavior.Id,
                "CLIENTE-A",
                "TAG1");

        Assert.Equal(
            20,
            snapshot.TotalRegistrosUsados);

        Assert.Equal(
            enabled.DataHoraInicio,
            snapshot.PeriodoInicio);

        Assert.Equal(
            enabled.DataHoraFim,
            snapshot.PeriodoFim);
    }

    [Fact]
    public async Task RebuildAsync_IntervaloPendenteDuplicado_NaoDeveContarDuasVezes()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await SeedBehaviorAsync(
                fixture.Context);

        var existing =
            CreateOccurrence(
                behavior.Id,
                new DateTime(
                    2026, 9, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: true);

        fixture.Context.TagsComportamentosOcorrenciasIa.Add(
            existing);

        await fixture.Context.SaveChangesAsync();

        var duplicatePending =
            CreateOccurrence(
                0,
                existing.DataHoraInicio,
                usedForLearning: true);

        var service =
            CreateService(
                fixture.Context);

        var snapshot =
            await service.RebuildAsync(
                behavior.Id,
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    duplicatePending
                });

        Assert.Equal(
            20,
            snapshot.TotalRegistrosUsados);
    }

    [Fact]
    public async Task RebuildAsync_HistoricoInsuficiente_DeveFalharSemInventarSnapshot()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await SeedBehaviorAsync(
                fixture.Context);

        fixture.Context.TagsComportamentosOcorrenciasIa.Add(
            CreateOccurrence(
                behavior.Id,
                new DateTime(
                    2026, 9, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: true));

        await fixture.Context.SaveChangesAsync();

        var service =
            new BehaviorMemorySnapshotRebuildService(
                fixture.Context,
                new ShortHistoryService(),
                new TagProfileStatisticsCalculator(
                    new HistoricoLimpezaService()),
                new BehaviorMemoryPersistencePlanBuilder());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.RebuildAsync(
                    behavior.Id,
                    "CLIENTE-A",
                    "TAG1"));
    }


    [Fact]
    public async Task RebuildCharacteristicsAsync_DeveReconstruirFeaturesCompletas()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await SeedBehaviorAsync(
                fixture.Context);

        fixture.Context.TagsComportamentosOcorrenciasIa.Add(
            CreateOccurrence(
                behavior.Id,
                new DateTime(
                    2026, 9, 1, 0, 0, 0,
                    DateTimeKind.Utc),
                usedForLearning: true));

        await fixture.Context.SaveChangesAsync();

        var service =
            CreateService(
                fixture.Context);

        var characteristics =
            await service.RebuildCharacteristicsAsync(
                behavior.Id,
                "CLIENTE-A",
                "TAG1");

        var characteristic =
            Assert.Single(
                characteristics);

        Assert.True(
            characteristic.IsCharacterizable);

        Assert.NotNull(
            characteristic.ProfileFeatures);

        Assert.Equal(
            20,
            characteristic.UsedSampleCount);

        Assert.True(
            characteristic.IntervaloInterquartil >= 0);

        Assert.True(
            characteristic.ProfileFeatures!.RobustScale >= 0);

        Assert.True(
            characteristic.ProfileFeatures.TypicalAbsoluteStep >= 0);
    }

    [Fact]
    public async Task RebuildCharacteristicsAsync_DataUnspecifiedPersistida_DevePreservarFormato()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await SeedBehaviorAsync(
                fixture.Context);

        var unspecifiedStart =
            new DateTime(
                2026, 9, 1, 0, 0, 0,
                DateTimeKind.Unspecified);

        fixture.Context.TagsComportamentosOcorrenciasIa.Add(
            CreateOccurrence(
                behavior.Id,
                unspecifiedStart,
                usedForLearning: true));

        await fixture.Context.SaveChangesAsync();

        var service =
            CreateService(
                fixture.Context);

        var characteristics =
            await service.RebuildCharacteristicsAsync(
                behavior.Id,
                "CLIENTE-A",
                "TAG1");

        var characteristic =
            Assert.Single(
                characteristics);

        Assert.Equal(
            DateTimeKind.Unspecified,
            characteristic.Inicio.Kind);

        Assert.Equal(
            DateTimeKind.Unspecified,
            characteristic.Fim.Kind);

        Assert.Equal(
            unspecifiedStart,
            characteristic.Inicio);
    }

    private static BehaviorMemorySnapshotRebuildService CreateService(
        AppDbContext context)
    {
        return new BehaviorMemorySnapshotRebuildService(
            context,
            new FakeHistoricoService(),
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService()),
            new BehaviorMemoryPersistencePlanBuilder());
    }

    private static async Task<TagComportamentoIa> SeedBehaviorAsync(
        AppDbContext context)
    {
        var behavior =
            new TagComportamentoIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Codigo =
                    "BEH-001",

                Status =
                    TagComportamentoIa.StatusKnown,

                Origem =
                    TagComportamentoIa.OrigemAutomatic,

                Classificacao =
                    TagComportamentoIa.ClassificacaoUndefined,

                PrimeiraObservacao =
                    new DateTime(
                        2026, 9, 1, 0, 0, 0,
                        DateTimeKind.Utc),

                UltimaObservacao =
                    new DateTime(
                        2026, 9, 1, 0, 19, 0,
                        DateTimeKind.Utc),

                QuantidadeOcorrencias =
                    1,

                TotalAmostras =
                    20,

                Confianca =
                    80
            };

        context.TagsComportamentosIa.Add(
            behavior);

        await context.SaveChangesAsync();

        return behavior;
    }

    private static TagComportamentoOcorrenciaIa CreateOccurrence(
        int behaviorId,
        DateTime start,
        bool usedForLearning)
    {
        return new TagComportamentoOcorrenciaIa
        {
            TagComportamentoIaId =
                behaviorId,

            DataHoraInicio =
                start,

            DataHoraFim =
                start.AddMinutes(
                    19),

            QuantidadeAmostras =
                20,

            Confianca =
                80,

            UtilizadaNoAprendizado =
                usedForLearning
        };
    }

    private sealed class FakeHistoricoService :
        IHistoricoService
    {
        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return new List<LeituraBruta>();
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return Enumerable.Range(
                    0,
                    20)
                .Select(index =>
                    new LeituraBruta
                    {
                        DataHora =
                            inicio.AddMinutes(
                                index),

                        Valor =
                            (float)(
                                40 +
                                (
                                    index % 2 == 0
                                        ? -0.25
                                        : 0.25
                                ))
                    })
                .ToList();
        }
    }

    private sealed class ShortHistoryService :
        IHistoricoService
    {
        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return new List<LeituraBruta>();
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return Enumerable.Range(
                    0,
                    5)
                .Select(index =>
                    new LeituraBruta
                    {
                        DataHora =
                            inicio.AddMinutes(
                                index),

                        Valor =
                            40
                    })
                .ToList();
        }
    }

    private sealed class DatabaseFixture :
        IAsyncDisposable
    {
        private DatabaseFixture(
            SqliteConnection connection,
            AppDbContext context)
        {
            Connection =
                connection;

            Context =
                context;
        }

        public SqliteConnection Connection { get; }

        public AppDbContext Context { get; }

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(
                        connection)
                    .Options;

            var context =
                new AppDbContext(
                    options);

            await context.Database
                .EnsureCreatedAsync();

            return new DatabaseFixture(
                connection,
                context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
