using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Runtime;

public sealed class BehaviorRuntimeBaselineProviderTests
{
    [Fact]
    public async Task ResolveAsync_KnownNormalCompativel_DeveUsarSnapshotExatoSemPersistir()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var history =
            CreateStableHistory(
                start:
                    new DateTime(
                        2026,
                        10,
                        1,
                        10,
                        0,
                        0),
                count:
                    20,
                center:
                    40);

        var behavior =
            await SeedKnownBehaviorAsync(
                fixture.Context,
                history,
                TagComportamentoIa.ClassificacaoNormal,
                latestVersion:
                    2);

        var behaviorCountBefore =
            await fixture.Context
                .TagsComportamentosIa
                .CountAsync();

        var snapshotCountBefore =
            await fixture.Context
                .TagsComportamentosSnapshotsIa
                .CountAsync();

        var provider =
            CreateProvider(
                fixture.Context);

        var result =
            await provider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                history,
                history[^1].DataHora);

        Assert.True(
            result.Recognized);

        Assert.True(
            result.CanApplyBaseline);

        Assert.Equal(
            behavior.Id,
            result.BehaviorId);

        Assert.Equal(
            "BEH-001",
            result.BehaviorCode);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            result.BehaviorClassification);

        Assert.Equal(
            2,
            result.SnapshotVersion);

        Assert.NotNull(
            result.CompatibilityConfidence);

        Assert.NotNull(
            result.Media);

        Assert.NotNull(
            result.DesvioPadrao);

        Assert.NotNull(
            result.DeadbandRecomendado);

        Assert.NotNull(
            result.ConfiancaDeadband);

        Assert.Equal(
            20,
            result.History.Count);

        /*
         * Provider runtime é estritamente read-only.
         */
        Assert.Equal(
            behaviorCountBefore,
            await fixture.Context
                .TagsComportamentosIa
                .CountAsync());

        Assert.Equal(
            snapshotCountBefore,
            await fixture.Context
                .TagsComportamentosSnapshotsIa
                .CountAsync());
    }

    [Fact]
    public async Task ResolveAsync_KnownUndefinedCompativel_DeveReconhecerSemAutorizarBaseline()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var history =
            CreateStableHistory(
                new DateTime(
                    2026,
                    10,
                    1,
                    11,
                    0,
                    0),
                20,
                40);

        await SeedKnownBehaviorAsync(
            fixture.Context,
            history,
            TagComportamentoIa.ClassificacaoUndefined,
            latestVersion:
                1);

        var provider =
            CreateProvider(
                fixture.Context);

        var result =
            await provider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                history,
                history[^1].DataHora);

        Assert.True(
            result.Recognized);

        Assert.False(
            result.CanApplyBaseline);

        Assert.Equal(
            "BEH-001",
            result.BehaviorCode);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            result.BehaviorClassification);

        Assert.Contains(
            "Known não implica Normal",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_KnownAbnormalCompativel_DeveReconhecerSemAutorizarBaseline()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var history =
            CreateStableHistory(
                new DateTime(
                    2026,
                    10,
                    1,
                    12,
                    0,
                    0),
                20,
                40);

        await SeedKnownBehaviorAsync(
            fixture.Context,
            history,
            TagComportamentoIa.ClassificacaoKnownAbnormal,
            latestVersion:
                1);

        var provider =
            CreateProvider(
                fixture.Context);

        var result =
            await provider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                history,
                history[^1].DataHora);

        Assert.True(
            result.Recognized);

        Assert.False(
            result.CanApplyBaseline);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoKnownAbnormal,
            result.BehaviorClassification);
    }

    [Fact]
    public async Task ResolveAsync_ApenasCandidatePersistido_NaoDeveReconhecerRuntime()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var history =
            CreateStableHistory(
                new DateTime(
                    2026,
                    10,
                    1,
                    13,
                    0,
                    0),
                20,
                40);

        await SeedBehaviorAsync(
            fixture.Context,
            history,
            status:
                TagComportamentoIa.StatusCandidate,
            classification:
                TagComportamentoIa.ClassificacaoNormal,
            latestVersion:
                1);

        var provider =
            CreateProvider(
                fixture.Context);

        var result =
            await provider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                history,
                history[^1].DataHora);

        Assert.False(
            result.Recognized);

        Assert.False(
            result.CanApplyBaseline);

        Assert.Null(
            result.BehaviorId);
    }

    [Fact]
    public async Task ResolveAsync_DeveIgnorarLeiturasFuturas()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var currentHistory =
            CreateStableHistory(
                new DateTime(
                    2026,
                    10,
                    1,
                    14,
                    0,
                    0),
                20,
                40);

        await SeedKnownBehaviorAsync(
            fixture.Context,
            currentHistory,
            TagComportamentoIa.ClassificacaoNormal,
            latestVersion:
                1);

        var futureHistory =
            CreateStableHistory(
                currentHistory[^1]
                    .DataHora
                    .AddMinutes(
                        1),
                20,
                400);

        var mixed =
            currentHistory
                .Concat(
                    futureHistory)
                .ToArray();

        var provider =
            CreateProvider(
                fixture.Context);

        var result =
            await provider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                mixed,
                currentHistory[^1].DataHora);

        Assert.True(
            result.Recognized);

        Assert.True(
            result.CanApplyBaseline);

        Assert.Equal(
            20,
            result.History.Count);

        Assert.All(
            result.History,
            point =>
                Assert.True(
                    point.DataHora <=
                    currentHistory[^1].DataHora));

        Assert.All(
            result.History,
            point =>
                Assert.InRange(
                    point.Valor,
                    39,
                    41));
    }

    [Fact]
    public async Task ResolveAsync_EpisodioAtualInsuficiente_DeveFalharSeguro()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var history =
            CreateStableHistory(
                new DateTime(
                    2026,
                    10,
                    1,
                    15,
                    0,
                    0),
                9,
                40);

        var provider =
            CreateProvider(
                fixture.Context);

        var result =
            await provider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                history,
                history[^1].DataHora);

        Assert.False(
            result.Recognized);

        Assert.False(
            result.CanApplyBaseline);

        Assert.Contains(
            "insuficiente",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    private static BehaviorRuntimeBaselineProvider
        CreateProvider(
            AppDbContext context)
    {
        var statistics =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var characterizer =
            new BehaviorCandidateEpisodeCharacterizer(
                statistics);

        var planBuilder =
            new BehaviorMemoryPersistencePlanBuilder();

        var reconciliation =
            new BehaviorMemoryReconciliationService(
                context,
                planBuilder);

        return new BehaviorRuntimeBaselineProvider(
            context,
            new BehaviorHistorySegmentationService(),
            new BehaviorChangePointDetector(),
            new BehaviorCandidateEpisodeBuilder(),
            characterizer,
            reconciliation);
    }

    private static async Task<TagComportamentoIa>
        SeedKnownBehaviorAsync(
            AppDbContext context,
            IReadOnlyList<LeituraBruta> history,
            string classification,
            int latestVersion)
    {
        return await SeedBehaviorAsync(
            context,
            history,
            TagComportamentoIa.StatusKnown,
            classification,
            latestVersion);
    }

    private static async Task<TagComportamentoIa>
        SeedBehaviorAsync(
            AppDbContext context,
            IReadOnlyList<LeituraBruta> history,
            string status,
            string classification,
            int latestVersion)
    {
        var behavior =
            new TagComportamentoIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Codigo =
                    status ==
                    TagComportamentoIa.StatusKnown
                        ? "BEH-001"
                        : "CAND-TESTE-RUNTIME",

                Status =
                    status,

                Origem =
                    TagComportamentoIa.OrigemAutomatic,

                Classificacao =
                    classification,

                PrimeiraObservacao =
                    history[0].DataHora,

                UltimaObservacao =
                    history[^1].DataHora,

                QuantidadeOcorrencias =
                    1,

                TotalAmostras =
                    history.Count,

                Confianca =
                    90,

                DataCriacao =
                    DateTime.UtcNow
            };

        context.TagsComportamentosIa.Add(
            behavior);

        await context.SaveChangesAsync();

        var snapshots =
            Enumerable
                .Range(
                    1,
                    latestVersion)
                .Select(version =>
                    BuildSnapshot(
                        history,
                        version))
                .ToArray();

        foreach (var snapshot in snapshots)
        {
            snapshot.TagComportamentoIaId =
                behavior.Id;
        }

        /*
         * A versão anterior é propositalmente incompatível.
         * O provider/reconciliador deve usar o snapshot mais recente.
         */
        if (snapshots.Length > 1)
        {
            snapshots[0].Media +=
                1000;
        }

        context.TagsComportamentosSnapshotsIa
            .AddRange(
                snapshots);

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        return behavior;
    }

    private static TagComportamentoSnapshotIa
        BuildSnapshot(
            IReadOnlyList<LeituraBruta> history,
            int version)
    {
        var statistics =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var characterizer =
            new BehaviorCandidateEpisodeCharacterizer(
                statistics);

        var episode =
            new BehaviorCandidateEpisode
            {
                SourceSegmentIndex =
                    0,

                EpisodeIndex =
                    0,

                StartReadingIndex =
                    0,

                EndReadingIndex =
                    history.Count - 1,

                Inicio =
                    history[0].DataHora,

                Fim =
                    history[^1].DataHora,

                Historico =
                    history.ToArray()
            };

        var characteristics =
            characterizer.Characterize(
                episode);

        Assert.True(
            characteristics.IsCharacterizable);

        var builder =
            new BehaviorMemoryPersistencePlanBuilder();

        return builder.BuildSnapshotPreview(
            [
                characteristics
            ],
            version);
    }

    private static LeituraBruta[]
        CreateStableHistory(
            DateTime start,
            int count,
            double center)
    {
        var offsets =
            new[]
            {
                0.00,
                0.12,
                -0.08,
                0.18,
                -0.14,
                0.05,
                0.16,
                -0.11,
                0.09,
                -0.04
            };

        return Enumerable
            .Range(
                0,
                count)
            .Select(index =>
                new LeituraBruta
                {
                    DataHora =
                        start.AddMinutes(
                            index),

                    Valor =
                        Convert.ToSingle(
                            center +
                            offsets[
                                index %
                                offsets.Length])
                })
            .ToArray();
    }

    private sealed class DatabaseFixture :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private DatabaseFixture(
            SqliteConnection connection,
            AppDbContext context)
        {
            _connection =
                connection;

            Context =
                context;
        }

        public AppDbContext Context { get; }

        public static async Task<DatabaseFixture>
            CreateAsync()
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

            await _connection.DisposeAsync();
        }
    }
}
