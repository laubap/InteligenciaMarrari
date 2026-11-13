using System.Globalization;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// RC-4 (PLANO_REGIME_COMBINADO.md §6.3): chave, mapa de estados e chave
/// de regime, como classes isoladas. Nada no runtime chama essas classes
/// ainda.
/// </summary>
public class RegimeCombinadoClassesTests
{
    // ------------------------------------------------------------------
    // Opções (chave)
    // ------------------------------------------------------------------

    [Fact]
    public void Options_PadraoDesligado()
    {
        Assert.False(new RegimeCombinadoOptions().Habilitado);
        Assert.Equal("AnomalyDetection:RegimeCombinado", RegimeCombinadoOptions.SectionName);
    }

    [Fact]
    public void Options_SemSecao_Desligado()
    {
        var options = ReadOptions(new Dictionary<string, string?>());

        Assert.False(options.Habilitado);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    public void Options_LidaDaSecao(
        string text,
        bool expected)
    {
        var options = ReadOptions(new Dictionary<string, string?>
        {
            ["AnomalyDetection:RegimeCombinado:Habilitado"] = text
        });

        Assert.Equal(expected, options.Habilitado);
    }

    [Fact]
    public void Options_SecaoNaoAfetaAnomalyDetectionOptionsNemTagsProibidas()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AnomalyDetection:RegimeCombinado:Habilitado"] = "true"
            })
            .Build();

        using var provider = BuildProvider(configuration);

        var combined = provider.GetRequiredService<IOptions<RegimeCombinadoOptions>>().Value;
        var forbidden = provider.GetRequiredService<IOptions<OperationalRegimeForbiddenTagsOptions>>().Value;

        Assert.True(combined.Habilitado);
        Assert.Empty(forbidden.TagsProibidasPorCliente);
    }

    [Fact]
    public void AppSettings_NaoTemSecaoRegimeCombinado()
    {
        // O padrão vem do código: o appsettings.json não pode ligar a chave.
        var path = FindAppSettings();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build();

        Assert.False(configuration.GetSection(RegimeCombinadoOptions.SectionName).Exists());
    }

    // ------------------------------------------------------------------
    // Mapa de estados: parse
    // ------------------------------------------------------------------

    [Fact]
    public void Map_ParseBomba()
    {
        var map = OperationalRegimeStateMap.Parse("2:2;*:0");

        Assert.Equal(
            new[] { new OperationalRegimeStateMapEntry(2, 2) },
            map.Entries);
        Assert.Equal(0, map.WildcardValue);
        Assert.Equal("2:2;*:0", map.ToString());
    }

    [Fact]
    public void Map_ParseSetPoint()
    {
        var map = OperationalRegimeStateMap.Parse("380:380;*:450");

        Assert.Equal(380, map.Entries.Single().Key);
        Assert.Equal(450, map.WildcardValue);
    }

    [Theory]
    [InlineData(" 2 : 2 ; * : 0 ", "2:2;*:0")]
    [InlineData("*:0;2:2", "2:2;*:0")]
    [InlineData("0:0;1:0;2:2", "0:0;1:0;2:2")]
    [InlineData("-1.5:3;2.25:4", "-1.5:3;2.25:4")]
    [InlineData("1e2:1", "100:1")]
    [InlineData("2:2", "2:2")]
    [InlineData("*:7", "*:7")]
    public void Map_ParseValido_FormaCanonica(
        string text,
        string canonical)
    {
        Assert.Equal(canonical, OperationalRegimeStateMap.Parse(text).ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2:2;")]
    [InlineData(";2:2")]
    [InlineData("2:2;;*:0")]
    [InlineData("2")]
    [InlineData("2:")]
    [InlineData(":2")]
    [InlineData("2:2:2")]
    [InlineData("2=2")]
    [InlineData("a:2")]
    [InlineData("2:b")]
    [InlineData("2:*")]
    [InlineData("2,5:1")]
    [InlineData("1:2,5")]
    [InlineData("1,000:1")]
    [InlineData("NaN:1")]
    [InlineData("1:NaN")]
    [InlineData("Infinity:1")]
    [InlineData("1:-Infinity")]
    [InlineData("1e400:1")]
    [InlineData("2:2;2:0")]
    [InlineData("2:2;2.0:0")]
    [InlineData("*:0;*:1")]
    public void Map_ParseInvalido_Erro(
        string? text)
    {
        Assert.False(OperationalRegimeStateMap.TryParse(text, out var map, out var error));
        Assert.Null(map);
        Assert.False(string.IsNullOrWhiteSpace(error));

        Assert.Throws<FormatException>(() => OperationalRegimeStateMap.Parse(text));
    }

    [Fact]
    public void Map_ParseIndependeDaCulturaAtual()
    {
        WithCulture("pt-BR", () =>
        {
            var map = OperationalRegimeStateMap.Parse("2.5:1");

            Assert.Equal(2.5, map.Entries.Single().Key);
            Assert.Equal("2.5:1", map.ToString());
            Assert.False(OperationalRegimeStateMap.TryParse("2,5:1", out _, out _));
        });
    }

    // ------------------------------------------------------------------
    // Mapa de estados: aplicação
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(2, 0, 2, OperationalRegimeStateMapStatus.Entry)]
    [InlineData(0, 0, 0, OperationalRegimeStateMapStatus.Wildcard)]
    [InlineData(1, 0, 0, OperationalRegimeStateMapStatus.Wildcard)]
    [InlineData(3, 0, 0, OperationalRegimeStateMapStatus.Wildcard)]
    [InlineData(2.0005, 0.001, 2, OperationalRegimeStateMapStatus.Entry)]
    [InlineData(2.0005, 0, 0, OperationalRegimeStateMapStatus.Wildcard)]
    [InlineData(1.5, 0.5, 2, OperationalRegimeStateMapStatus.Entry)]
    [InlineData(1.49, 0.5, 0, OperationalRegimeStateMapStatus.Wildcard)]
    public void Map_AplicaComTolerancia_BombaComCuringa(
        double value,
        double tolerance,
        double expected,
        OperationalRegimeStateMapStatus status)
    {
        var mapping = OperationalRegimeStateMap.Parse("2:2;*:0").Apply(value, tolerance);

        Assert.Equal(value, mapping.OriginalValue);
        Assert.Equal(expected, mapping.MappedValue);
        Assert.Equal(status, mapping.Status);
    }

    [Fact]
    public void Map_SemCuringa_ValorForaDoMapaPassaSemMudanca()
    {
        var map = OperationalRegimeStateMap.Parse("0:0;2:2");

        var inside = map.Apply(2, 0.001);
        var outside = map.Apply(1, 0.001);

        Assert.Equal(OperationalRegimeStateMapStatus.Entry, inside.Status);
        Assert.Equal(2, inside.MappedValue);

        Assert.Equal(OperationalRegimeStateMapStatus.OutOfMap, outside.Status);
        Assert.Equal(1, outside.MappedValue);
    }

    [Fact]
    public void Map_VariasEntradasCasam_ValeAPrimeiraNaOrdemEscrita()
    {
        Assert.Equal(10, OperationalRegimeStateMap.Parse("1:10;1.5:15").Apply(1.3, 0.5).MappedValue);
        Assert.Equal(15, OperationalRegimeStateMap.Parse("1.5:15;1:10").Apply(1.3, 0.5).MappedValue);
    }

    [Fact]
    public void Map_EntradaNumericaTemPrioridadeSobreCuringa()
    {
        // O '*' escrito antes não captura um valor que tem entrada própria.
        Assert.Equal(2, OperationalRegimeStateMap.Parse("*:0;2:2").Apply(2, 0).MappedValue);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Map_LeituraNaoFinita_PassaSemMudanca(
        double value)
    {
        var mapping = OperationalRegimeStateMap.Parse("2:2;*:0").Apply(value, 0.001);

        Assert.Equal(OperationalRegimeStateMapStatus.OutOfMap, mapping.Status);
        Assert.Equal(value, mapping.MappedValue);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Map_ToleranciaInvalida_Erro(
        double tolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => OperationalRegimeStateMap.Parse("2:2;*:0").Apply(2, tolerance));
    }

    // ------------------------------------------------------------------
    // Chave de regime
    // ------------------------------------------------------------------

    [Fact]
    public void Key_FormatoDoPlano()
    {
        var key = OperationalRegimeKey.Build(new[]
        {
            new OperationalRegimeKeyComponent("TAG42", 2),
            new OperationalRegimeKeyComponent("TAG43", 0),
            new OperationalRegimeKeyComponent("TAG46", 380)
        });

        Assert.Equal("TAG42=2|TAG43=0|TAG46=380", key);
    }

    [Fact]
    public void Key_RespeitaAOrdemDosComponentes()
    {
        var b01 = OperationalRegimeKey.Build(new[]
        {
            new OperationalRegimeKeyComponent("TAG42", 2),
            new OperationalRegimeKeyComponent("TAG43", 0),
            new OperationalRegimeKeyComponent("TAG46", 450)
        });

        var b02 = OperationalRegimeKey.Build(new[]
        {
            new OperationalRegimeKeyComponent("TAG43", 0),
            new OperationalRegimeKeyComponent("TAG42", 2),
            new OperationalRegimeKeyComponent("TAG46", 450)
        });

        Assert.Equal("TAG42=2|TAG43=0|TAG46=450", b01);
        Assert.Equal("TAG43=0|TAG42=2|TAG46=450", b02);
    }

    [Fact]
    public void Key_CulturaPtBr_IgualAInvariante()
    {
        var components = new[]
        {
            new OperationalRegimeKeyComponent("TAG42", 2.5),
            new OperationalRegimeKeyComponent("TAG46", 1234.75),
            new OperationalRegimeKeyComponent("TAG47", -0.125)
        };

        var invariant = "";
        var ptBr = "";

        WithCulture("", () => invariant = OperationalRegimeKey.Build(components));
        WithCulture("pt-BR", () => ptBr = OperationalRegimeKey.Build(components));

        Assert.Equal("TAG42=2.5|TAG46=1234.75|TAG47=-0.125", invariant);
        Assert.Equal(invariant, ptBr);
    }

    [Fact]
    public void Key_ZeroNegativoViraZero_EEspacosDaTagSaem()
    {
        Assert.Equal(
            "TAG42=0",
            OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent("  TAG42 ", -0.0) }));
    }

    [Fact]
    public void Key_UmComponente()
    {
        Assert.Equal(
            "TAG43=2",
            OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent("TAG43", 2) }));
    }

    [Fact]
    public void Key_EntradasInvalidas_Erro()
    {
        Assert.Throws<ArgumentNullException>(() => OperationalRegimeKey.Build(null!));
        Assert.Throws<ArgumentException>(() => OperationalRegimeKey.Build(Array.Empty<OperationalRegimeKeyComponent>()));
        Assert.Throws<ArgumentException>(() => OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent(" ", 1) }));
        Assert.Throws<ArgumentException>(() => OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent(null!, 1) }));
        Assert.Throws<ArgumentException>(() => OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent("TAG|42", 1) }));
        Assert.Throws<ArgumentException>(() => OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent("TAG=42", 1) }));
        Assert.Throws<ArgumentException>(() => OperationalRegimeKey.Build(new[]
        {
            new OperationalRegimeKeyComponent("TAG42", 1),
            new OperationalRegimeKeyComponent("tag42", 2)
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent("TAG42", double.NaN) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => OperationalRegimeKey.Build(new[] { new OperationalRegimeKeyComponent("TAG42", double.NegativeInfinity) }));
    }

    [Fact]
    public void MapaEChave_ConfiguracaoAlvoDasBombas()
    {
        // §6.2: TAG29 (B-01) = TAG42 (2→2; *→0) | TAG43 (2→2; *→0) | TAG46 (380→380; *→450).
        var pump = OperationalRegimeStateMap.Parse("2:2;*:0");
        var setPoint = OperationalRegimeStateMap.Parse("380:380;*:450");
        const double tolerance = 0.001;

        string KeyFor(double tag42, double tag43, double tag46) =>
            OperationalRegimeKey.Build(new[]
            {
                new OperationalRegimeKeyComponent("TAG42", pump.Apply(tag42, tolerance).MappedValue),
                new OperationalRegimeKeyComponent("TAG43", pump.Apply(tag43, tolerance).MappedValue),
                new OperationalRegimeKeyComponent("TAG46", setPoint.Apply(tag46, tolerance).MappedValue)
            });

        Assert.Equal("TAG42=2|TAG43=0|TAG46=380", KeyFor(2, 1, 380));
        Assert.Equal("TAG42=2|TAG43=0|TAG46=380", KeyFor(2, 0, 380));
        Assert.Equal("TAG42=0|TAG43=2|TAG46=450", KeyFor(0, 2, 420));
        Assert.Equal("TAG42=2|TAG43=2|TAG46=450", KeyFor(2, 2, 450));
    }

    // ------------------------------------------------------------------

    private static RegimeCombinadoOptions ReadOptions(
        Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        using var provider = BuildProvider(configuration);

        return provider.GetRequiredService<IOptions<RegimeCombinadoOptions>>().Value;
    }

    private static ServiceProvider BuildProvider(
        IConfiguration configuration)
    {
        var services = new ServiceCollection();

        AnomalyDetectionServiceCollectionExtensions
            .AddAnomalyDetectionConfiguration(services, configuration);

        return services.BuildServiceProvider();
    }

    private static string FindAppSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "appsettings.json");

            if (File.Exists(candidate) &&
                File.Exists(Path.Combine(directory.FullName, "IAApi.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("appsettings.json da API não encontrado.");
    }

    private static void WithCulture(
        string name,
        Action action)
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
