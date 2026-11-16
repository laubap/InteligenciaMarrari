using System.Globalization;
using System.Text;
using System.Text.Json;

// Serviço que consulta valores atuais de tags no Bridge externo.
// Envia uma lista de tags para o endpoint /valores-atuais e converte o JSON
// retornado em objetos de resposta com valor numérico.
public class BridgeValorAtualService
{
    private readonly HttpClient _httpClient;

    // O HttpClient vem da DI (AddBridgeServices): endereço de Bridge:BaseUrl
    // e timeout de 60 s. Nos testes, um handler falso evita qualquer conexão.
    public BridgeValorAtualService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    // Busca os valores atuais das tags informadas e retorna uma lista
    // de respostas prontas para uso pela aplicação.
    public async Task<List<ValorAtualTagResponse>> BuscarValoresAtuais(
        List<string> tags)
    {
        var request = new
        {
            Tags = tags
        };

        var json = JsonSerializer.Serialize(request);

        var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        // Caminho relativo ao BaseAddress (Bridge:BaseUrl).
        var response = await _httpClient.PostAsync(
            "valores-atuais",
            content
        );

        response.EnsureSuccessStatusCode();

        var responseJson =
            await response.Content.ReadAsStringAsync();

        var dados = JsonSerializer.Deserialize<List<ValorAtualTagBridgeItem>>(
            responseJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        ) ?? new List<ValorAtualTagBridgeItem>();

        return dados.Select(x => new ValorAtualTagResponse
        {
            TagName = x.TagName,
            Valor = ConverterValor(x.Valor),
            DataHora = x.DataHora,
            Unidade = x.Unidade,
            Descricao = x.Descricao,
            Encontrado = x.Encontrado && !double.IsNaN(ConverterValor(x.Valor))
        }).ToList();
    }

    // Converte o valor retornado pelo Bridge de JsonElement para double.
    // Suporta números e strings, retornando NaN para valores inválidos.
    private double ConverterValor(JsonElement valor)
    {
        if (valor.ValueKind == JsonValueKind.Number)
            return valor.GetDouble();

        if (valor.ValueKind == JsonValueKind.String)
        {
            var texto = valor.GetString();

            if (string.IsNullOrWhiteSpace(texto))
                return double.NaN;

            if (texto.Equals("NaN", StringComparison.OrdinalIgnoreCase))
                return double.NaN;

            if (double.TryParse(
                    texto,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var numero))
            {
                return numero;
            }
        }

        return double.NaN;
    }
}

// Modelo que representa o item recebido do Bridge no endpoint /valores-atuais.
public class ValorAtualTagBridgeItem
{
    public string TagName { get; set; } = "";

    public JsonElement Valor { get; set; }

    public DateTime DataHora { get; set; }

    public string? Unidade { get; set; }

    public string? Descricao { get; set; }

    public bool Encontrado { get; set; }
}

// Modelo de saída usado internamente após a conversão do valor.
public class ValorAtualTagResponse
{
    public string TagName { get; set; } = "";

    public double Valor { get; set; }

    public DateTime DataHora { get; set; }

    public string? Unidade { get; set; }

    public string? Descricao { get; set; }

    public bool Encontrado { get; set; }
}