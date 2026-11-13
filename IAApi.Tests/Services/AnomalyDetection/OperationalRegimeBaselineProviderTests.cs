using IAApi.Services.AnomalyDetection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OperationalRegimeBaselineProviderTests
{
    [Fact]
    public async Task Resolve_WithActiveConfiguration_UsesExpectedRegimeWindowAndBuildsBaseline()
    {
        await using var connection =
            new SqliteConnection(
                "DataSource=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.TagsRegimesOperacionaisIa.Add(
            new TagRegimeOperacionalIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "PROCESS.INLET",

                TagRegime =
                    "PROCESS.PUMP_RUNNING",

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

        var targetHistory =
            CreateTargetHistory();

        var regimeHistory =
            CreateRegimeHistory();

        var historicoService =
            new FakeHistoricoService
            {
                PeriodHistory =
                    regimeHistory
            };

        var provider =
            CreateProvider(
                context,
                historicoService);

        var currentTimestamp =
            Utc(0, 11);

        var result =
            provider.Resolve(
                "CLIENTE-A",
                "PROCESS.INLET",
                targetHistory,
                currentTimestamp);

        Assert.True(
            result.Applied,
            result.Reason);

        Assert.NotNull(
            result.Statistics);

        Assert.Equal(
            10,
            result.SelectedSamples);

        Assert.Equal(
            10,
            result.History.Count);

        Assert.Equal(
            0,
            result.CurrentRegimeValue!.Value,
            3);

        /*
         * Primeiro target: 00:01
         *
         * 00:01 - Lag 1 min - tolerância 2 min
         * = 23:58 do dia anterior.
         */
        Assert.Equal(
            new DateTime(
                2026,
                9,
                10,
                23,
                58,
                0,
                DateTimeKind.Utc),
            historicoService.LastInicio);

        /*
         * Current timestamp: 00:11
         *
         * 00:11 - Lag 1 min
         * = 00:10.
         */
        Assert.Equal(
            Utc(0, 10),
            historicoService.LastFim);

        Assert.Equal(
            "CLIENTE-A",
            historicoService.LastClienteId);

        Assert.Equal(
            "PROCESS.PUMP_RUNNING",
            historicoService.LastTagName);

        Assert.Equal(
            1,
            historicoService.PeriodCallCount);

        Assert.InRange(
            result.Statistics!.Media,
            0.02,
            0.04);
    }

    [Fact]
    public async Task Resolve_WithoutConfiguration_FallsBackWithoutQueryingRegimeHistory()
    {
        await using var connection =
            new SqliteConnection(
                "DataSource=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        var historicoService =
            new FakeHistoricoService();

        var provider =
            CreateProvider(
                context,
                historicoService);

        var result =
            provider.Resolve(
                "CLIENTE-A",
                "PROCESS.INLET",
                CreateTargetHistory(),
                Utc(0, 11));

        Assert.False(
            result.Applied);

        Assert.Null(
            result.Statistics);

        Assert.Empty(
            result.History);

        Assert.Equal(
            0,
            historicoService.PeriodCallCount);

        Assert.Contains(
            "não possui configuração",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_WithDisabledConfiguration_FallsBackWithoutQueryingRegimeHistory()
    {
        await using var connection =
            new SqliteConnection(
                "DataSource=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.TagsRegimesOperacionaisIa.Add(
            new TagRegimeOperacionalIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "PROCESS.INLET",

                TagRegime =
                    "PROCESS.PUMP_RUNNING",

                Ativo =
                    false,

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

        var historicoService =
            new FakeHistoricoService();

        var provider =
            CreateProvider(
                context,
                historicoService);

        var result =
            provider.Resolve(
                "CLIENTE-A",
                "PROCESS.INLET",
                CreateTargetHistory(),
                Utc(0, 11));

        Assert.False(
            result.Applied);

        Assert.Equal(
            0,
            historicoService.PeriodCallCount);

        Assert.Contains(
            "desativada",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_WhenRegimeHistoryFails_FallsBackInsteadOfThrowing()
    {
        await using var connection =
            new SqliteConnection(
                "DataSource=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.TagsRegimesOperacionaisIa.Add(
            new TagRegimeOperacionalIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "PROCESS.INLET",

                TagRegime =
                    "PROCESS.PUMP_RUNNING",

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

        var historicoService =
            new FakeHistoricoService
            {
                ExceptionToThrow =
                    new InvalidOperationException(
                        "Bridge indisponível para teste.")
            };

        var provider =
            CreateProvider(
                context,
                historicoService);

        var result =
            provider.Resolve(
                "CLIENTE-A",
                "PROCESS.INLET",
                CreateTargetHistory(),
                Utc(0, 11));

        Assert.False(
            result.Applied);

        Assert.Null(
            result.Statistics);

        Assert.Empty(
            result.History);

        Assert.Equal(
            1,
            historicoService.PeriodCallCount);

        Assert.Contains(
            "Fallback para baseline global",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "Bridge indisponível",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_WithInvalidTargetHistory_FallsBackWithoutQueryingRegimeHistory()
    {
        await using var connection =
            new SqliteConnection(
                "DataSource=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.TagsRegimesOperacionaisIa.Add(
            new TagRegimeOperacionalIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "PROCESS.INLET",

                TagRegime =
                    "PROCESS.PUMP_RUNNING",

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

        var historicoService =
            new FakeHistoricoService();

        var provider =
            CreateProvider(
                context,
                historicoService);

        var invalidHistory =
            new List<LeituraBruta>
            {
                new()
                {
                    DataHora =
                        default,

                    Valor =
                        1
                },

                new()
                {
                    DataHora =
                        Utc(0, 1),

                    Valor =
                        float.NaN
                }
            };

        var result =
            provider.Resolve(
                "CLIENTE-A",
                "PROCESS.INLET",
                invalidHistory,
                Utc(0, 11));

        Assert.False(
            result.Applied);

        Assert.Equal(
            0,
            historicoService.PeriodCallCount);

        Assert.Contains(
            "não possui leituras válidas",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    // ===== RC-7: tags proibidas como regime, por cliente =====

    private static OperationalRegimeForbiddenTagsOptions Pk2LabelList() =>
        new()
        {
            TagsProibidasPorCliente =
                new Dictionary<string, List<string>>
                {
                    ["PK2"] =
                        Enumerable.Range(60, 22)
                            .Select(n => $"TAG{n}")
                            .ToList()
                }
        };

    private static async Task<(SqliteConnection Connection, AppDbContext Context)> CreateSeededContextAsync(
        string clienteId,
        string tagRegime)
    {
        var connection =
            new SqliteConnection(
                "DataSource=:memory:");

        await connection.OpenAsync();

        var context =
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options);

        await context.Database.EnsureCreatedAsync();

        context.TagsRegimesOperacionaisIa.Add(
            new TagRegimeOperacionalIa
            {
                ClienteId = clienteId,
                TagName = "PROCESS.INLET",
                TagRegime = tagRegime,
                Ativo = true,
                LagMinutos = 1,
                ToleranciaAlinhamentoMinutos = 2,
                ToleranciaEstado = 0.001,
                MinimoAmostrasPorRegime = 10
            });

        await context.SaveChangesAsync();

        return (connection, context);
    }

    [Fact]
    public async Task Resolve_TagRegimeNaListaDoCliente_NaoAplicaSemConsultarHistorico()
    {
        var (connection, context) =
            await CreateSeededContextAsync("PK2", "TAG73");

        await using var _connection = connection;
        await using var _context = context;

        var historicoService =
            new FakeHistoricoService
            {
                PeriodHistory = CreateRegimeHistory()
            };

        var provider =
            CreateProvider(
                context,
                historicoService,
                Pk2LabelList());

        var result =
            provider.Resolve(
                "PK2",
                "PROCESS.INLET",
                CreateTargetHistory(),
                Utc(0, 11));

        Assert.False(result.Applied);
        Assert.Null(result.Statistics);
        Assert.Equal(0, historicoService.PeriodCallCount);
        Assert.Contains("TAG73", result.Reason);
        Assert.Contains("proibidas como regime", result.Reason);
        Assert.Contains("PK2", result.Reason);
    }

    [Theory]
    [InlineData("PK2", "PROCESS.PUMP_RUNNING", true)]   // tag válida, lista cadastrada
    [InlineData("PK2", "TAG73", false)]                 // lista vazia
    [InlineData("CLIENTE-A", "TAG73", true)]            // lista só do PK2
    public async Task Resolve_SemBloqueio_ResultadoIdenticoAoSemLista(
        string clienteId,
        string tagRegime,
        bool comListaPk2)
    {
        var (connection, context) =
            await CreateSeededContextAsync(clienteId, tagRegime);

        await using var _connection = connection;
        await using var _context = context;

        var historicoReferencia =
            new FakeHistoricoService { PeriodHistory = CreateRegimeHistory() };

        var historicoComLista =
            new FakeHistoricoService { PeriodHistory = CreateRegimeHistory() };

        var referencia =
            CreateProvider(context, historicoReferencia)
                .Resolve(clienteId, "PROCESS.INLET", CreateTargetHistory(), Utc(0, 11));

        var comLista =
            CreateProvider(
                    context,
                    historicoComLista,
                    comListaPk2 ? Pk2LabelList() : new OperationalRegimeForbiddenTagsOptions())
                .Resolve(clienteId, "PROCESS.INLET", CreateTargetHistory(), Utc(0, 11));

        Assert.True(referencia.Applied, referencia.Reason);
        Assert.Equal(referencia.Applied, comLista.Applied);
        Assert.Equal(referencia.Reason, comLista.Reason);
        Assert.Equal(referencia.SelectedSamples, comLista.SelectedSamples);
        Assert.Equal(referencia.CurrentRegimeValue, comLista.CurrentRegimeValue);
        Assert.Equal(referencia.Statistics!.Media, comLista.Statistics!.Media);
        Assert.Equal(referencia.Statistics.DesvioPadrao, comLista.Statistics.DesvioPadrao);
        Assert.Equal(referencia.Statistics.DeadbandRecomendado, comLista.Statistics.DeadbandRecomendado);
        Assert.Equal(
            referencia.History.Select(r => (r.DataHora, r.Valor)),
            comLista.History.Select(r => (r.DataHora, r.Valor)));
        Assert.Equal(historicoReferencia.PeriodCallCount, historicoComLista.PeriodCallCount);
        Assert.Equal(historicoReferencia.LastTagName, historicoComLista.LastTagName);
        Assert.Equal(historicoReferencia.LastInicio, historicoComLista.LastInicio);
        Assert.Equal(historicoReferencia.LastFim, historicoComLista.LastFim);
    }

    [Theory]
    [InlineData("PK2", "TAG73", true)]
    [InlineData("pk2", " tag73 ", true)]
    [InlineData("PK2", "TAG60", true)]
    [InlineData("PK2", "TAG81", true)]
    [InlineData("PK2", "TAG59", false)]
    [InlineData("PK2", "TAG82", false)]
    [InlineData("PK2", "PK2.TAG73", false)]
    [InlineData("OUTRO", "TAG73", false)]
    [InlineData("", "TAG73", false)]
    [InlineData("PK2", "", false)]
    public void ForbiddenTagsOptions_IsForbidden_PorClienteSemDiferenciarCaixa(
        string clienteId,
        string tagName,
        bool esperado)
    {
        Assert.Equal(
            esperado,
            Pk2LabelList().IsForbidden(clienteId, tagName));
    }

    [Fact]
    public void ForbiddenTagsOptions_PadraoVazio_NadaProibido()
    {
        var options = new OperationalRegimeForbiddenTagsOptions();

        Assert.Empty(options.TagsProibidasPorCliente);
        Assert.False(options.IsForbidden("PK2", "TAG73"));
    }

    [Fact]
    public void ForbiddenTagsOptions_LidaDaSecaoDeConfiguracao()
    {
        var configuration =
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente:PK2:0"] = "TAG60",
                        ["AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente:PK2:1"] = "TAG73"
                    })
                .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        IAApi.Services.AnomalyDetection.DependencyInjection
            .AnomalyDetectionServiceCollectionExtensions
            .AddAnomalyDetectionConfiguration(services, configuration);

        using var provider =
            Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(services);

        var options =
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<OperationalRegimeForbiddenTagsOptions>>(provider)
                .Value;

        Assert.True(options.IsForbidden("PK2", "TAG73"));
        Assert.False(options.IsForbidden("PK2", "TAG74"));
        Assert.False(options.IsForbidden("CLIENTE-A", "TAG73"));
    }

    [Fact]
    public void ForbiddenTagsOptions_SemSecao_ListaVazia()
    {
        var configuration =
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        IAApi.Services.AnomalyDetection.DependencyInjection
            .AnomalyDetectionServiceCollectionExtensions
            .AddAnomalyDetectionConfiguration(services, configuration);

        using var provider =
            Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(services);

        var options =
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<OperationalRegimeForbiddenTagsOptions>>(provider)
                .Value;

        Assert.Empty(options.TagsProibidasPorCliente);
    }

    private static OperationalRegimeBaselineProvider CreateProvider(
        AppDbContext context,
        IHistoricoService historicoService,
        OperationalRegimeForbiddenTagsOptions forbiddenTags)
    {
        return new OperationalRegimeBaselineProvider(
            context,
            historicoService,
            new OperationalRegimeBaselineService(
                new OperationalRegimeHistoryResolver(),
                new TagProfileStatisticsCalculator(
                    new HistoricoLimpezaService())),
            Microsoft.Extensions.Options.Options.Create(forbiddenTags));
    }

    private static OperationalRegimeBaselineProvider CreateProvider(
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

    private static List<LeituraBruta> CreateTargetHistory()
    {
        var values =
            new float[]
            {
                0.02f,
                0.03f,
                0.04f,
                0.02f,
                0.03f,
                0.04f,
                0.02f,
                0.03f,
                0.04f,
                0.03f
            };

        var history =
            new List<LeituraBruta>();

        for (var index = 0;
             index < values.Length;
             index++)
        {
            history.Add(
                new LeituraBruta
                {
                    /*
                     * Targets: 00:01 .. 00:10
                     */
                    DataHora =
                        Utc(
                            0,
                            index + 1),

                    Valor =
                        values[index]
                });
        }

        return history;
    }

    private static List<LeituraBruta> CreateRegimeHistory()
    {
        var history =
            new List<LeituraBruta>();

        /*
         * Regime disponível de 00:00 até 00:10.
         *
         * Todos os pontos representam PUMP OFF.
         */
        for (var minute = 0;
             minute <= 10;
             minute++)
        {
            history.Add(
                new LeituraBruta
                {
                    DataHora =
                        Utc(
                            0,
                            minute),

                    Valor =
                        0
                });
        }

        return history;
    }

    private static DateTime Utc(
        int hour,
        int minute)
    {
        return new DateTime(
            2026,
            9,
            11,
            hour,
            minute,
            0,
            DateTimeKind.Utc);
    }

    private sealed class FakeHistoricoService :
        IHistoricoService
    {
        public List<LeituraBruta> PeriodHistory { get; set; } =
            new();

        public Exception? ExceptionToThrow { get; set; }

        public int PeriodCallCount { get; private set; }

        public string? LastClienteId { get; private set; }

        public string? LastTagName { get; private set; }

        public DateTime? LastInicio { get; private set; }

        public DateTime? LastFim { get; private set; }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            throw new NotSupportedException(
                "Este teste utiliza somente BuscarHistoricoPorPeriodo.");
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            PeriodCallCount++;

            LastClienteId =
                clienteId;

            LastTagName =
                tagName;

            LastInicio =
                inicio;

            LastFim =
                fim;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return PeriodHistory
                .Where(
                    item =>
                        item.DataHora >= inicio &&
                        item.DataHora <= fim)
                .OrderBy(
                    item =>
                        item.DataHora)
                .ToList();
        }
    }
}