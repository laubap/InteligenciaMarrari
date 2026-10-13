namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// B3.16 — traduz a decisão funcional em dois canais operacionais:
///
/// - EmitAnomaly:
///   deve gerar/tratar uma NOVA anomalia.
///
/// - EmitNewEvidence:
///   existe evidência nova relevante que deve permanecer visível,
///   mesmo quando não deve ser contada como uma nova anomalia.
///
/// Este resolver não grava arquivos, não envia notificações e não altera
/// DetectorResult/FinalAnomaly. Ele apenas resolve a semântica operacional.
/// </summary>
public sealed class FunctionalOperationalAnomalyDecisionResolver
{
    public FunctionalOperationalAnomalyDecision Resolve(
        FunctionalExperimentalAnomalyDecision functionalDecision)
    {
        ArgumentNullException.ThrowIfNull(functionalDecision);

        /*
         * Fail-safe:
         *
         * Se a camada experimental não esteve disponível e o resolver
         * funcional precisou cair no LegacyFallback, o comportamento
         * operacional deve preservar exatamente o Legacy.
         */
        if (functionalDecision.UsedLegacyFallback)
        {
            return new FunctionalOperationalAnomalyDecision(
                LegacyFinalAnomaly:
                    functionalDecision.LegacyFinalAnomaly,

                EmitAnomaly:
                    functionalDecision.LegacyFinalAnomaly,

                EmitNewEvidence:
                    functionalDecision.LegacyFinalAnomaly,

                SourceDecision:
                    functionalDecision.SourceDecision,

                UsedLegacyFallback:
                    true,

                Reasons:
                    functionalDecision.Reasons
                        .Concat(["OperationalLegacyFallback"])
                        .ToArray());
        }

        return functionalDecision.SourceDecision switch
        {
            /*
             * Legacy normal:
             * nenhum alerta e nenhuma nova evidência.
             */
            ExperimentalAnomalyDecisionKind.LegacyNormal =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly: false,
                    EmitNewEvidence: false,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: false,
                    functionalDecision.Reasons),

            /*
             * Nova anomalia real:
             * gera anomalia e também nova evidência.
             */
            ExperimentalAnomalyDecisionKind.NewAnomaly =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly: true,
                    EmitNewEvidence: true,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: false,
                    functionalDecision.Reasons),

            /*
             * Continuação estável de incidente já conhecido:
             * não gera uma nova anomalia e não cria nova evidência.
             */
            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly: false,
                    EmitNewEvidence: false,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: false,
                    functionalDecision.Reasons),

            /*
             * CandidateMismatch:
             *
             * não é automaticamente uma nova anomalia independente,
             * mas É nova evidência relevante.
             *
             * Esse segundo canal preserva a visibilidade de B -> C.
             */
            /*
             * Increasing Noise continuation:
             * anomaly remains active, but this is not new evidence.
             */
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly: true,
                    EmitNewEvidence: false,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: false,
                    functionalDecision.Reasons),
            ExperimentalAnomalyDecisionKind.CandidateMismatch =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly: false,
                    EmitNewEvidence: true,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: false,
                    functionalDecision.Reasons),

            /*
             * DataQualityUncertain:
             *
             * Após a proteção B3.16 do engine, quando existe CriticalGap
             * com Legacy anômalo, a decisão funcional preserva o Legacy.
             *
             * Por segurança operacional, usamos os próprios campos já
             * resolvidos pela camada funcional, sem reinterpretar o gap.
             */
            ExperimentalAnomalyDecisionKind.DataQualityUncertain =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly:
                        functionalDecision.FunctionalFinalAnomaly,
                    EmitNewEvidence:
                        functionalDecision.CountsAsNewEvidence,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: false,
                    functionalDecision.Reasons),

            /*
             * Futuro enum desconhecido:
             * preserva Legacy conservadoramente.
             */
            _ =>
                new FunctionalOperationalAnomalyDecision(
                    functionalDecision.LegacyFinalAnomaly,
                    EmitAnomaly:
                        functionalDecision.LegacyFinalAnomaly,
                    EmitNewEvidence:
                        functionalDecision.LegacyFinalAnomaly,
                    functionalDecision.SourceDecision,
                    UsedLegacyFallback: true,
                    functionalDecision.Reasons
                        .Concat(["UnknownOperationalDecision", "LegacyFallback"])
                        .ToArray())
        };
    }
}