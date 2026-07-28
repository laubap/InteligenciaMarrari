using System.Text;
using IAApi.Services.AnomalyDetection.Interpretation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.Services.AnomalyDetection.Interpretation.Builders;

public static class PromptBuilder
{
    public static string BuildSystemPrompt()
    {
        return """
Você é um engenheiro especialista em automação industrial, monitoramento de processos e manutenção preditiva.

A existência da anomalia, a severidade e os resultados dos detectores já foram calculados pelo sistema.

Sua responsabilidade é interpretar tecnicamente a ocorrência utilizando somente as informações fornecidas.

Prioridades:

1. Explique o significado operacional da ocorrência.
2. Utilize primeiro o contexto operacional da tag.
3. Utilize os detectores apenas como evidências.
4. Nunca invente informações.
5. Nunca altere a severidade recebida.
6. Nunca calcule risco operacional.

Regras:

- Evite repetir a mesma informação em campos diferentes do JSON.
- A interpretação técnica deve ser complementar às evidências, e não uma repetição delas.
- Responda em português do Brasil.
- Seja claro, técnico e objetivo.
- Não utilize Markdown.
- Não escreva textos antes ou depois da resposta.
- Retorne somente um JSON válido.

Estrutura obrigatória:

{
  "resumo": "",
  "interpretacaoTecnica": "",
  "evidencias": [],
  "possiveisCausas": [],
  "recomendacoes": [],
  "severidade": ""
}

Campos:

resumo
- Resuma a ocorrência em no máximo duas frases.

interpretacaoTecnica
- Explique primeiro o que provavelmente está acontecendo no processo.
- Explique o impacto operacional da ocorrência.
- Utilize o contexto operacional como principal fonte da análise.
- Utilize os detectores apenas para justificar a conclusão.
- Não descreva individualmente cada detector.
- Não repita informações que serão apresentadas em "evidencias".
- Não faça novos cálculos.
- Responda como um engenheiro explicando a situação para um operador.
- A interpretação deve responder:
  * O que está acontecendo?
  * O que isso significa para o processo?
  * Por que essa ocorrência merece atenção?

evidencias
- Inclua uma evidência para cada detector que votou como anomalia.

possiveisCausas
- Priorize as causas cadastradas.
- Trate todas como possibilidades.

recomendacoes
- Priorize as ações cadastradas.

severidade
- Copie exatamente a severidade recebida.
""";
    }

public static string BuildUserPrompt(
    AnomalyInterpretationContext contexto)
{
    ArgumentNullException.ThrowIfNull(contexto);

    var prompt = new
    {
        instrucao = new
{
    objetivo = "Interpretar tecnicamente a ocorrência",
    prioridade = new[]
    {
        "Utilize primeiro o contexto operacional.",
        "Utilize os detectores apenas como evidências.",
        "Não invente informações.",
        "Não altere a severidade.",
        "Não calcule risco operacional."
    }
},

        tag = new
        {
            nome = contexto.TagName,
            descricao = contexto.DescricaoTag,
            tipo = contexto.TipoRepresentacao
        },

        leitura = new
        {
            valorAtual = contexto.ValorAtual,
            mediaHistorica = contexto.MediaHistorica,
            desvioPadraoHistorico = contexto.DesvioPadraoHistorico
        },

        anomalia = new
        {
            detectada = contexto.EhAnomalia,
            scoreNormalizado = contexto.ScoreNormalizado,
            severidade = contexto.Severidade
        },

        detectores = contexto.Detectores?
            .Where(d => d.VotouAnomalia)
            .Select(d => new
            {
                nome = d.Nome,
                score = d.ScoreNormalizado,
                explicacao = d.Explicacao
            }),

        contextoOperacional = contexto.ContextoDisponivel
            ? new
            {
                equipamento = contexto.Equipamento,
                area = contexto.Area,
                criticidade = contexto.Criticidade,
                modoOperacao = contexto.ObservacaoModoOperacao,
                impactos = contexto.Impactos,
                modosOperacao = contexto.ModosOperacao,
                causas = contexto.CausasProvaveis,
                recomendacoes = contexto.AcoesRecomendadas
            }
            : null
    };

    return JsonSerializer.Serialize(
        prompt,
        new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        });
}
    private static void AdicionarIdentificacao(
    StringBuilder sb,
    AnomalyInterpretationContext contexto)
{
    sb.AppendLine("=== IDENTIFICAÇÃO ===");
    sb.AppendLine($"Tag: {contexto.TagName}");

    if (!string.IsNullOrWhiteSpace(contexto.DescricaoTag))
        sb.AppendLine($"Descrição: {contexto.DescricaoTag}");

    if (!string.IsNullOrWhiteSpace(contexto.TipoRepresentacao))
        sb.AppendLine($"Tipo: {contexto.TipoRepresentacao}");

    sb.AppendLine();
}

