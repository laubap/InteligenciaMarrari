using System.Text.Json;

namespace IAApi.Services.AnomalyDetection.Interpretation.Builders;

public static class JsonSchemaBuilder
{
    private static readonly JsonElement Schema =
        CriarSchema();

    public static JsonElement Build()
    {
        return Schema.Clone();
    }

    private static JsonElement CriarSchema()
    {
        const string schemaJson = """
{
  "type": "object",
  "properties": {
    "resumo": {
      "type": "string",
      "description": "Resumo objetivo da ocorrência industrial."
    },
    "interpretacaoTecnica": {
      "type": "string",
      "description": "Interpretação técnica do comportamento detectado."
    },
    "evidencias": {
      "type": "array",
      "description": "Evidências que sustentam a interpretação.",
      "items": {
        "type": "string"
      }
    },
    "possiveisCausas": {
      "type": "array",
      "description": "Possíveis causas da ocorrência, sem afirmar diagnóstico definitivo.",
      "items": {
        "type": "string"
      }
    },
    "recomendacoes": {
      "type": "array",
      "description": "Ações recomendadas para investigação, monitoramento ou inspeção.",
      "items": {
        "type": "string"
      }
    },
    "severidade": {
      "type": "string",
      "description": "Severidade informada no contexto, sem alteração pela LLM."
    }
  },
  "required": [
    "resumo",
    "interpretacaoTecnica",
    "evidencias",
    "possiveisCausas",
    "recomendacoes",
    "severidade"
  ],
  "additionalProperties": false
}
""";

        using var document =
            JsonDocument.Parse(schemaJson);

        return document.RootElement.Clone();
    }
}