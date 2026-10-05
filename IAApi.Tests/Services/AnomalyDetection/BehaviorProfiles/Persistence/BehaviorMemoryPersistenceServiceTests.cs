using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public sealed class BehaviorMemoryPersistenceServiceTests
{
    [Fact]
    public async Task PersistAsync_NovoComportamento_DeveSalvarTodaMemoria()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryPersistenceService(
                fixture.Context);

        var plan =
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                3);

        var result =
            await service.PersistAsync(
                plan);

        Assert.True(
            result.CreatedBehavior);

        Assert.Equal(
            3,
            result.AddedOccurrences);

        Assert.True(
            result.AddedSnapshot);

        Assert.Equal(
            1,
            result.SnapshotVersion);

        var behavior =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            3,
            behavior.QuantidadeOcorrencias);

        Assert.Equal(
            60,
            behavior.TotalAmostras);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            behavior.Classificacao);

        Assert.Equal(
            3,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        var snapshot =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .SingleAsync();

        Assert.Equal(
            behavior.Id,
            snapshot.TagComportamentoIaId);

        Assert.Equal(
            1,
            snapshot.Versao);
    }

    [Fact]
    public async Task PersistAsync_SnapshotRico_DevePreservarTodasAsDimensoes()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryPersistenceService(
                fixture.Context);

        var plan =
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                3);

        plan.Snapshot.Mediana =
            40.15;

        plan.Snapshot.PrimeiroQuartil =
            39.8;

        plan.Snapshot.TerceiroQuartil =
            40.5;

        plan.Snapshot.IntervaloInterquartil =
            0.7;

        plan.Snapshot.RobustScale =
            0.62;

        plan.Snapshot.TypicalAbsoluteStep =
            0.31;

        await service.PersistAsync(
            plan);

        fixture.Context.ChangeTracker.Clear();

        var snapshot =
            await fixture.Context
                .TagsComportamentosSnapshotsIa
                .SingleAsync();

        Assert.Equal(
            40.15,
            snapshot.Mediana);

        Assert.Equal(
            39.8,
            snapshot.PrimeiroQuartil);

        Assert.Equal(
            40.5,
            snapshot.TerceiroQuartil);

        Assert.Equal(
            0.7,
            snapshot.IntervaloInterquartil);

        Assert.Equal(
            0.62,
            snapshot.RobustScale);

        Assert.Equal(
            0.31,
            snapshot.TypicalAbsoluteStep);
    }

    [Fact]
    public async Task PersistAsync_MesmoPlanoDuasVezes_NaoDeveDuplicarDados()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryPersistenceService(
                fixture.Context);

        var plan =
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                3);

        await service.PersistAsync(
            plan);

        var second =
            await service.PersistAsync(
                plan);

        Assert.False(
            second.CreatedBehavior);

        Assert.Equal(
            0,
            second.AddedOccurrences);

        Assert.False(
            second.AddedSnapshot);

        Assert.Single(
            await fixture.Context.TagsComportamentosIa
                .ToListAsync());

        Assert.Equal(
            3,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Single(
            await fixture.Context.TagsComportamentosSnapshotsIa
                .ToListAsync());
    }

    [Fact]
    public async Task PersistAsync_NovaOcorrencia_DeveCriarNovaVersaoDoSnapshot()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryPersistenceService(
                fixture.Context);

        await service.PersistAsync(
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                3));

        var expandedPlan =
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                4);

        var result =
            await service.PersistAsync(
                expandedPlan);

        Assert.False(
            result.CreatedBehavior);

        Assert.Equal(
            1,
            result.AddedOccurrences);

        Assert.True(
            result.AddedSnapshot);

        Assert.Equal(
            2,
            result.SnapshotVersion);

        var behavior =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            4,
            behavior.QuantidadeOcorrencias);

        Assert.Equal(
            80,
            behavior.TotalAmostras);

        var versions =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .OrderBy(x =>
                    x.Versao)
                .Select(x =>
                    x.Versao)
                .ToArrayAsync();

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            versions);
    }

    [Fact]
    public async Task PersistAsync_DuasExecucoesMesmaOcorrencia_DeveConvergirSemDuplicar()
    {
        await using var fixture =
            await ConcurrentDatabaseFixture.CreateAsync();

        /*
         * Baseline: BEH-001 com uma ocorrência e snapshot v1.
         */
        await using (var seedContext =
                     fixture.CreateContext())
        {
            var seedService =
                new BehaviorMemoryPersistenceService(
                    seedContext);

            await seedService.PersistAsync(
                CreatePlan(
                    "CLIENTE-A",
                    "TAG1",
                    "BEH-001",
                    1));
        }

        /*
         * SQLite permite apenas uma transação de escrita por vez.
         *
         * Portanto o teste correto não tenta obrigar dois writers a entrar
         * simultaneamente no SaveChanges. Em vez disso:
         *
         * 1. A abre a transação, detecta a nova ocorrência e chega ao
         *    primeiro SaveChanges;
         * 2. o interceptor segura A por alguns milissegundos;
         * 3. B começa enquanto A ainda possui o lock;
         * 4. Microsoft.Data.Sqlite espera/reexecuta a aquisição do lock;
         * 5. depois do commit de A, B entra, relê o banco e converge para
         *    "ocorrência já existente".
         */
        var delayInterceptor =
            new FirstSaveDelayInterceptor(
                TimeSpan.FromMilliseconds(
                    500));

        await using var contextA =
            fixture.CreateContext(
                delayInterceptor);

        await using var contextB =
            fixture.CreateContext();

        var serviceA =
            new BehaviorMemoryPersistenceService(
                contextA);

        var serviceB =
            new BehaviorMemoryPersistenceService(
                contextB);

        var planA =
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                2);

        var planB =
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                2);

        var attemptA =
            serviceA.PersistAsync(
                planA);

        /*
         * Só iniciamos B depois que A já abriu a transação e chegou ao
         * primeiro SaveChanges. Assim sabemos que B realmente encontra
         * o banco temporariamente bloqueado por outra escrita.
         */
        await delayInterceptor.FirstSaveReached
            .WaitAsync(
                TimeSpan.FromSeconds(
                    5));

        var attemptB =
            serviceB.PersistAsync(
                planB);

        var results =
            await Task.WhenAll(
                attemptA,
                attemptB);

        await using var verificationContext =
            fixture.CreateContext();

        var behavior =
            await verificationContext
                .TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            2,
            behavior.QuantidadeOcorrencias);

        Assert.Equal(
            40,
            behavior.TotalAmostras);

        Assert.Equal(
            2,
            await verificationContext
                .TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Equal(
            2,
            await verificationContext
                .TagsComportamentosSnapshotsIa
                .CountAsync());

        var versions =
            await verificationContext
                .TagsComportamentosSnapshotsIa
                .OrderBy(x =>
                    x.Versao)
                .Select(x =>
                    x.Versao)
                .ToArrayAsync();

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            versions);

        /*
         * A efetivamente incorpora a evidência nova.
         * B entra depois do commit e deve tratá-la como já persistida.
         */
        Assert.Equal(
            1,
            results.Count(x =>
                x.AddedOccurrences == 1));

        Assert.Equal(
            1,
            results.Count(x =>
                x.AddedOccurrences == 0));

        Assert.Equal(
            1,
            results.Count(x =>
                x.AddedSnapshot));

        Assert.Equal(
            1,
            results.Count(x =>
                !x.AddedSnapshot));

        Assert.Contains(
            results,
            x =>
                x.SnapshotVersion == 2);

        Assert.Contains(
            results,
            x =>
                x.SnapshotVersion is null);
    }

    [Fact]
    public async Task PersistAsync_ComportamentoExistente_NaoDeveSobrescreverClassificacaoDoCliente()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryPersistenceService(
                fixture.Context);

        await service.PersistAsync(
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                3));

        var behavior =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        behavior.Classificacao =
            TagComportamentoIa.ClassificacaoNormal;

        behavior.Origem =
            TagComportamentoIa.OrigemManual;

        await fixture.Context.SaveChangesAsync();

        await service.PersistAsync(
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                4));

        fixture.Context.ChangeTracker.Clear();

        var reloaded =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            reloaded.Classificacao);

        Assert.Equal(
            TagComportamentoIa.OrigemManual,
            reloaded.Origem);
    }

    [Fact]
    public async Task PersistAsync_MesmoCodigoEmOutraTag_DevePermanecerIsolado()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryPersistenceService(
                fixture.Context);

        await service.PersistAsync(
            CreatePlan(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                3));

        await service.PersistAsync(
            CreatePlan(
                "CLIENTE-A",
                "TAG2",
                "BEH-001",
                3));

        var behaviors =
            await fixture.Context.TagsComportamentosIa
                .OrderBy(x =>
                    x.TagName)
                .ToArrayAsync();

        Assert.Equal(
            2,
            behaviors.Length);

        Assert.Equal(
            "TAG1",
            behaviors[0].TagName);

        Assert.Equal(
            "TAG2",
            behaviors[1].TagName);

        Assert.All(
            behaviors,
            x =>
                Assert.Equal(
                    "BEH-001",
                    x.Codigo));
    }

    private static BehaviorMemoryPersistencePlan CreatePlan(
        string clienteId,
        string tagName,
        string code,
        int occurrenceCount)
    {
        var start =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var occurrences =
            Enumerable.Range(
                    0,
                    occurrenceCount)
                .Select(index =>
                    new TagComportamentoOcorrenciaIa
                    {
                        DataHoraInicio =
                            start.AddHours(
                                index * 2),

                        DataHoraFim =
                            start
                                .AddHours(
                                    index * 2)
                                .AddMinutes(
                                    19),

                        QuantidadeAmostras =
                            20,

                        Confianca =
                            80 - index,

                        UtilizadaNoAprendizado =
                            true,

                        DataCriacao =
                            new DateTime(
                                2026,
                                10,
                                1,
                                12,
                                0,
                                0,
                                DateTimeKind.Utc)
                    })
                .ToArray();

        return new BehaviorMemoryPersistencePlan
        {
            Behavior =
                new TagComportamentoIa
                {
                    ClienteId =
                        clienteId,

                    TagName =
                        tagName,

                    Codigo =
                        code,

                    Status =
                        TagComportamentoIa.StatusKnown,

                    Origem =
                        TagComportamentoIa.OrigemAutomatic,

                    Classificacao =
                        TagComportamentoIa.ClassificacaoUndefined,

                    PrimeiraObservacao =
                        occurrences.Min(x =>
                            x.DataHoraInicio),

                    UltimaObservacao =
                        occurrences.Max(x =>
                            x.DataHoraFim),

                    QuantidadeOcorrencias =
                        occurrenceCount,

                    TotalAmostras =
                        occurrenceCount * 20L,

                    Confianca =
                        75,

                    DataCriacao =
                        new DateTime(
                            2026,
                            10,
                            1,
                            12,
                            0,
                            0,
                            DateTimeKind.Utc)
                },

            Occurrences =
                occurrences,

            Snapshot =
                new TagComportamentoSnapshotIa
                {
                    Versao =
                        1,

                    Media =
                        40.2,

                    DesvioPadrao =
                        0.7,

                    Minimo =
                        39,

                    Maximo =
                        41,

                    Amplitude =
                        2,

                    PercentualZeros =
                        0,

                    VariacaoMedia =
                        0.25,

                    QuantidadePicos =
                        0,

                    TotalRegistrosHistorico =
                        occurrenceCount * 20,

                    TotalRegistrosUsados =
                        occurrenceCount * 20,

                    TotalOutliersRemovidos =
                        0,

                    DeadbandRecomendado =
                        0.5,

                    ConfiancaDeadband =
                        90,

                    JustificativaDeadband =
                        "Snapshot de teste.",

                    PeriodoInicio =
                        occurrences.Min(x =>
                            x.DataHoraInicio),

                    PeriodoFim =
                        occurrences.Max(x =>
                            x.DataHoraFim),

                    DataCriacao =
                        new DateTime(
                            2026,
                            10,
                            1,
                            12,
                            0,
                            0,
                            DateTimeKind.Utc)
                }
        };
    }

    /// <summary>
    /// Segura apenas o primeiro SaveChanges do contexto A.
    ///
    /// O sinal FirstSaveReached permite iniciar a segunda persistência
    /// somente depois que A já está dentro da sua transação.
    /// </summary>
    private sealed class FirstSaveDelayInterceptor :
        SaveChangesInterceptor
    {
        private readonly TimeSpan _delay;

        private readonly TaskCompletionSource<bool>
            _firstSaveReached =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        private int _hasDelayed;

        public FirstSaveDelayInterceptor(
            TimeSpan delay)
        {
            _delay =
                delay;
        }

        public Task FirstSaveReached =>
            _firstSaveReached.Task;

        public override async ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(
                    ref _hasDelayed,
                    1) == 0)
            {
                _firstSaveReached
                    .TrySetResult(
                        true);

                await Task.Delay(
                    _delay,
                    cancellationToken);
            }

            return await base.SavingChangesAsync(
                eventData,
                result,
                cancellationToken);
        }
    }

    private sealed class ConcurrentDatabaseFixture :
        IAsyncDisposable
    {
        private readonly string _databasePath;

        private readonly string _connectionString;

        private ConcurrentDatabaseFixture(
            string databasePath)
        {
            _databasePath =
                databasePath;

            _connectionString =
                $"Data Source={databasePath};Default Timeout=30";
        }

        public static async Task<ConcurrentDatabaseFixture>
            CreateAsync()
        {
            var databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"iaapi-b325-concurrency-{Guid.NewGuid():N}.db");

            var fixture =
                new ConcurrentDatabaseFixture(
                    databasePath);

            await using var context =
                fixture.CreateContext();

            await context.Database
                .EnsureCreatedAsync();

            return fixture;
        }

        public AppDbContext CreateContext(
            SaveChangesInterceptor? interceptor = null)
        {
            var builder =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(
                        _connectionString);

            if (interceptor is not null)
            {
                builder.AddInterceptors(
                    interceptor);
            }

            return new AppDbContext(
                builder.Options);
        }

        public ValueTask DisposeAsync()
        {
            TryDelete(
                _databasePath);

            TryDelete(
                _databasePath +
                "-wal");

            TryDelete(
                _databasePath +
                "-shm");

            return ValueTask.CompletedTask;
        }

        private static void TryDelete(
            string path)
        {
            try
            {
                if (File.Exists(
                        path))
                {
                    File.Delete(
                        path);
                }
            }
            catch
            {
                /*
                 * Limpeza de artefato temporário não interfere
                 * no resultado funcional do teste.
                 */
            }
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
