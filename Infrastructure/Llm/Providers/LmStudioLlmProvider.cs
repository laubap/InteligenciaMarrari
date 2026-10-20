using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.Infrastructure.Llm.Configuration;
using IAApi.Application.Llm.Contracts;
using Microsoft.Extensions.Options;

namespace IAApi.Infrastructure.Llm.Providers;

public sealed class LmStudioLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<LmStudioLlmProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LmStudioLlmProvider(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<LmStudioLlmProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Nome => "LM Studio";

    public async Task<LlmResponse> GerarAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Habilitado)
        {
            return CriarFalha(
                "O provedor de LLM está desabilitado."
            );
        }

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            return CriarFalha(
                "O prompt de sistema não foi informado."
            );
        }

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return CriarFalha(
                "O prompt do usuário não foi informado."
            );
        }

        if (string.IsNullOrWhiteSpace(_options.Modelo))
        {
            return CriarFalha(
                "O modelo do LM Studio não foi configurado."
            );
        }

        var payload = CriarPayload(request);

        try
        {
            _logger.LogInformation(
                "Enviando solicitação para o LM Studio. " +
                "Modelo: {Modelo}. Endereço: {BaseUrl}",
                _options.Modelo,
                _options.BaseUrl
            );


            

            using var response =
                await _httpClient.PostAsJsonAsync(
                    "chat/completions",
                    payload,
                    JsonOptions,
                    cancellationToken
                );

            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken
                );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "LM Studio retornou HTTP {StatusCode}. " +
                    "Corpo: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody
                );

                return CriarFalha(
                    $"LM Studio retornou HTTP " +
                    $"{(int)response.StatusCode}.",
                    responseBody
                );
            }

            var lmStudioResponse =
                JsonSerializer.Deserialize<LmStudioChatResponse>(
                    responseBody,
                    JsonOptions
                );

            if (lmStudioResponse is null)
            {
                return CriarFalha(
                    "Não foi possível interpretar a resposta " +
                    "do LM Studio.",
                    responseBody
                );
            }

            var escolha =
                lmStudioResponse.Choices.FirstOrDefault();

            if (escolha is null)
            {
                return CriarFalha(
                    "O LM Studio não retornou nenhuma escolha.",
                    responseBody
                );
            }

            var conteudo =
                escolha.Message?.Content;

            /*
             * Alguns modelos executados pelo LM Studio podem retornar
             * o JSON final em reasoning_content e deixar content vazio.
             *
             * Por segurança, reasoning_content só é utilizado como fallback
             * quando ele contém um objeto JSON válido.
             */
            if (string.IsNullOrWhiteSpace(conteudo) &&
                IsValidJsonObject(
                    escolha.Message?.ReasoningContent
                ))
            {
                conteudo =
                    escolha.Message!.ReasoningContent;

                _logger.LogWarning(
                    "O LM Studio retornou content vazio. " +
                    "Foi utilizado reasoning_content porque ele " +
                    "continha um objeto JSON válido."
                );
            }

            if (string.IsNullOrWhiteSpace(conteudo))
            {
                return CriarFalha(
                    "O LM Studio retornou uma resposta sem conteúdo JSON válido.",
                    responseBody
                );
            }

            conteudo = conteudo.Trim();

            _logger.LogInformation(
                "Resposta recebida do LM Studio. " +
                "Modelo: {Modelo}. " +
                "Tokens de entrada: {TokensEntrada}. " +
                "Tokens de saída: {TokensSaida}.",
                lmStudioResponse.Model,
                lmStudioResponse.Usage?.PromptTokens ?? 0,
                lmStudioResponse.Usage?.CompletionTokens ?? 0
            );

            return new LlmResponse
            {
                Sucesso = true,

                Conteudo = conteudo,

                Modelo =
                    string.IsNullOrWhiteSpace(lmStudioResponse.Model)
                        ? _options.Modelo
                        : lmStudioResponse.Model,

                Provider = Nome,

                MotivoFinalizacao =
                    escolha.FinishReason,

                /*
                 * A API compatível com OpenAI não retorna os mesmos
                 * tempos em nanossegundos que a API do Ollama.
                 */
                DuracaoTotalNanosegundos = 0,

                DuracaoCarregamentoNanosegundos = 0,

                TokensEntrada =
                    lmStudioResponse.Usage?.PromptTokens ?? 0,

                TokensSaida =
                    lmStudioResponse.Usage?.CompletionTokens ?? 0
            };
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Timeout ao consultar o LM Studio após " +
                "{TimeoutSegundos} segundos.",
                _options.TimeoutSegundos
            );

            return CriarFalha(
                $"Timeout ao consultar o LM Studio após " +
                $"{_options.TimeoutSegundos} segundos."
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Falha de comunicação com o LM Studio em " +
                "{BaseUrl}.",
                _options.BaseUrl
            );

            return CriarFalha(
                "Não foi possível conectar ao LM Studio.",
                ex.Message
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Resposta inválida recebida do LM Studio."
            );

            return CriarFalha(
                "A resposta do LM Studio não contém JSON válido.",
                ex.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro inesperado ao consultar o LM Studio."
            );

            return CriarFalha(
                "Erro inesperado ao consultar o LM Studio.",
                ex.Message
            );
        }
    }

    private LmStudioChatRequest CriarPayload(
        LlmRequest request)
    {
        return new LmStudioChatRequest
        {
            Model = _options.Modelo,

            Stream = false,

            Temperature =
                request.Temperatura ??
                _options.Temperatura,


            Messages = new List<LmStudioChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = request.SystemPrompt
                },
                new()
                {
                    Role = "user",
                    Content = request.UserPrompt
                }
            },

            /*
             * O LM Studio aceita JSON Schema usando o padrão
             * OpenAI Structured Outputs.
             */
            ResponseFormat =
                request.JsonSchema.HasValue
                    ? new LmStudioResponseFormat
                    {
                        Type = "json_schema",

                        JsonSchema =
                            new LmStudioJsonSchema
                            {
                                Name =
                                    "interpretacao_anomalia",

                                Strict = true,

                                Schema =
                                    request.JsonSchema.Value
                            }
                    }
                    : new LmStudioResponseFormat
                    {
                        Type = "json_object"
                    }
        };
    }

    private static bool IsValidJsonObject(
        string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        try
        {
            using var documento =
                JsonDocument.Parse(valor);

            return documento.RootElement.ValueKind ==
                   JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private LlmResponse CriarFalha(
        string mensagem,
        string? detalhe = null)
    {
        return new LlmResponse
        {
            Sucesso = false,

            Provider = Nome,

            Modelo = _options.Modelo,

            Erro = string.IsNullOrWhiteSpace(detalhe)
                ? mensagem
                : $"{mensagem} Detalhe: {detalhe}"
        };
    }

    /*
     * Classes do corpo da requisição.
     */

    private sealed class LmStudioChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<LmStudioChatMessage> Messages { get; set; } =
            new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }


        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("response_format")]
        public LmStudioResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class LmStudioChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class LmStudioResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("json_schema")]
        public LmStudioJsonSchema? JsonSchema { get; set; }
    }

    private sealed class LmStudioJsonSchema
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("strict")]
        public bool Strict { get; set; }

        [JsonPropertyName("schema")]
        public JsonElement Schema { get; set; }
    }

    /*
     * Classes da resposta do LM Studio.
     */

    private sealed class LmStudioChatResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("choices")]
        public List<LmStudioChoice> Choices { get; set; } =
            new();

        [JsonPropertyName("usage")]
        public LmStudioUsage? Usage { get; set; }
    }

    private sealed class LmStudioChoice
    {
        [JsonPropertyName("message")]
        public LmStudioResponseMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class LmStudioResponseMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; }
    }

    private sealed class LmStudioUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}