using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Services.AnomalyDetection.Presentation;

public static class PredictionResponseMapper
{
    public static ResumoAnomaliaDto MapearResumo(
        bool ehAnomalia,
        bool comportamentoReconhecido,
        string? classificacaoComportamentoReconhecido,
        string conclusao)
    {
        return new ResumoAnomaliaDto
        {
            Status = ehAnomalia
                ? "Anomalia detectada"
                : comportamentoReconhecido &&
                  string.Equals(
                      classificacaoComportamentoReconhecido,
                      "KnownAbnormal",
                      StringComparison.Ordinal)
                    ? "Comportamento anormal conhecido"
                    : "Comportamento normal",

            Conclusao =
                conclusao
        };
    }

    public static PerfilComportamentalDto MapearPerfil(
        AnomaliaRequest request,
        double mediaHistorica,
        double limiteInferior,
        double limiteSuperior,
        double score)
    {
        return new PerfilComportamentalDto
        {
            ValorRecebido =
                request.Valor,

            MediaHistorica =
                mediaHistorica,

            LimiteInferior =
                limiteInferior,

            LimiteSuperior =
                limiteSuperior,

            Score =
                score
        };
    }

    public static ContextoOperacionalDto? MapearContexto(
        TagContextoIa? contexto)
    {
        if (contexto is null)
        {
            return null;
        }

        return new ContextoOperacionalDto
        {
            Descricao =
                contexto.Descricao,

            Criticidade =
                contexto.Criticidade,

            Equipamento =
                contexto.Equipamento,

            Area =
                contexto.Area,

            ObservacaoModoOperacao =
                contexto.ObservacaoModoOperacao,

            Impactos =
                PredictionResponseFormatter.LerLista(
                    contexto.Impactos),

            CausasProvaveis =
                PredictionResponseFormatter.LerLista(
                    contexto.CausasProvaveis),

            AcoesRecomendadas =
                PredictionResponseFormatter.LerLista(
                    contexto.AcoesRecomendadas),

            ModosOperacao =
                PredictionResponseFormatter.LerLista(
                    contexto.ModosOperacao)
        };
    }

    public static InterpretacaoAnomaliaDto MapearInterpretacao(
        AnomalyInterpretationResult resultadoInterpretacao)
    {
        return new InterpretacaoAnomaliaDto
        {
            Resumo =
                resultadoInterpretacao.Resumo,

            InterpretacaoTecnica =
                resultadoInterpretacao.InterpretacaoTecnica,

            Evidencias =
                resultadoInterpretacao.Evidencias
                ?? new List<string>(),

            PossiveisCausas =
                resultadoInterpretacao.PossiveisCausas
                ?? new List<string>(),

            Recomendacoes =
                resultadoInterpretacao.Recomendacoes
                ?? new List<string>(),

            Severidade =
                resultadoInterpretacao.Severidade,

            GeradoPorLlm =
                resultadoInterpretacao.GeradoPorLlm,

            ModeloUtilizado =
                resultadoInterpretacao.ModeloUtilizado,

            DataGeracao =
                resultadoInterpretacao.DataGeracao
        };
    }

    public static RiscoProcessoDto MapearRisco(
        ResultadoRiscoProcesso riscoProcesso,
        List<string> fatoresRisco)
    {
        return new RiscoProcessoDto
        {
            Score =
                riscoProcesso.ScoreRisco,

            Classificacao =
                riscoProcesso.Classificacao,

            Mensagem =
                riscoProcesso.Mensagem,

            Fatores =
                fatoresRisco
        };
    }

    public static TendenciasDto MapearTendencias(
        ResultadoTendenciaRisco tendenciaRisco,
        ResultadoTendenciaValor tendenciaValor)
    {
        return new TendenciasDto
        {
            TendenciaRisco =
                tendenciaRisco.Tendencia,

            MensagemTendenciaRisco =
                tendenciaRisco.Mensagem,

            TendenciaValor =
                tendenciaValor.Tendencia,

            MensagemTendenciaValor =
                tendenciaValor.Mensagem
        };
    }

    public static MultivariateAnomalyDto? MapearMultivariado(
        bool multivariatePromotionApplied,
        MultivariateDecisionEvidence? evidence,
        string origem,
        double scoreUnivariado)
    {
        if (!multivariatePromotionApplied ||
            evidence is null)
        {
            return null;
        }

        return new MultivariateAnomalyDto
        {
            PromocaoOperacional =
                true,

            Origem =
                origem,

            Grupo =
                evidence.GroupName,

            MinuteIndex =
                evidence.MinuteIndex,

            IdadeMinutos =
                evidence.AgeMinutes,

            ScoreNormalizado =
                evidence.Score,

            LimiteScore =
                evidence.Threshold,

            MahalanobisSquared =
                evidence.MahalanobisSquared,

            ScoreUnivariado =
                scoreUnivariado,

            Tags =
                evidence.Tags.ToList()
        };
    }

    public static ProcessoDto? MapearProcesso(
    ProcessoIa? processo)
{
    if (processo is null)
    {
        return null;
    }

    return new ProcessoDto
    {
        Nome =
            processo.Nome,

        Descricao =
            processo.Descricao,

        Area =
            processo.Area,

        Criticidade =
            processo.Criticidade,

        Objetivo =
            processo.Objetivo,

        CondicaoNormal =
            processo.CondicaoNormal,

        ConsequenciasFalha =
            processo.ConsequenciasFalha,

        ProcedimentoRecomendado =
            processo.ProcedimentoRecomendado
    };
}

public static List<DetectorExecutadoDto> MapearDetectores(
    IEnumerable<DetectorDebugItem> detectores)
{
    return detectores
        .Select(detector =>
            new DetectorExecutadoDto
            {
                Nome =
                    detector.Nome,

                Aplicavel =
                    detector.Aplicavel,

                VotouAnomalia =
                    detector.VotouAnomalia,

                ScoreNormalizado =
                    detector.ScoreNormalizado,

                ScoreOriginal =
                    detector.ScoreOriginal,

                Peso =
                    detector.Peso,

                Explicacao =
                    detector.Explicacao,

                Metricas =
                    detector.Metricas
                    ?? new Dictionary<string, double>()
            })
        .ToList();
}
}