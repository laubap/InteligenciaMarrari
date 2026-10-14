using IAApi.Services.AnomalyDetection.Runtime.Incidents.Experimental;
using IAApi.Services.AnomalyDetection.Runtime.Incidents.StableDeviation;
using IAApi.RegimePolicy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using IAApi.Services.AnomalyDetection.Runtime.Regime;
using IAApi.Services.AnomalyDetection.Runtime.Regime.Evidence;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.18 — adapta para o runtime real a mesma cadeia experimental validada no
/// Benchmark: RegimeEvidenceAdapter -> RegimePolicyEngine -> DualBaseline ->
/// IncidentCoordinator.
///
/// Nesta etapa o provider somente PRODUZ contexto. Ele não decide emissão,
/// não muda DetectorResult.EhAnomalia e não altera baseline operacional.
///
/// B3.20.11 — também projeta para o contexto runtime as evidências do baseline
/// ativo já calculadas pelo ensemble Legacy (Z-Score, MAD e Taxa de variação).
/// Nenhum detector é reexecutado aqui.
/// </summary>
public sealed class RuntimeFunctionalDecisionContextProvider
{
    private const int RuntimeWindow = 0;

    private readonly RuntimeFunctionalDecisionStateStore _store;
    private readonly RegimeEvidenceAdapter _regimeAdapter = new();
    private readonly RegimePolicyEngine _regimePolicy = new();
    private readonly ExperimentalIncidentAdapter _incidentAdapter = new();
    private readonly ExperimentalIncidentCoordinator _incidentCoordinator = new();
    private readonly RegimeShadowOptions _regimeOptions = new();
    private readonly DualBaselineShadowEngine _dualBaseline;
    private readonly string _executionScopeId;
    private readonly ILogger<RuntimeFunctionalDecisionContextProvider>? _logger;

    public RuntimeFunctionalDecisionContextProvider(
        RuntimeFunctionalDecisionStateStore store,
        IOptions<AnomalyDetectionOptions> detectorOptions,
        ILogger<RuntimeFunctionalDecisionContextProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(detectorOptions);

        _store = store;
        _logger = logger;
        _executionScopeId = $"runtime:{Guid.NewGuid():N}";

        var source = detectorOptions.Value ?? new AnomalyDetectionOptions();

        _dualBaseline = new DualBaselineShadowEngine(
            new DualBaselineShadowOptions
            {
                Enabled = true,
                DecisionCounterfactualEnabled = true,
                ObserveAllTagsAndWindows = true,
                IncidentCounterfactualEnabled = true,
                MadThreshold = source.MadThreshold,
                ZScoreThreshold = source.ZScoreThreshold,
                EwmaLambda = source.EwmaLambda,
                EwmaSigmaLimit = source.EwmaSigmaLimit
            });
    }

    public RuntimeFunctionalDecisionContext Evaluate(
        DetectorInput input,
        DetectorResult legacyResult)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(legacyResult);

        var state = _store.GetOrCreate(input.ClienteId, input.TagName);

