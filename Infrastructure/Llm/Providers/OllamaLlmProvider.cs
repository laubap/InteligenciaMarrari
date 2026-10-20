using System.Net.Http.Json;
using System.Text.Json;
using IAApi.Infrastructure.Llm.Configuration;
using IAApi.Infrastructure.Llm.Models;
using Microsoft.Extensions.Options;
using IAApi.Application.Llm.Contracts;
using Microsoft.Extensions.Logging;

namespace IAApi.Infrastructure.Llm.Providers;

public sealed class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OllamaLlmProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaLlmProvider(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<OllamaLlmProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Nome => "Ollama";

    public async Task<LlmResponse> GerarAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Habilitado)
        {
            return CriarFalha("O provedor de LLM está desabilitado.");
        }

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            return CriarFalha("O prompt de sistema não foi informado.");
        }

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return CriarFalha("O prompt do usuário não foi informado.");
        }

        var payload = new OllamaChatRequest
        {
            Model = _options.Modelo,
            Stream = false,
            KeepAlive = _options.KeepAlive,
            Format = request.JsonSchema,
            Options = new OllamaOptions
            {
                Temperature = request.Temperatura ?? _options.Temperatura,
                NumPredict = request.MaxTokens ?? _options.MaxTokens
            },
            Messages = new List<LlmChatMessage>
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
            }
        };

        try
        {
            _logger.LogInformation(
                "Enviando solicitação para o Ollama. Modelo: {Modelo}",
                _options.Modelo
            );

            using var response = await _httpClient.PostAsJsonAsync(
                "/api/chat",
                payload,
                JsonOptions,
                cancellationToken
            );

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Ollama retornou HTTP {StatusCode}. Corpo: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody
                );

                return CriarFalha(
                    $"Ollama retornou HTTP {(int)response.StatusCode}.",
                    responseBody
                );
            }

            var ollamaResponse =
                JsonSerializer.Deserialize<OllamaChatResponse>(
                    responseBody,
                    JsonOptions
                );

            if (ollamaResponse is null)
            {
                return CriarFalha(
                    "Não foi possível interpretar a resposta do Ollama.",
                    responseBody
                );
            }

            if (string.IsNullOrWhiteSpace(ollamaResponse.Message.Content))
            {
                return CriarFalha(
                    "O Ollama retornou uma resposta sem conteúdo.",
                    responseBody
                );
            }

            _logger.LogInformation(
                "Resposta recebida do Ollama. Modelo: {Modelo}. " +
                "Tokens de entrada: {TokensEntrada}. " +
                "Tokens de saída: {TokensSaida}.",
                ollamaResponse.Model,
                ollamaResponse.PromptEvalCount,
                ollamaResponse.EvalCount
            );

            return new LlmResponse
            {
                Sucesso = true,
                Conteudo = ollamaResponse.Message.Content,
                Modelo = ollamaResponse.Model,
                Provider = Nome,
                MotivoFinalizacao = ollamaResponse.DoneReason,
                DuracaoTotalNanosegundos = ollamaResponse.TotalDuration,
                DuracaoCarregamentoNanosegundos =
                    ollamaResponse.LoadDuration,
                TokensEntrada = ollamaResponse.PromptEvalCount,
                TokensSaida = ollamaResponse.EvalCount
            };
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                "Timeout ao consultar o Ollama após {TimeoutSegundos} segundos.",
                _options.TimeoutSegundos
            );

            return CriarFalha(
                $"Timeout ao consultar o Ollama após " +
                $"{_options.TimeoutSegundos} segundos."
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Falha de comunicação com o Ollama em {BaseUrl}.",
                _options.BaseUrl
            );

            return CriarFalha(
                "Não foi possível conectar ao Ollama.",
                ex.Message
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Resposta inválida recebida do Ollama."
            );

            return CriarFalha(
                "A resposta do Ollama não contém JSON válido.",
                ex.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro inesperado ao consultar o Ollama."
            );

            return CriarFalha(
                "Erro inesperado ao consultar o Ollama.",
                ex.Message
            );
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
}