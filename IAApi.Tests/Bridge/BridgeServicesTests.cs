using System.Collections.Concurrent;
using System.Net;
using IAApi.Services.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IAApi.Tests.Bridge;

/// <summary>
/// Tarefa 22 (P28): BridgeValorAtualService e BridgeTagsService recebem o
/// HttpClient pela DI, com o endereço de Bridge:BaseUrl. Tudo offline: o
/// transporte é o <see cref="FakeBridgeHandler"/>, trocado só no handler
/// primário. Endereço, timeout e loggers vêm do registro real
/// (<see cref="BridgeServiceCollectionExtensions.AddBridgeServices"/>).
/// </summary>
public sealed class BridgeServicesTests
{
    // Endereço fixo usado antes da tarefa 22, para comparar as URLs.
    private const string EnderecoAntigo = "http://localhost:5001";

    [Theory]
    [InlineData(null, "http://localhost:5001/valores-atuais")]
    [InlineData("", "http://localhost:5001/valores-atuais")]
    [InlineData("http://bridge.test", "http://bridge.test/valores-atuais")]
    [InlineData("http://bridge.test/", "http://bridge.test/valores-atuais")]
    [InlineData("http://bridge.test/api", "http://bridge.test/api/valores-atuais")]
    [InlineData("http://bridge.test/api/", "http://bridge.test/api/valores-atuais")]
    public async Task ValorAtual_PostaNoEnderecoDaConfiguracao(string? baseUrl, string esperado)
    {
        var handler = FakeBridgeHandler.Json("[]");
        using var provider = CriarProvedor(baseUrl, handler);

        await Resolver<BridgeValorAtualService>(provider).BuscarValoresAtuais(["TAG-A"]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(esperado, request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task ValorAtual_SemChave_UrlIgualAoEnderecoFixoAntigo()
    {
        var handler = FakeBridgeHandler.Json("[]");
        using var provider = CriarProvedor(null, handler);

        await Resolver<BridgeValorAtualService>(provider).BuscarValoresAtuais(["TAG-A"]);

        Assert.Equal(
            new Uri($"{EnderecoAntigo}/valores-atuais").AbsoluteUri,
            Assert.Single(handler.Requests).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task ValorAtual_CorpoDoPostIgualAoAtual()
    {
        var handler = FakeBridgeHandler.Json("[]");
        using var provider = CriarProvedor("http://bridge.test", handler);

        await Resolver<BridgeValorAtualService>(provider)
            .BuscarValoresAtuais(["TAG-A", "TAG B", "ação"]);

        var request = Assert.Single(handler.Requests);
        // Mesmo JSON de JsonSerializer.Serialize(new { Tags = tags }), com o
        // escape padrão dos caracteres não ASCII.
        Assert.Equal("{\"Tags\":[\"TAG-A\",\"TAG B\",\"a\\u00E7\\u00E3o\"]}", request.Body);
        Assert.Equal("application/json; charset=utf-8", request.ContentType);
    }

    [Fact]
    public async Task ValorAtual_ConversaoDaRespostaNaoMuda()
    {
        var handler = FakeBridgeHandler.Json("""
            [{"tagName":"A","valor":12.5,"dataHora":"2026-11-16T10:00:00","unidade":"bar","descricao":"d","encontrado":true},
             {"tagName":"B","valor":"3.25","dataHora":"2026-11-16T10:00:00","encontrado":true},
             {"tagName":"C","valor":"NaN","encontrado":true},
             {"tagName":"D","valor":"","encontrado":true},
             {"tagName":"E","valor":null,"encontrado":true},
             {"tagName":"F","valor":"x","encontrado":true},
             {"tagName":"G","valor":7,"encontrado":false},
             {"tagName":"H","encontrado":true}]
            """);
        using var provider = CriarProvedor("http://bridge.test", handler);

        var valores = await Resolver<BridgeValorAtualService>(provider)
            .BuscarValoresAtuais(["A", "B", "C", "D", "E", "F", "G", "H"]);

        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F", "G", "H" }, valores.Select(v => v.TagName));
        Assert.Equal(12.5, valores[0].Valor);
        Assert.True(valores[0].Encontrado);
        Assert.Equal("bar", valores[0].Unidade);
        Assert.Equal("d", valores[0].Descricao);
        Assert.Equal(new DateTime(2026, 11, 16, 10, 0, 0), valores[0].DataHora);
        Assert.Equal(3.25, valores[1].Valor);
        Assert.True(valores[1].Encontrado);
        foreach (var v in valores.Skip(2).Take(4).Append(valores[7]))
        {
            Assert.True(double.IsNaN(v.Valor), v.TagName);
            Assert.False(v.Encontrado, v.TagName);
        }
        Assert.Equal(7, valores[6].Valor);
        Assert.False(valores[6].Encontrado);
    }

    [Fact]
    public async Task ValorAtual_ErroHttp_LancaComoAntes()
    {
        var handler = FakeBridgeHandler.Json("erro", HttpStatusCode.BadGateway);
        using var provider = CriarProvedor("http://bridge.test", handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Resolver<BridgeValorAtualService>(provider).BuscarValoresAtuais(["TAG-A"]));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(null, "http://localhost:5001/tags?cliente=CP300")]
    [InlineData("http://bridge.test", "http://bridge.test/tags?cliente=CP300")]
    [InlineData("http://bridge.test/", "http://bridge.test/tags?cliente=CP300")]
    [InlineData("http://bridge.test/api", "http://bridge.test/api/tags?cliente=CP300")]
    [InlineData("http://bridge.test/api/", "http://bridge.test/api/tags?cliente=CP300")]
    public async Task Tags_GetNoEnderecoDaConfiguracao_RetornaJsonCru(string? baseUrl, string esperado)
    {
        const string json = """{"cliente":"CP300","total":0,"tags":[]}""";
        var handler = FakeBridgeHandler.Json(json);
        using var provider = CriarProvedor(baseUrl, handler);

        var resposta = await Resolver<BridgeTagsService>(provider).BuscarTagsCliente("CP300");

        Assert.Equal(json, resposta);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(esperado, request.Uri.AbsoluteUri);
    }

    // Mesma codificação de antes: o clienteId entra no texto sem escape extra.
    [Theory]
    [InlineData("CP300")]
    [InlineData("cliente teste")]
    [InlineData("a&b=c")]
    [InlineData("ação")]
    [InlineData("x/y")]
    [InlineData("50%")]
    public async Task Tags_SemChave_UrlIgualAoEnderecoFixoAntigo(string clienteId)
    {
        var handler = FakeBridgeHandler.Json("{}");
        using var provider = CriarProvedor(null, handler);

        await Resolver<BridgeTagsService>(provider).BuscarTagsCliente(clienteId);

        Assert.Equal(
            new Uri($"{EnderecoAntigo}/tags?cliente={clienteId}").AbsoluteUri,
            Assert.Single(handler.Requests).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Tags_ErroHttp_LancaComoAntes()
    {
        var handler = FakeBridgeHandler.Json("erro", HttpStatusCode.NotFound);
        using var provider = CriarProvedor("http://bridge.test", handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Resolver<BridgeTagsService>(provider).BuscarTagsCliente("CP300"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Registro_ClientesComEnderecoETimeoutsAtuais()
    {
        using var provider = CriarProvedor("http://bridge.test/api", FakeBridgeHandler.FailIfCalled());
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var valorAtual = factory.CreateClient(nameof(BridgeValorAtualService));
        Assert.Equal("http://bridge.test/api/", valorAtual.BaseAddress!.AbsoluteUri);
        Assert.Equal(TimeSpan.FromSeconds(60), valorAtual.Timeout);

        var tags = factory.CreateClient(nameof(BridgeTagsService));
        Assert.Equal("http://bridge.test/api/", tags.BaseAddress!.AbsoluteUri);
        Assert.Equal(TimeSpan.FromSeconds(100), tags.Timeout);
    }

    [Fact]
    public void Registro_SemChave_UsaEnderecoPadrao()
    {
        using var provider = CriarProvedor(null, FakeBridgeHandler.FailIfCalled());
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(
            "http://localhost:5001/",
            factory.CreateClient(nameof(BridgeValorAtualService)).BaseAddress!.AbsoluteUri);
        Assert.Equal(
            "http://localhost:5001/",
            factory.CreateClient(nameof(BridgeTagsService)).BaseAddress!.AbsoluteUri);
    }

    [Fact]
    public void Registro_VidaUtil_TransientNosClientesESingletonNoHistorico()
    {
        var services = new ServiceCollection();
        services.AddBridgeServices();

        Assert.Equal(
            ServiceLifetime.Transient,
            Assert.Single(services, d => d.ServiceType == typeof(BridgeValorAtualService)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Transient,
            Assert.Single(services, d => d.ServiceType == typeof(BridgeTagsService)).Lifetime);
        var historico = Assert.Single(services, d => d.ServiceType == typeof(IHistoricoService));
        Assert.Equal(ServiceLifetime.Singleton, historico.Lifetime);
        Assert.Equal(typeof(BridgeHistoricoService), historico.ImplementationType);
    }

    [Theory]
    [InlineData("nao-e-url")]
    [InlineData("ftp://bridge.test")]
    [InlineData("localhost:5001")]
    public void Registro_EnderecoInvalido_FalhaSemExporOValor(string baseUrl)
    {
        var handler = FakeBridgeHandler.FailIfCalled();
        using var provider = CriarProvedor(baseUrl, handler);

        var erro = Assert.Throws<InvalidOperationException>(
            () => Resolver<BridgeValorAtualService>(provider));

        Assert.Contains("Bridge:BaseUrl", erro.Message);
        Assert.DoesNotContain(baseUrl, erro.Message);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Registro_SemLogsDoHttpClientFactory()
    {
        var logs = new CapturingLoggerProvider();
        var handler = FakeBridgeHandler.Json("[]");
        using var provider = CriarProvedor("http://bridge.test", handler, logs, controle: true);

        await Resolver<BridgeValorAtualService>(provider).BuscarValoresAtuais(["TAG-A"]);
        await Resolver<BridgeTagsService>(provider).BuscarTagsCliente("CP300");

        Assert.Equal(2, handler.Calls);
        Assert.DoesNotContain(logs.Categorias, c => c.Contains(nameof(BridgeValorAtualService)));
        Assert.DoesNotContain(logs.Categorias, c => c.Contains(nameof(BridgeTagsService)));

        // Controle: um cliente comum, no mesmo provedor, gera os logs do
        // factory. Sem isso, o teste passaria mesmo sem logger nenhum.
        await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("controle")
            .GetStringAsync("http://bridge.test/controle");
        Assert.Contains(logs.Categorias, c => c.StartsWith("System.Net.Http.HttpClient.controle"));
    }

    private static T Resolver<T>(ServiceProvider provider) where T : notnull
    {
        // Como no monitor: o serviço é resolvido dentro de um escopo.
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    private static ServiceProvider CriarProvedor(
        string? baseUrl,
        FakeBridgeHandler handler,
        CapturingLoggerProvider? logs = null,
        bool controle = false)
    {
        var valores = new Dictionary<string, string?>();
        if (baseUrl is not null)
        {
            valores["Bridge:BaseUrl"] = baseUrl;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(valores).Build());
        services.AddLogging(builder =>
        {
            // Mesmo nível padrão do appsettings.json.
            builder.SetMinimumLevel(LogLevel.Information);
            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });

        services.AddBridgeServices();

        // Só o transporte muda; o resto vem do registro real.
        services.AddHttpClient(nameof(BridgeValorAtualService))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient(nameof(BridgeTagsService))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        if (controle)
        {
            services.AddHttpClient("controle")
                .ConfigurePrimaryHttpMessageHandler(() => FakeBridgeHandler.Json("ok"));
        }

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _categorias = new();

        public IReadOnlyCollection<string> Categorias => _categorias.ToArray();

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Logger(CapturingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => owner._categorias.Enqueue(category);
        }
    }
}