        lock (state.Gate)
        {
            var sequence = ++state.Sequence;

            var shadowInput =
                CreateContiguousRuntimeInput(input);

            var adaptation = _regimeAdapter.Adapt(
                sequence,
                shadowInput,
                legacyResult,
                state.Regime.PolicyState,
                state.Regime.CandidateEvidence,
                _regimeOptions);

            /*
             * B3.22.5 V3a - transporta o estado de Increasing Noise
             * para a camada funcional. Ainda nao altera emissao.
             */
            var increasingNoise =
                adaptation.Evidence.HasIncreasingNoiseEvidence;

            var increasingNoiseStarted =
                increasingNoise &&
                !state.IncreasingNoiseActive;

            var increasingNoiseContinuation =
                increasingNoise &&
                state.IncreasingNoiseActive;

            /*
             * B3.22.5 - episode hysteresis.
             *
             * One missing evidence point does not close an already
             * confirmed Increasing Noise episode.
             */
            if (increasingNoise)
            {
                state.IncreasingNoiseActive = true;
                state.IncreasingNoiseConsecutiveMisses = 0;
            }
            else if (state.IncreasingNoiseActive)
            {
                state.IncreasingNoiseConsecutiveMisses++;

                if (state.IncreasingNoiseConsecutiveMisses >= 2)
                {
                    state.IncreasingNoiseActive = false;
                    state.IncreasingNoiseConsecutiveMisses = 0;
                }
            }
            else
            {
                state.IncreasingNoiseConsecutiveMisses = 0;
            }

            var rawDecision = _regimePolicy.Evaluate(
                state.Regime.PolicyState,
                adaptation.Observation,
                adaptation.Evidence);

            var policyDecision = RegimeShadowLifecycle.NormalizeDecision(
                state.Regime.PolicyState,
                rawDecision,
                adaptation.Evidence,
                adaptation.NextCandidateState);

var row = _dualBaseline.Evaluate(
    RuntimeWindow,
    sequence,
    shadowInput,
    new DualBaselineOriginalDecision(
        legacyResult.EhAnomalia,
        legacyResult.Score),
    policyDecision,
    state.DualBaseline);

            UpdateDecisionEpisode(state, row);

            var stableAlreadyStarted = state.StableUnresolvedStarted;
            var (classification, _) =
                DualBaselineDecisionClassifier.Classify(
                    row,
                    stableAlreadyStarted);

            if (row.RegimeState ==
                RegimeLifecycleState.StableUnresolvedDeviation.ToString())
            {
                state.StableUnresolvedStarted = true;
            }

            var episodeId =
                state.DecisionEpisodeId ??
                row.EpisodeId ??
                $"runtime-episode:{input.TagName}:{sequence}";

            var incidentObservation = _incidentAdapter.Adapt(
                _executionScopeId,
                sequence,
                row,
                legacyResult,
                classification,
                episodeId,
                state.DecisionEpisodeActive);

            var incidentEvaluation = _incidentCoordinator.Evaluate(
                state.Incident,
                incidentObservation);

            state.Incident = incidentEvaluation.NextState;

            var incidentId = OpenIncidentId(state.Incident);

            var lifecycle = RegimeShadowLifecycle.Apply(
                state.Regime.PolicyState,
                policyDecision,
                adaptation.NextCandidateState);

            state.Regime = new RegimeShadowTagState(
                policyDecision.NextState,
                lifecycle.NextCandidateState,
                lifecycle.ResetReason);

            if (classification ==
                DualBaselineDecisionClassification.ResolvedOrReturnedToActive)
            {
                state.DecisionEpisodeActive = false;
                state.DecisionEpisodeId = null;
                state.StableUnresolvedStarted = false;
                state.Incident = null;
            }
            else if (state.Incident?.PendingRawAlert is null &&
                     state.Incident?.Incident is null)
            {
                state.Incident = null;
            }

            var zActive =
                FindDetector(
                    legacyResult,
                    "Z-Score");

            var madActive =
                FindDetector(
                    legacyResult,
                    "Mediana e MAD");

            var rateActive =
                FindDetector(
                    legacyResult,
                    "Taxa de variação");

            var context = new RuntimeFunctionalDecisionContext(
                IncidentId: incidentId,
                CandidateBaselineReady: row.CandidateBaselineReady,
                RegimeState: row.RegimeState,
                AgainstActive: row.AgainstActive,
                MadCandidateVote: row.MadCandidate?.Vote == true,
                ZCandidateVote: row.ZCandidate?.Vote == true,
                EwmaCandidateVote: row.EwmaCandidate?.Vote == true,
                CriticalGap:
                    row.CandidateCriticalGap ||
                    (
                        string.Equals(
                            row.PolicyAction,
                            RegimePolicyAction.SuspendForDataQuality.ToString(),
                            StringComparison.Ordinal) &&
                        !adaptation.Observation.HasFrozenSensor
                    ),
                OperationalBaselineChanged: row.OperationalBaselineChanged,
                Promotion: row.Promotion)
            {
                FrozenSensor =
                    adaptation.Observation.HasFrozenSensor,

                IncreasingNoise =
                    increasingNoise,

                IncreasingNoiseStarted =
                    increasingNoiseStarted,

                IncreasingNoiseContinuation =
                    increasingNoiseContinuation,

                ZActiveVote =
                    zActive?.Aplicavel == true &&
                    zActive.VotouAnomalia,

                ZActiveScore =
                    zActive?.ScoreNormalizado ?? 0.0,

                MadActiveVote =
                    madActive?.Aplicavel == true &&
                    madActive.VotouAnomalia,

                MadActiveScore =
                    madActive?.ScoreNormalizado ?? 0.0,

                RateActiveVote =
                    rateActive?.Aplicavel == true &&
                    rateActive.VotouAnomalia,

                RateActiveScore =
                    rateActive?.ScoreNormalizado ?? 0.0,

                LegacyScoreNormalized =
                    legacyResult.ScoreNormalizado
            };

            _logger?.LogInformation(
                "B3.20.11 RUNTIME Cliente={ClienteId} Tag={TagName} Seq={Sequence} " +
                "Regime={RegimeState} CandidateReady={CandidateReady} AgainstActive={AgainstActive} " +
                "Incident={IncidentId} MadB={MadB} ZB={ZB} EwmaB={EwmaB} CriticalGap={CriticalGap} " +
                "LegacyScore={LegacyScore:F2} ZActive={ZActive}/{ZScore:F2} " +
                "MadActive={MadActive}/{MadScore:F2} RateActive={RateActive}/{RateScore:F2}",
                input.ClienteId,
                input.TagName,
                sequence,
                context.RegimeState,
                context.CandidateBaselineReady,
                context.AgainstActive,
                context.IncidentId ?? "-",
                context.MadCandidateVote,
                context.ZCandidateVote,
                context.EwmaCandidateVote,
                context.CriticalGap,
                context.LegacyScoreNormalized,
                context.ZActiveVote,
                context.ZActiveScore,
                context.MadActiveVote,
                context.MadActiveScore,
                context.RateActiveVote,
                context.RateActiveScore);

            return context;
        }
    }

    private static DetectorDebugItem? FindDetector(
        DetectorResult legacyResult,
        string detectorName)
    {
        return legacyResult.DetalhesDetectores
            .FirstOrDefault(
                detector =>
                    string.Equals(
                        detector.Nome,
                        detectorName,
                        StringComparison.OrdinalIgnoreCase));
    }

    private static DetectorInput CreateContiguousRuntimeInput(
        DetectorInput input)
    {
        var history =
            input.Historico;

        if (history.Count < 2 ||
            !input.QualidadeHistorico.HasCriticalGap)
        {
            return input;
        }

        var cadence =
            input.QualidadeHistorico.MedianIntervalMinutes;

        if (!double.IsFinite(cadence) ||
            cadence <= 0)
        {
            return input;
        }

        var criticalGapMinutes =
            cadence * 5.0;

        var startIndex = 0;

        for (var index = 1;
             index < history.Count;
             index++)
        {
            var gapMinutes =
                (history[index].DataHora -
                 history[index - 1].DataHora)
                .TotalMinutes;

            if (double.IsFinite(gapMinutes) &&
                gapMinutes > criticalGapMinutes)
            {
                startIndex = index;
            }
        }

        if (startIndex <= 0)
        {
            return input;
        }

        var contiguousHistory =
            history
                .Skip(startIndex)
                .Select(point =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = point.DataHora,
                        Valor = point.Valor
                    })
                .ToArray();

        if (contiguousHistory.Length < 10)
        {
            return input;
        }

        return new DetectorInput
        {
            ClienteId = input.ClienteId,
            TagName = input.TagName,
            TipoTag = input.TipoTag,
            ValorAtual = input.ValorAtual,
            DataHoraAtual = input.DataHoraAtual,
            MediaHistorica = input.MediaHistorica,
            DesvioPadraoHistorico = input.DesvioPadraoHistorico,

            /*
             * B3.24.1
             *
             * A reconstrução do histórico após gap crítico
             * não pode apagar os metadados de proteção da baseline.
             */
            VariacaoMinimaRelevante =
                input.VariacaoMinimaRelevante,

            ConfiancaVariacaoMinima =
                input.ConfiancaVariacaoMinima,

            VariacaoMinimaDefinidaPeloCliente =
                input.VariacaoMinimaDefinidaPeloCliente,

            Historico = contiguousHistory
        };
    }

    private static void UpdateDecisionEpisode(
        RuntimeFunctionalTagState state,
        DualBaselineTraceRecord row)
    {
        if (!state.DecisionEpisodeActive &&
            string.Equals(
                row.PolicyAction,
                RegimePolicyAction.ObserveTransition.ToString(),
                StringComparison.Ordinal) &&
            string.Equals(
                row.RegimeState,
                RegimeLifecycleState.TransitionSuspected.ToString(),
                StringComparison.Ordinal))
        {
            state.DecisionEpisodeActive = true;
            state.DecisionEpisodeId =
                row.EpisodeId ??
                $"runtime-episode:{row.Tag}:{row.MinuteIndex}";
        }
    }

    private static string? OpenIncidentId(
        ExperimentalIncidentAggregateState? aggregate)
    {
        var incident = aggregate?.Incident;

        if (incident is null)
            return null;

        if (incident.Lifecycle is
            StableDeviationIncidentLifecycle.Resolved or
            StableDeviationIncidentLifecycle.ClosedByRegimePromotion)
        {
            return null;
        }

        return incident.IncidentId;
    }
}


