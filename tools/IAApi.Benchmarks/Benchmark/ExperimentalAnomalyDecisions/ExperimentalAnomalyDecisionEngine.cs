namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// Motor puro da decisão experimental.
///
/// Não altera DetectorResult.
/// Não altera FinalAnomaly.
/// Não altera ensemble.
/// Não altera baseline.
///
/// Apenas responde:
/// "esta leitura ainda representa uma NOVA anomalia
/// ou é continuação de um desvio já conhecido?"
/// </summary>
public sealed class ExperimentalAnomalyDecisionEngine
{
    public ExperimentalAnomalyDecision Evaluate(
        ExperimentalAnomalyDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        /*
         * B3.22.4 - frozen sensor is independent data-quality evidence.
         * It can exist while Legacy remains normal, so it is evaluated
         * before the LegacyNormal early return.
         */
        if (input.FrozenSensor)
        {
            return new ExperimentalAnomalyDecision(
                ExperimentalAnomalyDecisionKind.DataQualityUncertain,
                true,
                true,
                [
                    "FrozenSensorDetected",
                    "IndependentDataQualityEvidence"
                ]);
        }

        /*
         * SEGURANÇA 0 — B3.16
         *
         * Se o Legacy marcou anomalia e existe CriticalGap,
         * preservamos conservadoramente a decisão Legacy ANTES
         * de qualquer interpretação baseada no Candidate B.
         *
         * Isso evita que:
         *
         * Legacy anomaly + CriticalGap + CandidateMismatch
         *
         * seja reduzido para CandidateMismatch
         * (CountsAsNewAnomaly = false).
         */
        if (input.LegacyFinalAnomaly &&
            input.CriticalGap)
        {
            return new ExperimentalAnomalyDecision(
                ExperimentalAnomalyDecisionKind.DataQualityUncertain,
                true,
                true,
                [
                    "CriticalDataQualityGap",
                    "LegacyDecisionPreservedConservatively"
                ]);
        }

        /*
         * SEGURANÇA 1
         *
         * MAD-B ou Z-B anormal significa que o comportamento atual
         * deixou de ser explicado pelo Candidate B.
         *
         * Exemplo:
         *
         * A -> B estável -> C
         *
         * C NÃO pode virar continuation.
         */
        if (input.MadCandidateVote || input.ZCandidateVote)
        {
            var reasons = new List<string>
            {
                "CandidateBaselineMismatch"
            };

            if (input.MadCandidateVote)
                reasons.Add("MadAgainstCandidateAbnormal");

            if (input.ZCandidateVote)
                reasons.Add("ZAgainstCandidateAbnormal");

            return new ExperimentalAnomalyDecision(
                ExperimentalAnomalyDecisionKind.CandidateMismatch,
                false,
                true,
                reasons);
        }

        /*
         * Se o Legacy nem marcou anomalia, não precisamos inventar
         * uma nova anomalia experimental.
         */
        /*
         * B3.22.5 - Increasing Noise pode existir com Legacy normal.
         * A primeira confirmacao e nova evidencia.
         * As leituras seguintes representam a mesma condicao ativa.
         */
        if (!input.LegacyFinalAnomaly &&
            input.IncreasingNoise &&
            input.IncreasingNoiseStarted &&
            !input.CriticalGap)
        {
            return new ExperimentalAnomalyDecision(
                ExperimentalAnomalyDecisionKind.NewAnomaly,
                true,
                true,
                [
                    "IncreasingNoiseDetected",
                    "IndependentVarianceExpansionEvidence"
                ]);
        }

        if (!input.LegacyFinalAnomaly &&
            input.IncreasingNoise &&
            input.IncreasingNoiseContinuation &&
            !input.CriticalGap)
        {
            return new ExperimentalAnomalyDecision(
                ExperimentalAnomalyDecisionKind
                    .IncreasingNoiseContinuation,
                false,
                false,
                [
                    "IncreasingNoiseStillActive"
                ]);
        }
        if (!input.LegacyFinalAnomaly)
        {
            return new ExperimentalAnomalyDecision(
                ExperimentalAnomalyDecisionKind.LegacyNormal,
                false,
                false,
                ["LegacyFinalAnomalyFalse"]);
        }

        /*
         * REGRA CENTRAL DA B3.15
         *
         * Só podemos interpretar uma leitura como CONTINUAÇÃO
         * quando existe um incidente real já aberto e B está
         * estatisticamente pronto.
         */
        var incidentOpen =
            !string.IsNullOrWhiteSpace(input.IncidentId);

        var stableUnresolved =
            string.Equals(
                input.RegimeState,
                "StableUnresolvedDeviation",
                StringComparison.OrdinalIgnoreCase);

        var activeStillAbnormal =
            string.Equals(
                input.AgainstActive,
                "Abnormal",
                StringComparison.OrdinalIgnoreCase);

        var candidateFits =
            !input.MadCandidateVote &&
            !input.ZCandidateVote;

        var safeForContinuation =
            incidentOpen &&
            input.CandidateBaselineReady &&
            stableUnresolved &&
            activeStillAbnormal &&
            candidateFits &&
            !input.CriticalGap &&
            !input.OperationalBaselineChanged &&
            !input.Promotion;

        if (safeForContinuation)
        {
            var reasons = new List<string>
            {
                "IncidentAlreadyOpen",
                "CandidateBaselineReady",
                "StableUnresolvedDeviation",
                "ActiveBaselineStillAbnormal",
                "MadFitsCandidate",
                "ZFitsCandidate",
                "OperationalBaselineUnchanged",
                "CandidateNotPromoted"
            };

            /*
             * Já provamos que EWMA pode votar isoladamente em sinais
             * periódicos. Portanto ele continua visível para diagnóstico,
             * mas sozinho não destrói CandidateFit.
             */
            if (input.EwmaCandidateVote)
                reasons.Add("EwmaOnlyTemporalMismatch");

            return new ExperimentalAnomalyDecision(
                    ExperimentalAnomalyDecisionKind
                        .StableUnresolvedContinuation,
                    false,
                    false,
                    reasons);
        }

        /*
         * Tudo que não satisfaz TODAS as condições acima
         * permanece conservadoramente como nova anomalia.
         *
         * Isso preserva:
         * - primeira detecção;
         * - fase inicial A -> B;
         * - Candidate ainda não Ready;
         * - episódios sem Incident;
         * - transitórios;
         * - casos não suportados.
         */
        var fallbackReasons = new List<string>
        {
            "LegacyFinalAnomalyPreserved"
        };

        if (!incidentOpen)
            fallbackReasons.Add("NoOpenIncident");

        if (!input.CandidateBaselineReady)
            fallbackReasons.Add("CandidateNotReady");

        if (!stableUnresolved)
            fallbackReasons.Add("RegimeNotStableUnresolved");

        if (!activeStillAbnormal)
            fallbackReasons.Add("ActiveBaselineNotAbnormal");

        if (input.OperationalBaselineChanged)
            fallbackReasons.Add("OperationalBaselineChanged");

        if (input.Promotion)
            fallbackReasons.Add("CandidatePromoted");

        return new ExperimentalAnomalyDecision(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            true,
            true,
            fallbackReasons);
    }
}