using System.Text.Json;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

public sealed class ExperimentalAnomalyDecisionObserver : IAsyncDisposable
{
    private readonly ExperimentalAnomalyDecisionOptions _options;
    private readonly ExperimentalAnomalyDecisionEngine _engine = new();

    private readonly StreamWriter _writer;

    private readonly string _summaryPath;
    private readonly string _errorPath;

    private long _legacyAnomalies;
    private long _legacyNormal;

    private long _newAnomalies;
    private long _continuations;
private long _candidateMismatches;
private long _dataQualityUncertain;

private long _legacyAnomaliesReclassifiedAsContinuation;

private long _candidateMismatchesWithoutLegacyAnomaly;

/*
 * B3.15 — observabilidade de nova evidência.
 *
 * CandidateMismatch precisa permanecer visível mesmo quando
 * não conta como NEW ANOMALY.
 */
private long _newEvidenceSignals;

private long _experimentalNewAnomalySignals;

/*
 * Quantos CandidateMismatch aconteceram quando já existia:
 * - incidente aberto;
 * - Candidate B pronto;
 * - regime StableUnresolvedDeviation.
 *
 * É a nossa evidência conservadora de uma transição
 * do tipo A -> B -> C.
 */
private long _candidateMismatchAfterStableCandidate;

/*
 * Garante que todo CandidateMismatch decidido pelo engine
 * também entrou no trace experimental.
 */
private long _candidateMismatchWrittenToTrace;

private long _candidateMismatchAfterStableCandidateWrittenToTrace;

    private long _errors;

    /*
     * B3.22.5 - estado observacional por janela/tag.
     *
     * Permite distinguir a primeira confirmacao de Increasing Noise
     * das leituras seguintes da mesma condicao.
     */
    private readonly Dictionary<string, bool> _increasingNoiseActive =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _increasingNoiseConsecutiveMisses =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _byTag =
        new(StringComparer.OrdinalIgnoreCase);

    public string TracePath { get; }

    public string SummaryPath => _summaryPath;

    public long Errors => _errors;

    

    public ExperimentalAnomalyDecisionObserver(
        ExperimentalAnomalyDecisionOptions options,
        string directory,
        string executionId)
    {
        _options = options;

        TracePath = Path.Combine(
            directory,
            $"experimental_anomaly_decisions_{executionId}.jsonl");

        _summaryPath = Path.Combine(
            directory,
            $"experimental_anomaly_decision_summary_{executionId}.json");

        _errorPath = Path.Combine(
            directory,
            $"experimental_anomaly_decision_errors_{executionId}.jsonl");

        _writer = new StreamWriter(
            new FileStream(
                TracePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read));
    }