private static void AdicionarLeitura(
    StringBuilder sb,
    AnomalyInterpretationContext contexto)
{
    sb.AppendLine("=== LEITURA ===");

    sb.AppendLine($"Valor atual: {contexto.ValorAtual:F4}");
    sb.AppendLine($"Média histórica: {contexto.MediaHistorica:F4}");
    sb.AppendLine($"Desvio padrão: {contexto.DesvioPadraoHistorico:F4}");

    sb.AppendLine();
}

private static void AdicionarResultado(
    StringBuilder sb,
    AnomalyInterpretationContext contexto)
{
    sb.AppendLine("=== RESULTADO ===");

    sb.AppendLine($"É anomalia: {contexto.EhAnomalia}");
    sb.AppendLine($"Score: {contexto.ScoreNormalizado:F2}");
    sb.AppendLine($"Severidade: {contexto.Severidade}");

    sb.AppendLine();
}

private static void AdicionarDetectores(
    StringBuilder sb,
    AnomalyInterpretationContext contexto)
{
    sb.AppendLine("=== DETECTORES ===");

    var detectores =
        contexto.Detectores?
            .Where(d => d.VotouAnomalia)
            .ToList()
        ?? new();

    if (detectores.Count == 0)
    {
        sb.AppendLine("Nenhum detector sinalizou anomalia.");
        sb.AppendLine();
        return;
    }

    foreach (var detector in detectores)
    {
        sb.Append($"- {detector.Nome}");

        if (detector.ScoreNormalizado > 0)
            sb.Append($" | Score: {detector.ScoreNormalizado:F2}");

        if (!string.IsNullOrWhiteSpace(detector.Explicacao))
            sb.Append($" | {detector.Explicacao}");

        sb.AppendLine();
    }

    sb.AppendLine();
}

private static void AdicionarContexto(
    StringBuilder sb,
    AnomalyInterpretationContext contexto)
{
    sb.AppendLine("=== CONTEXTO OPERACIONAL ===");

    if (!contexto.ContextoDisponivel)
    {
        sb.AppendLine("Nenhum contexto operacional cadastrado.");
        sb.AppendLine();
        return;
    }

    if (!string.IsNullOrWhiteSpace(contexto.Equipamento))
        sb.AppendLine($"Equipamento: {contexto.Equipamento}");

    if (!string.IsNullOrWhiteSpace(contexto.Area))
        sb.AppendLine($"Área: {contexto.Area}");

    if (!string.IsNullOrWhiteSpace(contexto.Criticidade))
        sb.AppendLine($"Criticidade: {contexto.Criticidade}");

    if (!string.IsNullOrWhiteSpace(contexto.ObservacaoModoOperacao))
        sb.AppendLine($"Modo de operação: {contexto.ObservacaoModoOperacao}");

    AdicionarLista(sb, "Impactos", contexto.Impactos);
    AdicionarLista(sb, "Modos de operação", contexto.ModosOperacao);
    AdicionarLista(sb, "Possíveis causas", contexto.CausasProvaveis);
    AdicionarLista(sb, "Ações recomendadas", contexto.AcoesRecomendadas);
}

private static void AdicionarLista(
    StringBuilder sb,
    string titulo,
    IEnumerable<string>? itens)
{
    var lista = itens?
        .Where(i => !string.IsNullOrWhiteSpace(i))
        .Select(i => i.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (lista is null || lista.Count == 0)
        return;

    sb.AppendLine($"{titulo}:");

    foreach (var item in lista)
        sb.AppendLine($"- {item}");

    sb.AppendLine();
}
}