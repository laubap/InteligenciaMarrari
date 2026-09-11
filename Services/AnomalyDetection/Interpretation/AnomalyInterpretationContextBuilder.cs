using IAApi.Services;
using System.Text.Json;

namespace IAApi.Services.AnomalyDetection.Interpretation;

public class AnomalyInterpretationContextBuilder
{
    private readonly TagContextoIaService _tagContextoIaService;

    public AnomalyInterpretationContextBuilder(
        TagContextoIaService tagContextoIaService)
    {
        _tagContextoIaService = tagContextoIaService;
    }

    public async Task<AnomalyInterpretationContext> CriarAsync(
        DetectorInput input,
        DetectorResult resultadoEnsemble,
        double limiteEnsemble,
        bool ehAnomaliaOperacional,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resultadoEnsemble);

        var contextoTag = await _tagContextoIaService.ObterAsync(
            input.ClienteId,
            input.TagName,
            cancellationToken);

        return new AnomalyInterpretationContext
        {
            ClienteId = input.ClienteId,
            TagName = input.TagName,

            ValorAtual = input.ValorAtual,
            DataHoraAtual = input.DataHoraAtual,
            MediaHistorica = input.MediaHistorica,
            DesvioPadraoHistorico = input.DesvioPadraoHistorico,

            EhAnomalia = ehAnomaliaOperacional,
            ScoreNormalizado = resultadoEnsemble.ScoreNormalizado,

            Severidade = AnomalySeverityClassifier.Classificar(
                resultadoEnsemble.ScoreNormalizado,
                limiteEnsemble),

            ExplicacaoEnsemble = resultadoEnsemble.Explicacao,

            MetricasGerais = resultadoEnsemble.Metricas
                ?? new Dictionary<string, double>(),

            Detectores = resultadoEnsemble.DetalhesDetectores
                ?? new List<DetectorDebugItem>(),

            ContextoDisponivel = contextoTag != null,

            ContextoCompleto =
                contextoTag?.ContextoCompleto ?? false,

            TipoRepresentacao =
                contextoTag?.TipoRepresentacao ?? "",

            DescricaoTag =
                contextoTag?.Descricao ?? "",

            Criticidade =
                contextoTag?.Criticidade ?? "",

            Equipamento =
                contextoTag?.Equipamento ?? "",

            Area =
                contextoTag?.Area ?? "",

            ObservacaoModoOperacao =
                contextoTag?.ObservacaoModoOperacao ?? "",

            Impactos =
                DesserializarLista(contextoTag?.Impactos),

            ModosOperacao =
                DesserializarLista(contextoTag?.ModosOperacao),

            CausasProvaveis =
                DesserializarLista(contextoTag?.CausasProvaveis),

            AcoesRecomendadas =
                DesserializarLista(contextoTag?.AcoesRecomendadas)
        };
    }

    private static List<string> DesserializarLista(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                   ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}