    public async ValueTask<ExperimentalAnomalyDecision?> ObserveAsync(
        DualBaselineTraceRecord row,
        DetectorResult original,
        DualBaselineDecisionClassification classification,
        string regimeEpisodeId,
        string? incidentId)
    {
        if (!_options.Enabled)
            return null;

        try
        {
            if (original.EhAnomalia)
                _legacyAnomalies++;
            else
                _legacyNormal++;

            var increasingNoiseKey =
                $"{row.FastWindowIndex}\u001f{row.Tag}";

            var increasingNoise =
                row.IncreasingNoiseEvidence;

            var increasingNoiseWasActive =
                _increasingNoiseActive
                    .GetValueOrDefault(increasingNoiseKey);

            var increasingNoiseStarted =
                increasingNoise &&
                !increasingNoiseWasActive;

            var increasingNoiseContinuation =
                increasingNoise &&
                increasingNoiseWasActive;

            /*
             * B3.22.5 - episode hysteresis.
             *
             * One missing evidence point does not close an already
             * confirmed Increasing Noise episode.
             */
            if (increasingNoise)
            {
                _increasingNoiseActive[increasingNoiseKey] = true;
                _increasingNoiseConsecutiveMisses[increasingNoiseKey] = 0;
            }
            else if (increasingNoiseWasActive)
            {
                var misses =
                    _increasingNoiseConsecutiveMisses
                        .GetValueOrDefault(increasingNoiseKey) + 1;

                if (misses >= 2)
                {
                    _increasingNoiseActive[increasingNoiseKey] = false;
                    _increasingNoiseConsecutiveMisses[increasingNoiseKey] = 0;
                }
                else
                {
                    _increasingNoiseConsecutiveMisses[increasingNoiseKey] =
                        misses;
                }
            }
            else
            {
                _increasingNoiseConsecutiveMisses[increasingNoiseKey] = 0;
            }
            var input = new ExperimentalAnomalyDecisionInput(
                row.FastWindowIndex,
                row.MinuteIndex,
                row.Timestamp,
                row.Tag,

                original.EhAnomalia,
                original.Score,

                incidentId,
                regimeEpisodeId,

                row.CandidateBaselineReady,
                row.RegimeState,
                row.AgainstActive,

                row.MadCandidate?.Vote == true,
                row.ZCandidate?.Vote == true,
                row.EwmaCandidate?.Vote == true,

                row.CandidateCriticalGap,
                row.OperationalBaselineChanged,
                row.Promotion,

                original.DetalhesDetectores
                    .Where(x => x.VotouAnomalia)
                    .Select(x => x.Nome)
                    .ToArray())
            {
                FrozenSensor =
                    row.FrozenSensorDataQuality,

                IncreasingNoise =
                    increasingNoise,

                IncreasingNoiseStarted =
                    increasingNoiseStarted,

                IncreasingNoiseContinuation =
                    increasingNoiseContinuation
            };

                var decision = _engine.Evaluate(input);

                if (decision.CountsAsNewAnomaly)
                    _experimentalNewAnomalySignals++;

                if (decision.CountsAsNewEvidence)
                    _newEvidenceSignals++;

            switch (decision.Kind)
            {
                case ExperimentalAnomalyDecisionKind.NewAnomaly:
                    _newAnomalies++;
                    break;

                case ExperimentalAnomalyDecisionKind
                    .StableUnresolvedContinuation:
                    _continuations++;

                    if (original.EhAnomalia)
                    {
                        _legacyAnomaliesReclassifiedAsContinuation++;

                        _byTag[row.Tag] =
                            _byTag.GetValueOrDefault(row.Tag) + 1;
                    }

                    break;

case ExperimentalAnomalyDecisionKind.CandidateMismatch:
    _candidateMismatches++;

    if (!original.EhAnomalia)
        _candidateMismatchesWithoutLegacyAnomaly++;

    /*
     * Evidência conservadora de A -> B -> C:
     *
     * Já existe incidente;
     * Candidate B já estava pronto;
     * estávamos em StableUnresolvedDeviation;
     * e agora MAD-B ou Z-B deixou de aceitar o comportamento.
     *
     * Não afirmamos que todo CandidateMismatch é necessariamente C.
     * Medimos somente os casos em que a sequência anterior
     * dá suporte a essa interpretação.
     */
    var candidateMismatchAfterStableCandidate =
        !string.IsNullOrWhiteSpace(input.IncidentId) &&
        input.CandidateBaselineReady &&
        string.Equals(
            input.RegimeState,
            "StableUnresolvedDeviation",
            StringComparison.OrdinalIgnoreCase);

    if (candidateMismatchAfterStableCandidate)
        _candidateMismatchAfterStableCandidate++;

    break;

                case ExperimentalAnomalyDecisionKind.DataQualityUncertain:
                    _dataQualityUncertain++;
                    break;
            }

            /*
             * Não precisamos gravar milhões de LegacyNormal.
             *
             * O trace é focado em tudo que interessa para comparar
             * Legacy vs Experimental.
             */
            var shouldWrite =
                original.EhAnomalia ||
                input.IncreasingNoise ||
                decision.Kind ==
                    ExperimentalAnomalyDecisionKind.CandidateMismatch ||
                decision.Kind ==
                    ExperimentalAnomalyDecisionKind.DataQualityUncertain;

            if (!shouldWrite)
                return decision;

if (decision.Kind ==
    ExperimentalAnomalyDecisionKind.CandidateMismatch)
{
    _candidateMismatchWrittenToTrace++;

    var mismatchAfterStableCandidate =
        !string.IsNullOrWhiteSpace(input.IncidentId) &&
        input.CandidateBaselineReady &&
        string.Equals(
            input.RegimeState,
            "StableUnresolvedDeviation",
            StringComparison.OrdinalIgnoreCase);

    if (mismatchAfterStableCandidate)
        _candidateMismatchAfterStableCandidateWrittenToTrace++;
}

            var record = new ExperimentalAnomalyDecisionRecord(
                input.FastWindow,
                input.MinuteIndex,
                input.Timestamp,
                input.Tag,

                input.LegacyFinalAnomaly,
                input.LegacyScore,

                decision.Kind,
                decision.CountsAsNewAnomaly,
                decision.CountsAsNewEvidence,

                input.IncidentId,
                input.RegimeEpisodeId,

                input.CandidateBaselineReady,
                input.RegimeState,
                input.AgainstActive,

                input.MadCandidateVote,
                input.ZCandidateVote,
                input.EwmaCandidateVote,

                input.CriticalGap,
                input.OperationalBaselineChanged,
                input.Promotion,

                input.LegacyAnomalousDetectors,
                decision.Reasons);

            await _writer.WriteLineAsync(
                JsonSerializer.Serialize(record, JsonOptions));

            return decision;
        }
        catch (Exception ex)
        {
            _errors++;

            await File.AppendAllTextAsync(
                _errorPath,
                JsonSerializer.Serialize(new
                {
                    row.FastWindowIndex,
                    row.MinuteIndex,
                    row.Tag,
                    error = ex.ToString()
                }) + Environment.NewLine);

            /*
             * Fail-open:
             *
             * erro experimental NUNCA derruba Legacy.
             */
            if (!_options.FailOpen)
                throw;

            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        await _writer.DisposeAsync();

/*
 * B3.15 — NEW ANOMALY experimental.
 *
 * A fonte autoritativa é CountsAsNewAnomaly produzida pelo Engine.
 *
 * Isso é importante porque:
 *
 * - StableUnresolvedContinuation = não é nova anomalia;
 * - CandidateMismatch = nova evidência, mas não automaticamente
 *   nova anomalia;
 * - DataQualityUncertain preserva conservadoramente o Legacy.
 */
var experimentalNewAnomalySignals =
    _experimentalNewAnomalySignals;

/*
 * Quantos FinalAnomaly Legacy deixaram de ser interpretados
 * como NEW ANOMALY pelo modo experimental.
 */
var legacyAnomaliesNoLongerCountedAsNew =
    Math.Max(
        0,
        _legacyAnomalies -
        experimentalNewAnomalySignals);

/*
 * Mantemos uma métrica separada para toda evidência nova
 * observável no modo experimental.
 *
 * Assim B -> C continua aparecendo sem distorcer a comparação
 * de NEW ANOMALIES Legacy vs Experimental.
 */
/*
 * Usa o contador produzido diretamente pela semântica
 * CountsAsNewEvidence do engine.
 *
 * Ele é mantido separado de NEW ANOMALY.
 */
var experimentalNewEvidenceSignals =
    _newEvidenceSignals;

 var reductionPercentage =
    _legacyAnomalies == 0
        ? 0
        : 100.0 *
          legacyAnomaliesNoLongerCountedAsNew /
          _legacyAnomalies;

        var candidateMismatchFullyVisible =
    _candidateMismatches ==
    _candidateMismatchWrittenToTrace;

var aToBToCNewEvidencePreserved =
    _candidateMismatchAfterStableCandidate ==
    _candidateMismatchAfterStableCandidateWrittenToTrace;

        var summary = new
        {
            generatedAtUtc = DateTime.UtcNow,

            semantics = new
            {
                legacyFinalAnomalyChanged = false,

                stableUnresolvedContinuationIsNormal = false,

                stableUnresolvedContinuation =
                    "Known open deviation continues and current reading " +
                    "fits ready Candidate B.",

                candidateMismatch =
                    "MAD-B or Z-B is abnormal and therefore represents " +
                    "new anomalous evidence.",

                candidateMismatchCountsAsNewAnomaly =
                    false,

                candidateMismatchVisibility =
                    "CandidateMismatch remains visible as new evidence " +
                    "but is not automatically counted as a new anomaly.",

                experimentalNewAnomaly =
                    "Counts Legacy FinalAnomaly records that remain new " +
                    "after eligible StableUnresolvedContinuation consolidation.",

                experimentalNewEvidence =
                    "Counts every decision explicitly marked by the engine " +
                    "as CountsAsNewEvidence. This includes NewAnomaly, " +
                    "CandidateMismatch and conservatively preserved " +
                    "DataQualityUncertain decisions.",

                ewmaOnlyMismatch =
                    "Diagnostic only; does not independently block " +
                    "candidate continuation."
            },

            legacyAnomalyRecords =
                _legacyAnomalies,

            legacyNormalRecords =
                _legacyNormal,

            legacyAnomaliesReclassifiedAsContinuation =
                _legacyAnomaliesReclassifiedAsContinuation,

            experimentalNewAnomalySignals =
                experimentalNewAnomalySignals,

            legacyAnomaliesNoLongerCountedAsNew =
                legacyAnomaliesNoLongerCountedAsNew,

            candidateMismatchSignals =
                _candidateMismatches,

            candidateMismatchWithoutLegacyAnomaly =
                _candidateMismatchesWithoutLegacyAnomaly,

            candidateMismatchWrittenToTrace =
                _candidateMismatchWrittenToTrace,

            candidateMismatchFullyVisible,

candidateMismatchAfterStableCandidate =
    _candidateMismatchAfterStableCandidate,

candidateMismatchAfterStableCandidateWrittenToTrace =
    _candidateMismatchAfterStableCandidateWrittenToTrace,

aToBToCNewEvidencePreserved,

            dataQualityUncertain =
                _dataQualityUncertain,


            experimentalNewEvidenceSignals,

            newAnomalyReductionPercentage =
                reductionPercentage,

            continuationRecords =
                _continuations,

            errors =
                _errors,

            continuationReductionByTag =
                _byTag
                    .OrderByDescending(x => x.Value)
                    .ToArray()
        };

        await File.WriteAllTextAsync(
            _summaryPath,
            JsonSerializer.Serialize(
                summary,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                }));
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };
}