using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Interpretation;

public sealed class PredictionInterpretationService
{
    private readonly AnomalyInterpretationContextBuilder
        _interpretationContextBuilder;

    private readonly IAnomalyInterpretationService
        _anomalyInterpretationService;

    public PredictionInterpretationService(
        AnomalyInterpretationContextBuilder interpretationContextBuilder,
        IAnomalyInterpretationService anomalyInterpretationService)
    {
        _interpretationContextBuilder =
            interpretationContextBuilder;

        _anomalyInterpretationService =
            anomalyInterpretationService;
    }

    public async Task<AnomalyInterpretationResult?> InterpretarAsync(
        DetectorInput entradaDetector,
        DetectorResult resultadoDetector,
        RuntimeAnomalyDecision runtimeDecision,
        double limiteEnsembleInterpretacao,
        bool multivariatePromotionApplied,
        bool comportamentoReconhecido,
        string? classificacaoComportamentoReconhecido,
        string? codigoComportamentoReconhecido,
        CancellationToken cancellationToken = default)
    {
        if (multivariatePromotionApplied)
        {
            return null;
        }

        if (runtimeDecision.RequiresNewInterpretation)
        {
            var contextoInterpretacao =
                await _interpretationContextBuilder.CriarAsync(
                    entradaDetector,
                    resultadoDetector,
                    limiteEnsembleInterpretacao,
                    runtimeDecision.EmitAnomaly,
                    cancellationToken);

            return await _anomalyInterpretationService.InterpretarAsync(
                contextoInterpretacao,
                cancellationToken);
        }

        if (runtimeDecision.IsAnomalyContinuation)
        {
            var contextoContinuacao =
                await _interpretationContextBuilder.CriarAsync(
                    entradaDetector,
                    resultadoDetector,
                    limiteEnsembleInterpretacao,
                    runtimeDecision.EmitAnomaly,
                    cancellationToken);

            var increasingNoiseContinuation =
                string.Equals(
                    runtimeDecision.Source,
                    "IncreasingNoiseContinuation",
                    StringComparison.OrdinalIgnoreCase);

            return new AnomalyInterpretationResult
            {
                Resumo =
                    "A anomalia permanece ativa e está em acompanhamento.",

                InterpretacaoTecnica =
                    increasingNoiseContinuation
                        ? "A expansão sustentada da variabilidade permanece ativa. " +
                          "Esta leitura representa a continuidade da mesma condição " +
                          "já identificada, sem nova evidência funcional independente."
                        : "A condição anômala previamente identificada permanece ativa. " +
                          "Esta leitura é uma continuação e não representa uma nova " +
                          "evidência funcional independente.",

                Evidencias =
                    increasingNoiseContinuation
                        ? new List<string>
                        {
                            "A evidência de aumento sustentado de variabilidade permanece ativa.",
                            "Nenhuma nova interpretação foi iniciada para esta leitura de continuação."
                        }
                        : new List<string>
                        {
                            "A condição funcional de anomalia permanece ativa.",
                            "Nenhuma nova interpretação foi iniciada para esta leitura de continuação."
                        },

                PossiveisCausas =
                    new(),

                Recomendacoes =
                    new()
                    {
                        "Continuar acompanhando a evolução das próximas leituras."
                    },

                Severidade =
                    contextoContinuacao.Severidade,

                GeradoPorLlm =
                    false,

                ModeloUtilizado =
                    "Continuação determinística",

                DataGeracao =
                    DateTime.UtcNow
            };
        }

        var comportamentoAnormalConhecido =
            comportamentoReconhecido &&
            string.Equals(
                classificacaoComportamentoReconhecido,
                "KnownAbnormal",
                StringComparison.Ordinal);

        return new AnomalyInterpretationResult
        {
            Resumo =
                comportamentoAnormalConhecido
                    ? $"Comportamento anormal conhecido " +
                      $"{codigoComportamentoReconhecido ?? "-"} reconhecido, " +
                      "sem nova anomalia detectada."
                    : "Nenhuma anomalia foi identificada.",

            InterpretacaoTecnica =
                comportamentoAnormalConhecido
                    ? $"O padrão atual corresponde ao comportamento " +
                      $"anormal conhecido {codigoComportamentoReconhecido ?? "-"}. " +
                      "Os detectores não identificaram nova evidência funcional " +
                      "de anomalia nesta leitura."
                    : "Os detectores indicaram que o comportamento atual da tag " +
                      "permanece dentro do padrão esperado.",

            Evidencias =
                comportamentoAnormalConhecido
                    ? new List<string>
                    {
                        $"Comportamento reconhecido: " +
                        $"{codigoComportamentoReconhecido ?? "-"}.",
                        "Classificação semântica: KnownAbnormal.",
                        "Nenhum detector identificou nova evidência " +
                        "funcional de anomalia nesta leitura."
                    }
                    : new List<string>
                    {
                        "Nenhum detector identificou comportamento anômalo."
                    },

            PossiveisCausas =
                new(),

            Recomendacoes =
                comportamentoAnormalConhecido
                    ? new List<string>
                    {
                        "Continuar acompanhando o comportamento conhecido " +
                        "e observar novas evidências dos detectores."
                    }
                    : new List<string>
                    {
                        "Continuar monitorando normalmente."
                    },

            Severidade =
                "Normal",

            GeradoPorLlm =
                false,

            ModeloUtilizado =
                comportamentoAnormalConhecido
                    ? "Contexto comportamental determinístico"
                    : "Não utilizado",

            DataGeracao =
                DateTime.UtcNow
        };
    }
}