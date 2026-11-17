using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IAApi.Tests.Monitoring;

/// <summary>
/// Tarefa 36.1 (P44): por que o log da sessao de 16/11 saiu sem data e hora.
///
/// Os testes montam o host como o Program.cs (<c>WebApplication.CreateBuilder</c>) com as mesmas
/// chaves que o tools/Start-IAApiComLog.ps1 passa por variavel de ambiente (aqui, em memoria; o
/// separador "__" da variavel de ambiente equivale a ":"), mandam uma linha ao console capturado e
/// leem o texto que o provedor de console de fato escreveu. Nao sobem servidor, nao leem Bridge,
/// nao abrem banco e nao tocam variavel de ambiente do processo.
///
/// Causa confirmada: sem <c>Logging:Console:FormatterName</c>, o provedor de console escolhe o
/// formatador "simple" copiando as opcoes ANTIGAS (<c>Logging:Console:TimestampFormat</c>,
/// <c>IncludeScopes</c>, <c>UseUtcTimestamp</c>) e descarta <c>Logging:Console:FormatterOptions</c>.
/// O script so definia <c>FormatterOptions:TimestampFormat</c> e, portanto, nao tinha efeito.
/// </summary>
[Collection(ConsoleSemParalelismo.Nome)]
public sealed class ConsoleLogFormatoSessaoTests
{
    // "2026-11-16 15:19:30 info: Categoria[0]" (formatador simple com TimestampFormat).
    private static readonly Regex LinhaComHorario = new(
        @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} info: ", RegexOptions.Multiline);

    private const string Categoria = "IAApi.Tests.Formato";
    private const string CategoriaEfCommand = "Microsoft.EntityFrameworkCore.Database.Command";

    // O que o script mandava em 16/11 (Start-IAApiComLog.ps1, passo 3).
    private static readonly Dictionary<string, string?> ComoEm16Nov = new()
    {
        ["Logging:Console:FormatterOptions:TimestampFormat"] = "yyyy-MM-dd HH:mm:ss ",
        ["Logging:Console:FormatterOptions:ColorBehavior"] = "Disabled",
    };

    [Fact]
    public async Task ComoEm16Nov_SoFormatterOptions_SaiSemHorario()
    {
        var saida = await EscreverLinhaAsync(ComoEm16Nov);

        Assert.Contains($"info: {Categoria}[0]", saida);
        Assert.DoesNotMatch(LinhaComHorario, saida);
    }

    [Fact]
    public async Task ComFormatterNameSimple_FormatterOptionsPassamAValer_SaiComHorario()
    {
        var cfg = new Dictionary<string, string?>(ComoEm16Nov)
        {
            ["Logging:Console:FormatterName"] = "simple",
        };

        var saida = await EscreverLinhaAsync(cfg);

        Assert.Matches(LinhaComHorario, saida);
        Assert.Contains($"info: {Categoria}[0]", saida);
    }

    [Fact]
    public async Task ChaveAntigaTimestampFormat_TambemFunciona_MasNaoEAEscolhida()
    {
        // Alternativa descartada: funciona, mas e a API marcada como obsoleta.
        var cfg = new Dictionary<string, string?>
        {
            ["Logging:Console:TimestampFormat"] = "yyyy-MM-dd HH:mm:ss ",
        };

        var saida = await EscreverLinhaAsync(cfg);

        Assert.Matches(LinhaComHorario, saida);
    }

    [Fact]
    public async Task SqlDoEfEmWarning_SomeNoInformationEMantemWarningEErro()
    {
        var cfg = new Dictionary<string, string?>(ComoEm16Nov)
        {
            ["Logging:Console:FormatterName"] = "simple",
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:" + CategoriaEfCommand] = "Warning",
        };

        var saida = await EscreverAsync(cfg, fabrica =>
        {
            var ef = fabrica.CreateLogger(CategoriaEfCommand);
            ef.LogInformation("Executed DbCommand (1ms) [SELECT 1]");
            ef.LogWarning("Aviso do EF mantido");
            ef.LogError("Failed executing DbCommand mantido");
            // Outras categorias seguem em Information.
            fabrica.CreateLogger(Categoria).LogInformation("Linha de outra categoria");
        });

        Assert.DoesNotContain("Executed DbCommand", saida);
        Assert.Contains("Aviso do EF mantido", saida);
        Assert.Contains("Failed executing DbCommand mantido", saida);
        Assert.Contains("Linha de outra categoria", saida);
    }

    [Fact]
    public async Task SemFiltro_SqlDoEfEmInformationAparece_Como960MilLinhasDe16Nov()
    {
        var cfg = new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Information",
        };

        var saida = await EscreverAsync(cfg, fabrica =>
            fabrica.CreateLogger(CategoriaEfCommand).LogInformation("Executed DbCommand (1ms) [SELECT 1]"));

        Assert.Contains("Executed DbCommand", saida);
    }

    private static Task<string> EscreverLinhaAsync(IDictionary<string, string?> configuracao) =>
        EscreverAsync(configuracao, fabrica => fabrica.CreateLogger(Categoria).LogInformation("Linha de teste"));

    private static async Task<string> EscreverAsync(
        IDictionary<string, string?> configuracao, Action<ILoggerFactory> escrever)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        // O provedor de console pega o Console.Out ao ser criado: trocar ANTES de montar o host.
        Console.SetOut(writer);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Configuration.AddInMemoryCollection(configuracao);

            var app = builder.Build(); // monta o container; nao inicia servidor
            escrever(app.Services.GetRequiredService<ILoggerFactory>());
            await app.DisposeAsync();   // descarrega a fila do provedor de console
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }
}

[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ConsoleSemParalelismo
{
    public const string Nome = "Console (Console.SetOut)";
}
