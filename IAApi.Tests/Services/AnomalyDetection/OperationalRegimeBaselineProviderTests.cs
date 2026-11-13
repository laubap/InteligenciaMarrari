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

    // ===== RC-8: regime combinado (componentes) atrás da chave =====
    //
    // B-01 (TAG29) com 3 componentes, como a configuração alvo de §6.2:
    // TAG42 (própria bomba, mapa 2:2;*:0), TAG43 (outra bomba, 2:2;*:0) e
    // TAG46 (set-point, 380:380;*:450). Lag 1, tolerância 2, mínimo 10.
    // Targets 00:01..00:30: ~100 até 00:15 e ~50 depois.
    // TAG42 = 2 sempre; TAG43 = 2 até 00:14 e 0 depois; TAG46 = 450.
    // Estado em 00:30 (leitura às 00:31): (2, 0, 450) ⇒ targets 00:16..00:30.
    // O caminho atual (só TAG42) pega os 30 targets.

    private static readonly DateTime JanelaInicioRc8 =
        new(2026, 9, 10, 23, 58, 0, DateTimeKind.Utc);

    private static List<LeituraBruta> TargetRc8() =>
        Enumerable.Range(1, 30)
            .Select(minute => new LeituraBruta
            {
                DataHora = Utc(0, minute),
                Valor = (minute <= 15 ? 100f : 50f) + (minute % 3) * 0.5f
            })
            .ToList();

    private static List<LeituraBruta> SerieRc8(Func<int, float> valor) =>
        Enumerable.Range(0, 31)
            .Select(minute => new LeituraBruta
            {
                DataHora = Utc(0, minute),
                Valor = valor(minute)
            })
            .ToList();

    private static FakeHistoricoService HistoricoRc8()
    {
        var historico = new FakeHistoricoService();
        historico.PeriodHistoryByTag["TAG42"] = SerieRc8(_ => 2f);
        historico.PeriodHistoryByTag["TAG43"] = SerieRc8(minute => minute <= 14 ? 2f : 0f);
        historico.PeriodHistoryByTag["TAG46"] = SerieRc8(_ => 450f);
        return historico;
    }

    private static async Task<(SqliteConnection Connection, AppDbContext Context)> SeedRc8Async(
        bool comComponentes,
        string? mapaComponente1 = "2:2;*:0",
        string tagComponente3 = "TAG46")
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context =
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection)
                    .Options);

        await context.Database.EnsureCreatedAsync();

        var configuration =
            new TagRegimeOperacionalIa
            {
                ClienteId = "PK2",
                TagName = "TAG29",
                TagRegime = "TAG42",
                MapaEstados = mapaComponente1,
                Ativo = true,
                LagMinutos = 1,
                ToleranciaAlinhamentoMinutos = 2,
                ToleranciaEstado = 0.001,
                MinimoAmostrasPorRegime = 10
            };

        context.TagsRegimesOperacionaisIa.Add(configuration);
        await context.SaveChangesAsync();

        if (comComponentes)
        {
            context.TagsRegimesComponentesIa.AddRange(
                // Fora de ordem de propósito: o provider ordena por Ordem.
                new TagRegimeComponenteIa
                {
                    ConfiguracaoId = configuration.Id,
                    Ordem = 3,
                    TagRegime = tagComponente3,
                    MapaEstados = "380:380;*:450"
                },
                new TagRegimeComponenteIa
                {
                    ConfiguracaoId = configuration.Id,
                    Ordem = 2,
                    TagRegime = "TAG43",
                    MapaEstados = "2:2;*:0"
                });

            await context.SaveChangesAsync();
        }

        context.ChangeTracker.Clear();

        return (connection, context);
    }

    private static OperationalRegimeBaselineProvider CreateProviderRc8(
        AppDbContext context,
        IHistoricoService historicoService,
        Microsoft.Extensions.Options.IOptions<RegimeCombinadoOptions>? regimeCombinado,
        OperationalRegimeForbiddenTagsOptions? forbiddenTags = null)
    {
        return new OperationalRegimeBaselineProvider(
            context,
            historicoService,
            new OperationalRegimeBaselineService(
                new OperationalRegimeHistoryResolver(),
                new TagProfileStatisticsCalculator(
                    new HistoricoLimpezaService())),
            forbiddenTags is null
                ? null
                : Microsoft.Extensions.Options.Options.Create(forbiddenTags),
            regimeCombinado);
    }

    private static Microsoft.Extensions.Options.IOptions<RegimeCombinadoOptions> Chave(bool habilitado) =>
        Microsoft.Extensions.Options.Options.Create(
            new RegimeCombinadoOptions { Habilitado = habilitado });

    private static void AssertMesmoResultado(
        OperationalRegimeBaselineResolution esperado,
        OperationalRegimeBaselineResolution atual)
    {
        Assert.Equal(esperado.Applied, atual.Applied);
        Assert.Equal(esperado.Reason, atual.Reason);
        Assert.Equal(esperado.CurrentRegimeValue, atual.CurrentRegimeValue);
        Assert.Equal(esperado.CurrentRegimeKey, atual.CurrentRegimeKey);
        Assert.Equal(esperado.OriginalTargetSamples, atual.OriginalTargetSamples);
        Assert.Equal(esperado.AlignedSamples, atual.AlignedSamples);
        Assert.Equal(esperado.SelectedSamples, atual.SelectedSamples);
        Assert.Equal(esperado.Statistics?.Media, atual.Statistics?.Media);
        Assert.Equal(esperado.Statistics?.DesvioPadrao, atual.Statistics?.DesvioPadrao);
        Assert.Equal(esperado.Statistics?.DeadbandRecomendado, atual.Statistics?.DeadbandRecomendado);
        Assert.Equal(esperado.Statistics?.ConfiancaDeadband, atual.Statistics?.ConfiancaDeadband);
        Assert.Equal(
            esperado.History.Select(p => (p.DataHora, p.Valor)),
            atual.History.Select(p => (p.DataHora, p.Valor)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task RC8_ChaveDesligada_ComComponentesEMapa_IdenticoASemComponentes_SemConsultaExtra(
        bool? habilitado)
    {
        var (c1, comComponentes) = await SeedRc8Async(comComponentes: true);
        var (c2, semComponentes) = await SeedRc8Async(comComponentes: false, mapaComponente1: null);
        await using var _c1 = c1;
        await using var _c2 = c2;
        await using var _x1 = comComponentes;
        await using var _x2 = semComponentes;

        var historicoReferencia = HistoricoRc8();
        var historicoAtual = HistoricoRc8();

        var referencia =
            CreateProviderRc8(semComponentes, historicoReferencia, regimeCombinado: null)
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        var atual =
            CreateProviderRc8(
                    comComponentes,
                    historicoAtual,
                    habilitado is null ? null : Chave(habilitado.Value))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        // Caminho atual: só TAG42, mapa ignorado, 30 targets, sem chave.
        Assert.True(referencia.Applied, referencia.Reason);
        Assert.Equal(30, referencia.SelectedSamples);
        Assert.Null(referencia.CurrentRegimeKey);
        AssertMesmoResultado(referencia, atual);

        Assert.Equal(historicoReferencia.Calls, historicoAtual.Calls);
        Assert.Equal(new[] { "TAG42" }, historicoAtual.Calls.Select(c => c.Tag));
    }

    [Fact]
    public async Task RC8_ChaveDesligada_NaoLeATabelaDeComponentes()
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        // Sem a tabela, qualquer leitura falharia.
        await context.Database.ExecuteSqlRawAsync("DROP TABLE TagsRegimesComponentesIa");

        var desligada =
            CreateProviderRc8(context, HistoricoRc8(), Chave(false))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.True(desligada.Applied, desligada.Reason);
        Assert.Equal(30, desligada.SelectedSamples);

        // Prova de que a ligada lê: a mesma situação vira fallback.
        var historicoLigada = HistoricoRc8();
        var ligada =
            CreateProviderRc8(context, historicoLigada, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.False(ligada.Applied);
        Assert.Contains("componentes do regime combinado", ligada.Reason);
        Assert.Empty(historicoLigada.Calls);
    }

    [Fact]
    public async Task RC8_ChaveLigada_TresComponentes_TresConsultasNaMesmaJanelaETupla()
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();

        var result =
            CreateProviderRc8(context, historico, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.Equal(
            new[]
            {
                ("TAG42", JanelaInicioRc8, Utc(0, 30)),
                ("TAG43", JanelaInicioRc8, Utc(0, 30)),
                ("TAG46", JanelaInicioRc8, Utc(0, 30))
            },
            historico.Calls);

        Assert.True(result.Applied, result.Reason);
        Assert.Equal("TAG42=2|TAG43=0|TAG46=450", result.CurrentRegimeKey);
        Assert.Equal(2, result.CurrentRegimeValue!.Value, 3);
        Assert.Equal(30, result.OriginalTargetSamples);
        Assert.Equal(15, result.SelectedSamples);
        Assert.Equal(
            Enumerable.Range(16, 15).Select(minute => Utc(0, minute)),
            result.History.Select(p => p.DataHora));
        Assert.NotNull(result.Statistics);
        Assert.InRange(result.Statistics!.Media, 50, 51);
    }

    [Fact]
    public async Task RC8_ChaveLigada_IgualAoServiceComAsMesmasSeries()
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();

        var viaProvider =
            CreateProviderRc8(context, historico, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        var configuration = context.TagsRegimesOperacionaisIa.AsNoTracking().Single();
        var components =
            context.TagsRegimesComponentesIa.AsNoTracking().OrderBy(c => c.Ordem).ToList();

        var direto =
            new OperationalRegimeBaselineService(
                    new OperationalRegimeHistoryResolver(),
                    new TagProfileStatisticsCalculator(new HistoricoLimpezaService()))
                .Resolve(
                    configuration,
                    components,
                    TargetRc8(),
                    new IReadOnlyList<LeituraBruta>[]
                    {
                        historico.PeriodHistoryByTag["TAG42"],
                        historico.PeriodHistoryByTag["TAG43"],
                        historico.PeriodHistoryByTag["TAG46"]
                    },
                    Utc(0, 31));

        AssertMesmoResultado(direto, viaProvider);
    }

    [Fact]
    public async Task RC8_ChaveLigada_SemComponentes_IdenticoAoCaminhoAtual()
    {
        // Mesmo com MapaEstados no componente 1: sem componentes, caminho atual.
        var (connection, context) = await SeedRc8Async(comComponentes: false);
        await using var _connection = connection;
        await using var _context = context;

        var historicoDesligada = HistoricoRc8();
        var historicoLigada = HistoricoRc8();

        var desligada =
            CreateProviderRc8(context, historicoDesligada, regimeCombinado: null)
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        var ligada =
            CreateProviderRc8(context, historicoLigada, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.True(desligada.Applied, desligada.Reason);
        AssertMesmoResultado(desligada, ligada);
        Assert.Null(ligada.CurrentRegimeKey);
        Assert.Equal(historicoDesligada.Calls, historicoLigada.Calls);
        Assert.Single(historicoLigada.Calls);
    }

    [Fact]
    public async Task RC8_ChaveLigada_ComponenteProibido_RecusaSemConsultarHistorico()
    {
        var (connection, context) =
            await SeedRc8Async(comComponentes: true, tagComponente3: "TAG73");
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();

        var result =
            CreateProviderRc8(context, historico, Chave(true), Pk2LabelList())
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.False(result.Applied);
        Assert.Null(result.Statistics);
        Assert.Empty(historico.Calls);
        Assert.Contains("Componente 3 (TAG73)", result.Reason);
        Assert.Contains("proibidas como regime", result.Reason);
        Assert.Contains("PK2", result.Reason);

        // Com a chave desligada o componente nem é lido: caminho atual (TAG42).
        var historicoDesligada = HistoricoRc8();
        var desligada =
            CreateProviderRc8(context, historicoDesligada, Chave(false), Pk2LabelList())
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.True(desligada.Applied, desligada.Reason);
        Assert.Equal(new[] { "TAG42" }, historicoDesligada.Calls.Select(c => c.Tag));
    }

    [Theory]
    [InlineData("TAG43", 2, new[] { "TAG42", "TAG43" })]
    [InlineData("TAG46", 3, new[] { "TAG42", "TAG43", "TAG46" })]
    public async Task RC8_ChaveLigada_FalhaNaConsultaDeUmComponente_FallbackGlobal(
        string tagComFalha,
        int ordem,
        string[] consultasEsperadas)
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();
        historico.FailOnTag = tagComFalha;

        var result =
            CreateProviderRc8(context, historico, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.False(result.Applied);
        Assert.Null(result.Statistics);
        Assert.Empty(result.History);
        Assert.Contains($"componente {ordem} ({tagComFalha})", result.Reason);
        Assert.Contains("Fallback para baseline global", result.Reason);
        Assert.Equal(consultasEsperadas, historico.Calls.Select(c => c.Tag));
    }

    [Fact]
    public async Task RC8_ChaveLigada_FalhaNaConsultaDoComponente1_FallbackSemConsultarOsOutros()
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();
        historico.FailOnTag = "TAG42";

        var result =
            CreateProviderRc8(context, historico, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.False(result.Applied);
        Assert.Contains("Não foi possível obter o histórico da tag de regime", result.Reason);
        Assert.Equal(new[] { "TAG42" }, historico.Calls.Select(c => c.Tag));
    }

    [Fact]
    public async Task RC8_ChaveLigada_ComponenteSemLeituraNoEstadoAtual_NaoAplica()
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();
        historico.PeriodHistoryByTag["TAG46"] = new List<LeituraBruta>();

        var result =
            CreateProviderRc8(context, historico, Chave(true))
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.False(result.Applied);
        Assert.Null(result.Statistics);
        Assert.Contains("TAG46", result.Reason);
        Assert.Equal(3, historico.Calls.Count);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("false", 1)]
    [InlineData("true", 3)]
    public async Task RC8_DI_ChaveLidaDaSecao_ProviderSegueAChave(
        string? habilitado,
        int consultasEsperadas)
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var values = new Dictionary<string, string?>();
        if (habilitado is not null)
            values["AnomalyDetection:RegimeCombinado:Habilitado"] = habilitado;

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

        var historico = HistoricoRc8();
        var services = new ServiceCollection();

        IAApi.Services.AnomalyDetection.DependencyInjection
            .AnomalyDetectionServiceCollectionExtensions
            .AddAnomalyDetectionConfiguration(services, configuration);

        IAApi.Services.AnomalyDetection.DependencyInjection
            .AnomalyDetectionServiceCollectionExtensions
            .AddOperationalRegimeServices(services);

        services.AddSingleton(context);
        services.AddSingleton<IHistoricoService>(historico);
        services.AddScoped<HistoricoLimpezaService>();
        services.AddScoped<TagProfileStatisticsCalculator>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var result =
            scope.ServiceProvider
                .GetRequiredService<OperationalRegimeBaselineProvider>()
                .Resolve("PK2", "TAG29", TargetRc8(), Utc(0, 31));

        Assert.True(result.Applied, result.Reason);
        Assert.Equal(consultasEsperadas, historico.Calls.Count);
        Assert.Equal(consultasEsperadas == 3 ? 15 : 30, result.SelectedSamples);
    }

    // ===== RC-9: RegimeKey no diagnóstico e no log da preparação =====

    private static async Task<(IAApi.IntelligenceLab.Observability.BaselineDiagnostic? Baseline, string Log)>
        PrepararRc9Async(bool habilitado)
    {
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();
        historico.TargetHistory = TargetRc8();

        var preparation =
            new IAApi.Services.AnomalyDetection.Orchestration.PredictionDetectionPreparationService(
                historico,
                CreateProviderRc8(context, historico, Chave(habilitado)));

        var request = new AnomaliaRequest
        {
            ClienteId = "PK2",
            TagName = "TAG29",
            Valor = 50,
            DataHora = Utc(0, 31)
        };

        var perfil = new TagPerfilIa
        {
            ClienteId = "PK2",
            TagName = "TAG29",
            Media = 75,
            DesvioPadrao = 25,
            VariacaoMinimaRelevante = 1,
            ConfiancaDeadband = 75
        };

        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        IAApi.Services.AnomalyDetection.Orchestration.PredictionDetectionPreparationResult result;

        try
        {
            result = await preparation.PrepararAsync(
                request, perfil, "perfil-comportamental", Utc(0, 31));
        }
        finally
        {
            Console.SetOut(original);
        }

        // Só as linhas desta preparação (outros testes podem escrever no console).
        var log =
            string.Join(
                "\n",
                writer.ToString()
                    .Split('\n')
                    .Where(line =>
                        line.Contains("BASELINE POR REGIME APLICADO") &&
                        line.Contains("Cliente=PK2 Tag=TAG29 ")));

        return (result.BaselineEfetivo, log.TrimEnd('\r'));
    }

    [Fact]
    public async Task RC9_ChaveDesligada_DiagnosticoELogSemChave()
    {
        var (baseline, log) = await PrepararRc9Async(habilitado: false);

        Assert.NotNull(baseline);
        Assert.Equal(
            IAApi.IntelligenceLab.Observability.BaselineDiagnosticKind.OperationalRegime,
            baseline!.Kind);
        Assert.Equal(30, baseline.SampleCount);
        Assert.Null(baseline.RegimeKey);

        // Mesma linha de antes da RC-9: "Regime=… Amostras=…", sem "Chave=".
        Assert.DoesNotContain("Chave=", log);
        Assert.Contains("BASELINE POR REGIME APLICADO Cliente=PK2 Tag=TAG29 Regime=2 Amostras=30 Media=", log);

        // JSON (padrão web do endpoint) sem a propriedade.
        var json = System.Text.Json.JsonSerializer.Serialize(
            baseline,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.DoesNotContain("regimeKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RC9_ChaveLigada_DiagnosticoELogComChave()
    {
        var (baseline, log) = await PrepararRc9Async(habilitado: true);

        Assert.NotNull(baseline);
        Assert.Equal(
            IAApi.IntelligenceLab.Observability.BaselineDiagnosticKind.OperationalRegime,
            baseline!.Kind);
        Assert.Equal(15, baseline.SampleCount);
        Assert.Equal(2, baseline.RegimeValue);
        Assert.Equal("TAG42=2|TAG43=0|TAG46=450", baseline.RegimeKey);

        Assert.Contains(
            "BASELINE POR REGIME APLICADO Cliente=PK2 Tag=TAG29 Regime=2 " +
            "Chave=TAG42=2|TAG43=0|TAG46=450 Amostras=15 Media=",
            log);

        var json = System.Text.Json.JsonSerializer.Serialize(
            baseline,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"regimeKey\":\"TAG42=2|TAG43=0|TAG46=450\"", json);
    }

    [Fact]
    public async Task RC9_RegimeNaoAplicado_DiagnosticoSemChave()
    {
        // Chave ligada, mas o componente 3 sem leitura: fallback global, e a
        // chave não vai para o diagnóstico (só com o regime aplicado).
        var (connection, context) = await SeedRc8Async(comComponentes: true);
        await using var _connection = connection;
        await using var _context = context;

        var historico = HistoricoRc8();
        historico.TargetHistory = TargetRc8();
        historico.PeriodHistoryByTag["TAG46"] = new List<LeituraBruta>();

        var result =
            await new IAApi.Services.AnomalyDetection.Orchestration.PredictionDetectionPreparationService(
                    historico,
                    CreateProviderRc8(context, historico, Chave(true)))
                .PrepararAsync(
                    new AnomaliaRequest { ClienteId = "PK2", TagName = "TAG29", Valor = 50, DataHora = Utc(0, 31) },
                    new TagPerfilIa { ClienteId = "PK2", TagName = "TAG29", Media = 75, DesvioPadrao = 25, VariacaoMinimaRelevante = 1, ConfiancaDeadband = 75 },
                    "perfil-comportamental",
                    Utc(0, 31));

        Assert.Equal(
            IAApi.IntelligenceLab.Observability.BaselineDiagnosticKind.Global,
            result.BaselineEfetivo!.Kind);
        Assert.Null(result.BaselineEfetivo.RegimeKey);
        Assert.Null(result.BaselineEfetivo.RegimeValue);
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

        /// <summary>RC-8: série por tag; tag ausente usa PeriodHistory.</summary>
        public Dictionary<string, List<LeituraBruta>> PeriodHistoryByTag { get; } =
            new(StringComparer.Ordinal);

        /// <summary>RC-8: só a consulta desta tag falha.</summary>
        public string? FailOnTag { get; set; }

        /// <summary>RC-8: todas as consultas, na ordem.</summary>
        public List<(string Tag, DateTime Inicio, DateTime Fim)> Calls { get; } =
            new();

        public int PeriodCallCount { get; private set; }

        public string? LastClienteId { get; private set; }

        public string? LastTagName { get; private set; }

        public DateTime? LastInicio { get; private set; }

        public DateTime? LastFim { get; private set; }

        /// <summary>RC-9: histórico da tag analisada para PrepararAsync.</summary>
        public List<LeituraBruta>? TargetHistory { get; set; }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            if (TargetHistory is not null)
            {
                return TargetHistory
                    .Select(item => new LeituraBruta { DataHora = item.DataHora, Valor = item.Valor })
                    .ToList();
            }

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

            Calls.Add((tagName, inicio, fim));

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            if (FailOnTag is not null &&
                string.Equals(FailOnTag, tagName, StringComparison.Ordinal))
            {
                throw new TimeoutException(
                    $"Tempo limite ao consultar {tagName}.");
            }

            var source =
                PeriodHistoryByTag.TryGetValue(tagName, out var byTag)
                    ? byTag
                    : PeriodHistory;

            return source
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