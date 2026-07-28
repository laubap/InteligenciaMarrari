using System.Text.Json;
using IAApi.Infrastructure.Llm.Models;
using IAApi.Infrastructure.Llm.Providers;
using IAApi.Services.AnomalyDetection.Interpretation.Builders;

namespace IAApi.Services.AnomalyDetection.Interpretation;

public sealed class LocalLlmAnomalyInterpretationService
    : IAnomalyInterpretationService
{
    private readonly ILlmProvider _llmProvider;

    private readonly RuleBasedAnomalyInterpretationService
        _ruleBasedService;

    private readonly ILogger<LocalLlmAnomalyInterpretationService>
        _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public LocalLlmAnomalyInterpretationService(
        ILlmProvider llmProvider,
        RuleBasedAnomalyInterpretationService ruleBasedService,
        ILogger<LocalLlmAnomalyInterpretationService> logger)
    {
        _llmProvider = llmProvider;
        _ruleBasedService = ruleBasedService;
        _logger = logger;
    }

    public async Task<AnomalyInterpretationResult> InterpretarAsync(
        AnomalyInterpretationContext contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var request = CriarRequisicao(contexto);

            _logger.LogInformation(
                "Iniciando interpretação da tag {TagName} com o " +
                "provedor {Provider}.",
                contexto.TagName,
                _llmProvider.Nome
            );

            var respostaLlm =
                await _llmProvider.GerarAsync(
                    request,
                    cancellationToken
                );

            if (!respostaLlm.Sucesso)
            {
                _logger.LogWarning(
                    "A interpretação por LLM falhou para a tag " +
                    "{TagName}. Erro: {Erro}. Será utilizado o " +
                    "fallback baseado em regras.",
                    contexto.TagName,
                    respostaLlm.Erro
                );

                return await UsarFallbackAsync(
                    contexto,
                    cancellationToken
                );
            }

            _logger.LogInformation(
                """
==================== RESPOSTA BRUTA DA IA ====================
{Conteudo}
==============================================================
""",
                respostaLlm.Conteudo
            );

            var resultado =
                DesserializarResposta(
                    respostaLlm.Conteudo,
                    respostaLlm.Modelo
                );

            if (resultado is null)
            {
                _logger.LogWarning(
                    "A resposta da LLM para a tag {TagName} não pôde " +
                    "ser desserializada. Será utilizado o fallback " +
                    "baseado em regras.",
                    contexto.TagName
                );

                return await UsarFallbackAsync(
                    contexto,
                    cancellationToken
                );
            }

            if (!ValidarResultado(resultado, contexto))
            {
                _logger.LogWarning(
                    "A resposta da LLM para a tag {TagName} não passou " +
                    "pela validação. Será utilizado o fallback baseado " +
                    "em regras.",
                    contexto.TagName
                );

                return await UsarFallbackAsync(
                    contexto,
                    cancellationToken
                );
            }

            NormalizarResultado(
                resultado,
                contexto,
                respostaLlm
            );

            _logger.LogInformation(
                "Interpretação da tag {TagName} gerada com sucesso " +
                "pelo modelo {Modelo}. Tokens de entrada: " +
                "{TokensEntrada}. Tokens de saída: {TokensSaida}.",
                contexto.TagName,
                respostaLlm.Modelo,
                respostaLlm.TokensEntrada,
                respostaLlm.TokensSaida
            );

            return resultado;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "A interpretação da tag {TagName} foi cancelada.",
                contexto.TagName
            );

            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "A LLM retornou um JSON inválido para a tag " +
                "{TagName}. Será utilizado o fallback baseado em regras.",
                contexto.TagName
            );

            return await UsarFallbackAsync(
                contexto,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro inesperado durante a interpretação da tag " +
                "{TagName}. Será utilizado o fallback baseado em regras.",
                contexto.TagName
            );

            return await UsarFallbackAsync(
                contexto,
                cancellationToken
            );
        }
    }

    private static LlmRequest CriarRequisicao(
        AnomalyInterpretationContext contexto)
    {
        return new LlmRequest
        {
            SystemPrompt =
                PromptBuilder.BuildSystemPrompt(),

            UserPrompt =
                PromptBuilder.BuildUserPrompt(contexto),

            JsonSchema =
                JsonSchemaBuilder.Build(),

            Temperatura = null,

            MaxTokens = 700
        };
    }

    private static AnomalyInterpretationResult?
        DesserializarResposta(
            string conteudo,
            string modelo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return null;
        }

        var jsonLimpo =
            LimparConteudoJson(conteudo);

        var resposta =
            JsonSerializer.Deserialize<LlmInterpretationResponse>(
                jsonLimpo,
                JsonOptions
            );

        if (resposta is null)
        {
            return null;
        }

        return new AnomalyInterpretationResult
        {
            Resumo =
                resposta.Resumo?.Trim() ?? "",

            InterpretacaoTecnica =
                resposta.InterpretacaoTecnica?.Trim() ?? "",

            Evidencias =
                LimparLista(resposta.Evidencias),

            PossiveisCausas =
                LimparLista(resposta.PossiveisCausas),

            Recomendacoes =
                LimparLista(resposta.Recomendacoes),

            Severidade =
                resposta.Severidade?.Trim() ?? "",

            GeradoPorLlm = true,

            ModeloUtilizado =
                string.IsNullOrWhiteSpace(modelo)
                    ? "Modelo local via Ollama"
                    : modelo,

            DataGeracao = DateTime.UtcNow
        };
    }

    private static string LimparConteudoJson(
        string conteudo)
    {
        var texto = conteudo.Trim();

        if (texto.StartsWith("```json",
                StringComparison.OrdinalIgnoreCase))
        {
            texto = texto[7..];
        }
        else if (texto.StartsWith("```",
                     StringComparison.OrdinalIgnoreCase))
        {
            texto = texto[3..];
        }

        if (texto.EndsWith("```",
                StringComparison.OrdinalIgnoreCase))
        {
            texto = texto[..^3];
        }

        return texto.Trim();
    }

    private static bool ValidarResultado(
        AnomalyInterpretationResult resultado,
        AnomalyInterpretationContext contexto)
    {
        if (string.IsNullOrWhiteSpace(resultado.Resumo))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                resultado.InterpretacaoTecnica))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                resultado.Severidade))
        {
            return false;
        }

        if (!SeveridadesEquivalentes(
                resultado.Severidade,
                contexto.Severidade))
        {
            return false;
        }

        if (resultado.Evidencias is null)
        {
            return false;
        }

        if (resultado.PossiveisCausas is null)
        {
            return false;
        }

        if (resultado.Recomendacoes is null)
        {
            return false;
        }

        return true;
    }

    private static bool SeveridadesEquivalentes(
        string severidadeLlm,
        string severidadeCalculada)
    {
        var primeira =
            NormalizarTexto(severidadeLlm);

        var segunda =
            NormalizarTexto(severidadeCalculada);

        return string.Equals(
            primeira,
            segunda,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static string NormalizarTexto(
        string valor)
    {
        return valor
            .Trim()
            .ToLowerInvariant();
    }

    private static void NormalizarResultado(
        AnomalyInterpretationResult resultado,
        AnomalyInterpretationContext contexto,
        LlmResponse respostaLlm)
    {
        resultado.Resumo =
            resultado.Resumo.Trim();

        resultado.InterpretacaoTecnica =
            resultado.InterpretacaoTecnica.Trim();

        resultado.Evidencias =
            LimparLista(resultado.Evidencias);

        resultado.PossiveisCausas =
            LimparLista(resultado.PossiveisCausas);

        resultado.Recomendacoes =
            LimparLista(resultado.Recomendacoes);

        /*
         * A LLM não pode mudar a severidade calculada pelo sistema.
         * Mesmo após a validação, o valor oficial é sempre o contexto.
         */
        resultado.Severidade =
            contexto.Severidade;

        resultado.GeradoPorLlm = true;

        resultado.ModeloUtilizado =
            string.IsNullOrWhiteSpace(respostaLlm.Modelo)
                ? $"{respostaLlm.Provider} - modelo local"
                : $"{respostaLlm.Provider} - " +
                  $"{respostaLlm.Modelo}";

        resultado.DataGeracao =
            DateTime.UtcNow;

        /*
         * Quando não há anomalia, possíveis causas normalmente não
         * fazem sentido. A decisão matemática continua vindo dos
         * detectores, não da LLM.
         */
        if (!contexto.EhAnomalia)
        {
            resultado.PossiveisCausas =
                new List<string>();

            if (resultado.Recomendacoes.Count == 0)
            {
                resultado.Recomendacoes.Add(
                    "Continuar monitorando o comportamento da tag."
                );
            }
        }
    }

    private async Task<AnomalyInterpretationResult>
        UsarFallbackAsync(
            AnomalyInterpretationContext contexto,
            CancellationToken cancellationToken)
    {
        var resultado =
            await _ruleBasedService.InterpretarAsync(
                contexto,
                cancellationToken
            );

        resultado.GeradoPorLlm = false;

        resultado.ModeloUtilizado =
            "Interpretação baseada em regras";

        resultado.DataGeracao =
            DateTime.UtcNow;

        return resultado;
    }

    private static List<string> LimparLista(
        IEnumerable<string>? itens)
    {
        if (itens is null)
        {
            return new List<string>();
        }

        return itens
            .Where(item =>
                !string.IsNullOrWhiteSpace(item))
            .Select(item =>
                item.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class LlmInterpretationResponse
    {
        public string? Resumo { get; set; }

        public string? InterpretacaoTecnica { get; set; }

        public List<string>? Evidencias { get; set; }

        public List<string>? PossiveisCausas { get; set; }

        public List<string>? Recomendacoes { get; set; }

        public string? Severidade { get; set; }
    }
}