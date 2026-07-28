using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

namespace IAApi.Services.AnomalyDetection.Interpretation;

public class OpenAiAnomalyInterpretationService
    : IAnomalyInterpretationService
{
    private readonly OpenAiOptions _options;
    private readonly RuleBasedAnomalyInterpretationService _fallback;
    private readonly ILogger<OpenAiAnomalyInterpretationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenAiAnomalyInterpretationService(
        IOptions<OpenAiOptions> options,
        RuleBasedAnomalyInterpretationService fallback,
        ILogger<OpenAiAnomalyInterpretationService> logger)
    {
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<AnomalyInterpretationResult> InterpretarAsync(
        AnomalyInterpretationContext contexto,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Habilitado)
        {
            _logger.LogInformation(
                "Interpretação por OpenAI desabilitada. Usando regras."
            );

            return await _fallback.InterpretarAsync(
                contexto,
                cancellationToken
            );
        }

        var apiKey = Environment.GetEnvironmentVariable(
            "OPENAI_API_KEY"
        );

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "A variável OPENAI_API_KEY não foi encontrada. " +
                "Usando interpretação baseada em regras."
            );

            return await _fallback.InterpretarAsync(
                contexto,
                cancellationToken
            );
        }

        try
        {
            var client = new ChatClient(
                model: _options.Modelo,
                apiKey: apiKey
            );

            var mensagens = new List<ChatMessage>
            {
                new SystemChatMessage(CriarInstrucaoSistema()),
                new UserChatMessage(CriarPrompt(contexto))
            };

            var opcoes = new ChatCompletionOptions
            {
                ResponseFormat =
                    ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName:
                            "interpretacao_anomalia",

                        jsonSchema:
                            BinaryData.FromBytes(
                                CriarJsonSchema()
                            ),

                        jsonSchemaIsStrict: true
                    )
            };

            var resposta = await client.CompleteChatAsync(
                mensagens,
                opcoes,
                cancellationToken
            );

            var conteudo = resposta.Value.Content
                .FirstOrDefault()?
                .Text;

            if (string.IsNullOrWhiteSpace(conteudo))
            {
                throw new InvalidOperationException(
                    "A OpenAI retornou uma resposta vazia."
                );
            }

            var resultadoLlm =
                JsonSerializer.Deserialize<OpenAiInterpretationResponse>(
                    conteudo,
                    JsonOptions
                );

            if (resultadoLlm is null)
            {
                throw new InvalidOperationException(
                    "Não foi possível converter a resposta da OpenAI."
                );
            }

            return new AnomalyInterpretationResult
            {
                Resumo = resultadoLlm.Resumo,

                InterpretacaoTecnica =
                    resultadoLlm.InterpretacaoTecnica,

                Evidencias =
                    resultadoLlm.Evidencias ?? new(),

                PossiveisCausas =
                    resultadoLlm.PossiveisCausas ?? new(),

                Recomendacoes =
                    resultadoLlm.Recomendacoes ?? new(),

                Severidade =
                    contexto.Severidade,

                GeradoPorLlm = true,

                ModeloUtilizado =
                    _options.Modelo,

                DataGeracao =
                    DateTime.UtcNow
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao gerar interpretação com OpenAI. " +
                "Usando serviço baseado em regras."
            );

            return await _fallback.InterpretarAsync(
                contexto,
                cancellationToken
            );
        }
    }

    private static string CriarInstrucaoSistema()
    {
        return """
        Você é um assistente técnico especializado em monitoramento
        industrial, confiabilidade e manutenção preditiva.

        Sua função é explicar resultados que já foram calculados por
        detectores estatísticos e modelos de machine learning.

        Regras obrigatórias:

        1. Não altere a decisão de anomalia recebida.
        2. Não invente valores, causas, equipamentos ou procedimentos.
        3. Use somente as informações fornecidas no contexto.
        4. Diferencie evidências técnicas de causas possíveis.
        5. Trate causas cadastradas como hipóteses, não como diagnóstico.
        6. Não prometa tempo restante até uma falha.
        7. Não afirme que uma falha ocorrerá com certeza.
        8. Produza texto claro, profissional e em português do Brasil.
        9. Recomendações devem priorizar verificação segura por
           profissional responsável.
        10. Não mencione que você é uma inteligência artificial.

        Retorne somente o objeto JSON solicitado.
        """;
    }

    private static string CriarPrompt(
        AnomalyInterpretationContext contexto)
    {
        var detectores = contexto.Detectores
            .Select(detector => new
            {
                nome = detector.Nome,
                aplicavel = detector.Aplicavel,
                votouAnomalia = detector.VotouAnomalia,
                scoreNormalizado = detector.ScoreNormalizado,
                scoreOriginal = detector.ScoreOriginal,
                peso = detector.Peso,
                explicacao = detector.Explicacao,
                metricas = detector.Metricas
            })
            .ToList();

        var dados = new
        {
            identificacao = new
            {
                clienteId = contexto.ClienteId,
                tagName = contexto.TagName,
                dataHoraAtual = contexto.DataHoraAtual
            },

            resultadoDeteccao = new
            {
                ehAnomalia = contexto.EhAnomalia,
                scoreNormalizado = contexto.ScoreNormalizado,
                severidade = contexto.Severidade,
                explicacaoEnsemble =
                    contexto.ExplicacaoEnsemble
            },

            perfilEstatistico = new
            {
                valorAtual = contexto.ValorAtual,
                mediaHistorica = contexto.MediaHistorica,
                desvioPadraoHistorico =
                    contexto.DesvioPadraoHistorico,
                metricasGerais =
                    contexto.MetricasGerais
            },

            contextoOperacional = new
            {
                contextoDisponivel =
                    contexto.ContextoDisponivel,

                contextoCompleto =
                    contexto.ContextoCompleto,

                tipoRepresentacao =
                    contexto.TipoRepresentacao,

                descricaoTag =
                    contexto.DescricaoTag,

                criticidade =
                    contexto.Criticidade,

                equipamento =
                    contexto.Equipamento,

                area =
                    contexto.Area,

                observacaoModoOperacao =
                    contexto.ObservacaoModoOperacao,

                impactos =
                    contexto.Impactos,

                modosOperacao =
                    contexto.ModosOperacao,

                causasProvaveis =
                    contexto.CausasProvaveis,

                acoesRecomendadas =
                    contexto.AcoesRecomendadas
            },

            detectores
        };

        var jsonContexto = JsonSerializer.Serialize(
            dados,
            new JsonSerializerOptions
            {
                WriteIndented = true
            }
        );

        var prompt = new StringBuilder();

        prompt.AppendLine(
            "Analise o resultado de detecção abaixo."
        );

        prompt.AppendLine();

        prompt.AppendLine(
            "A decisão de anomalia e a severidade já foram calculadas " +
            "pelo sistema e não devem ser modificadas."
        );

        prompt.AppendLine();

        prompt.AppendLine(
            "Produza:"
        );

        prompt.AppendLine(
            "- resumo: síntese objetiva para o operador;"
        );

        prompt.AppendLine(
            "- interpretacaoTecnica: explicação técnica do comportamento;"
        );

        prompt.AppendLine(
            "- evidencias: principais sinais que sustentam a análise;"
        );

        prompt.AppendLine(
            "- possiveisCausas: somente hipóteses fundamentadas nos dados;"
        );

        prompt.AppendLine(
            "- recomendacoes: ações seguras e práticas."
        );

        prompt.AppendLine();

        prompt.AppendLine(
            "Não repita o mesmo detector ou a mesma evidência."
        );

        prompt.AppendLine();

        prompt.AppendLine("CONTEXTO:");

        prompt.AppendLine(jsonContexto);

        return prompt.ToString();
    }

    private static byte[] CriarJsonSchema()
    {
        const string schema = """
        {
          "type": "object",
          "properties": {
            "resumo": {
              "type": "string"
            },
            "interpretacaoTecnica": {
              "type": "string"
            },
            "evidencias": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "possiveisCausas": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "recomendacoes": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          "required": [
            "resumo",
            "interpretacaoTecnica",
            "evidencias",
            "possiveisCausas",
            "recomendacoes"
          ],
          "additionalProperties": false
        }
        """;

        return Encoding.UTF8.GetBytes(schema);
    }

    private sealed class OpenAiInterpretationResponse
    {
        public string Resumo { get; set; } = "";

        public string InterpretacaoTecnica { get; set; } = "";

        public List<string> Evidencias { get; set; } = new();

        public List<string> PossiveisCausas { get; set; } = new();

        public List<string> Recomendacoes { get; set; } = new();
    }
}