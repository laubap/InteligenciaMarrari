using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Serviço responsável por buscar o histórico de leituras
/// no Bridge conectado ao PowerServer.
/// </summary>
public class BridgeHistoricoService : IHistoricoService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public BridgeHistoricoService(IConfiguration configuration)
    {
        _configuration =
            configuration
            ?? throw new ArgumentNullException(nameof(configuration));

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    /// <summary>
    /// Busca o histórico recente da tag utilizando a quantidade
    /// de horas configurada no appsettings.json.
    /// </summary>
    public List<LeituraBruta> BuscarHistorico(
        string clienteId,
        string tagName,
        string tipoTag)
    {
        ValidarClienteETag(clienteId, tagName);

        var baseUrl = ObterBaseUrl();

        var horasTexto =
            _configuration["Bridge:HorasHistorico"]
            ?? "24";

        if (
            !int.TryParse(
                horasTexto,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var horas
            )
            || horas <= 0
        )
        {
            horas = 24;
        }

        var url =
            $"{baseUrl}/historico" +
            $"?tagName={Uri.EscapeDataString(tagName)}" +
            $"&horas={horas.ToString(CultureInfo.InvariantCulture)}";

        Console.WriteLine("================================");
        Console.WriteLine("BUSCANDO HISTÓRICO RECENTE NO BRIDGE");
        Console.WriteLine($"Cliente: {clienteId}");
        Console.WriteLine($"Tag: {tagName}");
        Console.WriteLine($"Tipo: {tipoTag}");
        Console.WriteLine($"Horas: {horas}");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine("================================");

        var json = BuscarJson(url, baseUrl);

        return ConverterHistorico(
            json,
            clienteId,
            tagName
        );
    }

    /// <summary>
    /// Busca o histórico exatamente dentro da janela informada.
    ///
    /// O início e o fim são enviados diretamente ao Bridge.
    /// O Bridge consulta o PowerServer usando exatamente esse intervalo.
    /// </summary>
    public List<LeituraBruta> BuscarHistoricoPorPeriodo(
        string clienteId,
        string tagName,
        DateTime inicio,
        DateTime fim)
    {
        ValidarClienteETag(clienteId, tagName);

        if (fim <= inicio)
        {
            throw new ArgumentException(
                "A data final da janela deve ser maior que a data inicial."
            );
        }

        var baseUrl = ObterBaseUrl();

        /*
         * O formato "O" envia a data com precisão completa.
         *
         * Exemplo:
         * 2026-07-15T08:00:00.0000000
         *
         * Uri.EscapeDataString protege caracteres especiais
         * presentes na data e no nome da tag.
         */
        var inicioFormatado =
            inicio.ToString(
                "O",
                CultureInfo.InvariantCulture
            );

        var fimFormatado =
            fim.ToString(
                "O",
                CultureInfo.InvariantCulture
            );

        var url =
            $"{baseUrl}/historico" +
            $"?tagName={Uri.EscapeDataString(tagName)}" +
            $"&inicio={Uri.EscapeDataString(inicioFormatado)}" +
            $"&fim={Uri.EscapeDataString(fimFormatado)}";

        Console.WriteLine("================================");
        Console.WriteLine("BUSCANDO JANELA DE TREINAMENTO NO BRIDGE");
        Console.WriteLine($"Cliente: {clienteId}");
        Console.WriteLine($"Tag: {tagName}");
        Console.WriteLine(
            $"Início: {inicio:yyyy-MM-dd HH:mm:ss}"
        );
        Console.WriteLine(
            $"Fim: {fim:yyyy-MM-dd HH:mm:ss}"
        );
        Console.WriteLine($"URL: {url}");
        Console.WriteLine("================================");

        var json = BuscarJson(url, baseUrl);

        var historico = ConverterHistorico(
            json,
            clienteId,
            tagName
        );

        /*
         * O Bridge já consulta o período exato.
         * Mesmo assim, este filtro final protege contra algum registro
         * retornado fora dos limites ou com valor inválido.
         */
        var resultado = historico
            .Where(leitura =>
                leitura.DataHora >= inicio
                && leitura.DataHora <= fim
                && !float.IsNaN(leitura.Valor)
                && !float.IsInfinity(leitura.Valor)
            )
            .GroupBy(leitura => leitura.DataHora)
            .Select(grupo => grupo.Last())
            .OrderBy(leitura => leitura.DataHora)
            .ToList();

        Console.WriteLine(
            $"Leituras encontradas na janela: {resultado.Count}"
        );
        Console.WriteLine("================================");

        if (resultado.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nenhuma leitura foi encontrada para a tag " +
                $"'{tagName}' entre " +
                $"{inicio:dd/MM/yyyy HH:mm:ss} e " +
                $"{fim:dd/MM/yyyy HH:mm:ss}."
            );
        }

        return resultado;
    }

    /// <summary>
    /// Obtém e normaliza a URL base do Bridge.
    /// </summary>
    private string ObterBaseUrl()
    {
        var baseUrl =
            _configuration["Bridge:BaseUrl"]
            ?? "http://localhost:5001";

        return baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Executa a requisição HTTP no Bridge.
    /// </summary>
    private string BuscarJson(
        string url,
        string baseUrl)
    {
        try
        {
            using var response = _httpClient
                .GetAsync(url)
                .GetAwaiter()
                .GetResult();

            var conteudo = response.Content
                .ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"O Bridge respondeu com o status " +
                    $"{(int)response.StatusCode} " +
                    $"({response.ReasonPhrase}). " +
                    $"Resposta: {conteudo}"
                );
            }

            if (string.IsNullOrWhiteSpace(conteudo))
            {
                throw new InvalidOperationException(
                    "O Bridge retornou uma resposta vazia."
                );
            }

            return conteudo;
        }
        catch (TaskCanceledException ex)
        {
            throw new Exception(
                "Tempo limite excedido ao consultar o Bridge.",
                ex
            );
        }
        catch (HttpRequestException ex)
        {
            throw new Exception(
                $"Não foi possível conectar ao Bridge em " +
                $"'{baseUrl}'. Verifique se o Bridge está rodando.",
                ex
            );
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Erro inesperado ao consultar o Bridge: " +
                $"{ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Converte a resposta JSON do Bridge para o modelo usado pela IA.
    /// </summary>
    private static List<LeituraBruta> ConverterHistorico(
        string json,
        string clienteId,
        string tagName)
    {
        try
        {
            var dados =
                JsonSerializer.Deserialize<List<BridgeHistoricoItem>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

            if (dados == null || dados.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Nenhum histórico foi encontrado para a tag " +
                    $"'{tagName}' do cliente '{clienteId}'."
                );
            }

            Console.WriteLine(
                $"Histórico recebido do Bridge: {dados.Count} registros"
            );

            var resultado = dados
                .Select(item => new LeituraBruta
                {
                    DataHora = item.DataHora,
                    Valor = ConverterValor(item.Valor)
                })
                .Where(leitura =>
                    !float.IsNaN(leitura.Valor)
                    && !float.IsInfinity(leitura.Valor)
                )
                .GroupBy(leitura => leitura.DataHora)
                .Select(grupo => grupo.Last())
                .OrderBy(leitura => leitura.DataHora)
                .ToList();

            Console.WriteLine(
                $"Histórico válido convertido para IA: " +
                $"{resultado.Count} registros"
            );

            if (resultado.Count == 0)
            {
                throw new InvalidOperationException(
                    $"O Bridge retornou registros para a tag " +
                    $"'{tagName}', mas nenhum possui valor numérico válido."
                );
            }

            return resultado;
        }
        catch (JsonException ex)
        {
            throw new Exception(
                $"O Bridge retornou um JSON inválido. " +
                $"Erro: {ex.Message}. " +
                $"Resposta recebida: {json}",
                ex
            );
        }
    }

    /// <summary>
    /// Converte o valor retornado pelo Bridge para float.
    /// O Bridge pode retornar o valor como número ou texto.
    /// </summary>
    private static float ConverterValor(JsonElement valorJson)
    {
        if (valorJson.ValueKind == JsonValueKind.Number)
        {
            if (valorJson.TryGetSingle(out var numero))
            {
                return numero;
            }

            if (valorJson.TryGetDouble(out var numeroDouble))
            {
                return (float)numeroDouble;
            }

            return float.NaN;
        }

        if (valorJson.ValueKind == JsonValueKind.String)
        {
            var texto = valorJson.GetString();

            if (string.IsNullOrWhiteSpace(texto))
            {
                return float.NaN;
            }

            if (
                texto.Equals(
                    "NaN",
                    StringComparison.OrdinalIgnoreCase
                )
                || texto.Equals(
                    "Infinity",
                    StringComparison.OrdinalIgnoreCase
                )
                || texto.Equals(
                    "-Infinity",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return float.NaN;
            }

            if (
                float.TryParse(
                    texto,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var valorInvariante
                )
            )
            {
                return valorInvariante;
            }

            if (
                float.TryParse(
                    texto,
                    NumberStyles.Float,
                    CultureInfo.GetCultureInfo("pt-BR"),
                    out var valorPtBr
                )
            )
            {
                return valorPtBr;
            }
        }

        return float.NaN;
    }

    /// <summary>
    /// Valida cliente e tag antes de consultar o Bridge.
    /// </summary>
    private static void ValidarClienteETag(
        string clienteId,
        string tagName)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ArgumentException(
                "O identificador do cliente é obrigatório.",
                nameof(clienteId)
            );
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException(
                "O nome da tag é obrigatório.",
                nameof(tagName)
            );
        }

        /*
         * A validação permanece apenas como aviso porque alguns projetos
         * podem possuir nomes de tags sem o prefixo do cliente.
         */
        if (
            tagName.Contains('.')
            && !tagName.StartsWith(
                clienteId + ".",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            Console.WriteLine(
                $"AVISO: a tag '{tagName}' não começa com o " +
                $"prefixo do cliente '{clienteId}'."
            );
        }
    }
}

/// <summary>
/// Estrutura correspondente a cada leitura retornada pelo Bridge.
/// </summary>
public class BridgeHistoricoItem
{
    public DateTime DataHora { get; set; }

    public string TagName { get; set; } = "";

    public JsonElement Valor { get; set; }
}