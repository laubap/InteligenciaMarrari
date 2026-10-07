using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime;

namespace IAApi.Services.AnomalyDetection.Runtime.Regime.Evidence;


public sealed record RegimeShadowAdaptation(
    ReadingObservation Observation,
    RegimeEvidenceSnapshot Evidence,
    CandidateEvidenceState NextCandidateState,
    IReadOnlyDictionary<string, object?> Audit);

public sealed class RegimeEvidenceAdapter
{
    private readonly CandidateStabilityCalculator _stability = new();
    private readonly SlowDriftEvidenceCalculator _slowDrift = new();
    private readonly FrozenSensorEvidenceCalculator _frozenSensor = new();
    private readonly IncreasingNoiseEvidenceCalculator _increasingNoise = new();
    public RegimeShadowAdaptation Adapt(
        long sequence,
        DetectorInput input,
        DetectorResult result,
        RegimePolicyState policy,
        CandidateEvidenceState prior,
        RegimeShadowOptions options)
    {
        var context = input.Contexto;
        var profile =
            input.ObterPerfilComportamental(
                new TagBehaviorProfileResolver());

        var kind = profile.Type switch
        {
            TagBehaviorType.Continuous =>
                RegimeSignalKind.Continuous,

            TagBehaviorType.Discrete =>
                RegimeSignalKind.Discrete,

            TagBehaviorType.Counter =>
                RegimeSignalKind.Counter,

            TagBehaviorType.Setpoint =>
                RegimeSignalKind.Setpoint,

            _ =>
                RegimeSignalKind.Unknown
        };

        var protection =
            BaselineProtectionProfile.Build(
                input,
                context.Mediana);

        var roc =
            Find(
                result,
                "Taxa de varia");

        var level =
            Find(
                result,
                "patamar");

        var persistence =
            Find(
                result,
                "Persist");

        var rawRocEdge =
            roc?.Aplicavel == true &&
            roc.VotouAnomalia;

        /*
         * ConfirmaÃ§Ã£o bruta produzida pelo Level Shift Detector.
         * A arbitragem com Slow Drift Ã© feita mais abaixo.
         */
        var rawLevelConfirmed =
            level?.Aplicavel == true &&
            level.VotouAnomalia &&
            Metric(
                level,
                "possuiGapCriticoNasJanelas") < .5;

        var cadence =
            context.IntervaloMedianoMinutos;

        var qualityGap =
            input.QualidadeHistorico
                .HasCriticalGap;

        var frozenCalculation =
            _frozenSensor.Calculate(
                input,
                kind,
                options);

        var frozenSensor =
            !qualityGap &&
            frozenCalculation.HasEvidence;

        var dataQualityIssue =
            qualityGap ||
            frozenSensor;

        var persistent =
            persistence?.Aplicavel == true &&
            (
                persistence.VotouAnomalia ||
                Metric(
                    persistence,
                    "duracaoMinutos") > 0
            );        var slowDriftCalculation =
            _slowDrift.Calculate(
                input,
                kind,
                protection,
                options);

        var increasingNoiseCalculation =
            _increasingNoise.Calculate(
                input,
                kind,
                protection,
                policy,
                prior,
                options);

        /*
         * Slow drift Ã© evidÃªncia de regime.
         * NÃ£o cria voto novo no Ensemble Legacy.
         *
         * Se existe um edge abrupto de ROC ou Level Shift confirmado,
         * preservamos a semÃ¢ntica desses sinais e nÃ£o rotulamos
         * o mesmo instante como slow drift.
         */
        /*
         * EvidÃªncia matemÃ¡tica bruta de slow drift.
         *
         * Ela permanece verdadeira mesmo quando outro detector (por exemplo
         * Level Shift) tambÃ©m dispara no mesmo instante.
         */
        var slowDriftActive =
            slowDriftCalculation.HasEvidence;

        /*
         * B3.22.2 V4 â€” arbitragem de evidÃªncias.
         *
         * O RegimePolicyEngine abre CandidateRegime quando recebe
         * HasLevelShiftConfirmation=true, mesmo que
         * CandidateFormationConfirmed=false.
         *
         * Portanto, enquanto a evidÃªncia matemÃ¡tica de slow drift continuar
         * ativa, um Level Shift simultÃ¢neo Ã© tratado apenas como evidÃªncia
         * bruta/auditÃ¡vel, e NÃƒO como confirmaÃ§Ã£o de plateau para a policy.
         *
         * Assim:
         * - rampa lenta + Level Shift momentÃ¢neo => continua Slow Drift;
         * - Level Shift sem Slow Drift ativo => mantÃ©m comportamento anterior.
         */
        var levelConfirmed =
            rawLevelConfirmed &&
            !slowDriftActive;

        /*
         * B3.22.5 V2b - ROC x sustained increasing noise arbitration.
         *
         * rawRocEdge remains auditable. It is suppressed only as a
         * MATERIAL REGIME TRANSITION when increasing noise is active
         * and the current reading is still compatible with the recent
         * noisy distribution.
         *
         * A point outside this recent-noise envelope keeps the ROC edge,
         * preserving abrupt spike behavior.
         */
        var increasingNoiseUnderlying =
            increasingNoiseCalculation.HasEvidence;

        var increasingNoiseActive =
            increasingNoiseUnderlying &&
            !slowDriftActive;

        var increasingNoiseEnvelopeScales =
            Math.Max(
                1.0,
                options.MinimumDisplacementRobustScales);

        var increasingNoiseEnvelopeLimit =
            increasingNoiseActive &&
            increasingNoiseCalculation.RecentScale > 0
                ? increasingNoiseCalculation.RecentScale *
                  increasingNoiseEnvelopeScales
                : 0;

        var increasingNoiseCurrentDistance =
            increasingNoiseActive
                ? Math.Abs(
                    input.ValorAtual -
                    increasingNoiseCalculation.RecentCenter)
                : 0;

        var currentWithinIncreasingNoiseEnvelope =
            increasingNoiseActive &&
            increasingNoiseEnvelopeLimit > 0 &&
            increasingNoiseCurrentDistance <=
                increasingNoiseEnvelopeLimit;

        var rocSuppressedByIncreasingNoise =
            rawRocEdge &&
            increasingNoiseActive &&
            !levelConfirmed &&
            !slowDriftActive;

        var edge =
            rawRocEdge &&
            !rocSuppressedByIncreasingNoise;

        /*
         * EvidÃªncia publicada para a policy.
         * Um edge abrupto real continua tendo prioridade sobre Slow Drift.
         */
        var slowDrift =
            !edge &&
            slowDriftActive;

        var state =
            prior;

        /*
         * EpisÃ³dio iniciado por ROC:
         * mantÃ©m o comportamento jÃ¡ existente, iniciando imediatamente
         * o buffer candidato.
         */
        if (!state.EpisodeStarted &&
            edge)
        {
            state =
                StartCandidateBuffer(
                    state: new CandidateEvidenceState(
                        context.Mediana,
                        Math.Max(
                            context.EscalaRobusta,
                            protection.Deadband),
                        protection.Deadband,
                        [],
                        EpisodeId:
                            $"episode:{input.TagName}:{sequence}",
                        FormationEvidenceSources:
                            ["roc-edge"]),
                    sequence,
                    input,
                    "roc-edge");
        }
        /*
         * EpisÃ³dio iniciado por slow drift:
         * abre somente a observaÃ§Ã£o de regime.
         *
         * NÃƒO inicia ainda o buffer candidato, porque durante a rampa
         * nÃ£o existe um novo plateau estÃ¡vel para promover.
         */
        else if (!state.EpisodeStarted &&
                 slowDrift)
        {
            state =
                new CandidateEvidenceState(
                    context.Mediana,
                    Math.Max(
                        context.EscalaRobusta,
                        protection.Deadband),
                    protection.Deadband,
                    [],
                    EpisodeId:
                        $"episode:{input.TagName}:{sequence}",
                    FormationEvidenceSources:
                        ["slow-drift"]);
        }
        /*
         * Um episÃ³dio de slow drift pode posteriormente ganhar um edge
         * abrupto real. Nesse caso passamos a tratÃ¡-lo como formaÃ§Ã£o
         * candidata por transiÃ§Ã£o material.
         */
        else if (state.EpisodeStarted &&
                 !state.CandidateStarted &&
                 edge)
        {
            state =
                StartCandidateBuffer(
                    state,
                    sequence,
                    input,
                    "roc-edge");
        }
        else if (state.EpisodeStarted &&
                 !state.CandidateStarted &&
                 slowDrift)
        {
            var sources =
                new HashSet<string>(
                    state.FormationEvidenceSources ??
                    [],
                    StringComparer.Ordinal);

            sources.Add(
                "slow-drift");

            state =
                state with
                {
                    FormationEvidenceSources =
                        sources
                            .OrderBy(
                                value =>
                                    value,
                                StringComparer.Ordinal)
                            .ToArray()
                };
        }

        var activeCenter =
            state.EpisodeStarted
                ? state.ActiveCenter
                : context.Mediana;

        var activeScale =
            state.EpisodeStarted
                ? state.ActiveScale
                : Math.Max(
                    context.EscalaRobusta,
                    protection.Deadband);

        var deadband =
            state.EpisodeStarted
                ? state.Deadband
                : protection.Deadband;

        var displacementLimit =
            Math.Max(
                deadband *
                options.MinimumDisplacementDeadbands,
                activeScale *
                options.MinimumDisplacementRobustScales);

        var outside =
            Math.Abs(
                input.ValorAtual -
                activeCenter) >
            displacementLimit;

        var returned =
            false;

        if (state.EpisodeStarted)
        {
            var sources =
                new HashSet<string>(
                    state.FormationEvidenceSources ??
                    [],
                    StringComparer.Ordinal);

            var slowDriftEpisode =
                sources.Contains(
                    "slow-drift") &&
                !sources.Contains(
                    "roc-edge");

            /*
             * Se uma tentativa de estabilizaÃ§Ã£o comeÃ§ou depois do drift
             * mas a deriva reapareceu antes da abertura formal do candidato,
             * descartamos somente esse buffer de plateau.
             *
             * O episÃ³dio de slow drift continua ativo.
             */
            if (slowDriftEpisode &&
                slowDriftActive &&
                state.CandidateStarted &&
                policy.Lifecycle ==
                    RegimeLifecycleState.TransitionSuspected &&
                sources.Contains(
                    "post-slow-drift-stabilization"))
            {
                state =
                    state with
                    {
                        Points =
                            [],
                        BufferStartedMinuteIndex =
                            null,
                        BufferStartedTimestamp =
                            null,
                        BufferInitialValue =
                            null,
                        PointCountBeforeCurrentEpisode =
                            0,
                        ConsecutiveReturnPoints =
                            0,
                        FormationEvidenceSources =
                            ["slow-drift"]
                    };

                sources =
                    new HashSet<string>(
                        state.FormationEvidenceSources ??
                        [],
                        StringComparer.Ordinal);
            }

            /*
             * Durante slow drift puro nÃ£o formamos candidato.
             *
             * O buffer candidato sÃ³ comeÃ§a quando:
             * - a evidÃªncia de drift deixa de estar ativa;
             * - o sinal ainda estÃ¡ materialmente fora do baseline;
             * - a policy continua observando a transiÃ§Ã£o;
             * - existe persistÃªncia temporal ou Level Shift.
             *
             * Isso representa o inÃ­cio de um possÃ­vel plateau
             * posterior Ã  rampa.
             */
            if (slowDriftEpisode &&
                !state.CandidateStarted &&
                !slowDriftActive &&
                !dataQualityIssue &&
                outside &&
                policy.Lifecycle ==
                    RegimeLifecycleState.TransitionSuspected &&
                (
                    persistent ||
                    levelConfirmed
                ))
            {
                state =
                    StartCandidateBuffer(
                        state,
                        sequence,
                        input,
                        "post-slow-drift-stabilization");

                sources =
                    new HashSet<string>(
                        state.FormationEvidenceSources ??
                        [],
                        StringComparer.Ordinal);
            }

            var points =
                state.Points.ToList();

            var returnPoints =
                dataQualityIssue ||
                outside
                    ? 0
                    : state
                        .ConsecutiveReturnPoints +
                      1;

            if (state.CandidateStarted &&
                outside &&
                (
                    points.Count == 0 ||
                    points[^1].Sequence !=
                        sequence
                ))
            {
                points.Add(
                    new CandidatePoint(
                        sequence,
                        input.DataHoraAtual,
                        input.ValorAtual));

                var cutoff =
                    input.DataHoraAtual
                        .AddMinutes(
                            -options
                                .CandidateBufferHorizonMinutes);

                points =
                    points
                        .Where(
                            point =>
                                point.Timestamp >=
                                cutoff)
                        .TakeLast(
                            options
                                .CandidateBufferCapacity)
                        .ToList();
            }

            returned =
                !dataQualityIssue &&
                returnPoints >=
                    options.ReturnMinimumPoints;

            state =
                state with
                {
                    Points =
                        points.AsReadOnly(),

                    ConsecutiveReturnPoints =
                        returnPoints,

                    FormationEvidenceSources =
                        sources
                            .OrderBy(
                                value =>
                                    value,
                                StringComparer.Ordinal)
                            .ToArray()
                };
        }

        var duration =
            state.Points.Count > 1
                ? (
                    state.Points[^1].Timestamp -
                    state.Points[0].Timestamp
                  ).TotalMinutes
                : 0;

        var formationSources =
            new HashSet<string>(
                state.FormationEvidenceSources ??
                [],
                StringComparer.Ordinal);

        var slowDriftEpisodeForFormation =
            formationSources.Contains(
                "slow-drift") &&
            !formationSources.Contains(
                "roc-edge");

        var postSlowDriftStabilization =
            formationSources.Contains(
                "post-slow-drift-stabilization");

        /*
         * Para episÃ³dios iniciados por ROC, preserva a semÃ¢ntica anterior:
         * depois de pontos/duraÃ§Ã£o mÃ­nimos, TransitionSuspected pode formar
         * candidato.
         *
         * Para episÃ³dios iniciados por slow drift, TransitionSuspected
         * sozinho NÃƒO basta. Ã‰ necessÃ¡rio que a deriva tenha cessado e
         * que um buffer pÃ³s-drift tenha sido iniciado.
         */
        var lifecycleAllowsFormation =
            policy.Lifecycle ==
                RegimeLifecycleState.TransitionSuspected &&
            (
                !slowDriftEpisodeForFormation ||
                (
                    !slowDriftActive &&
                    postSlowDriftStabilization
                )
            );

        var formed =
            state.Points.Count >=
                options.FormationMinimumPoints &&
            duration >=
                options.FormationMinimumMinutes &&
            (
                levelConfirmed ||
                edge ||
                lifecycleAllowsFormation
            );

        if (state.CandidateStarted)
        {
            var sources =
                new HashSet<string>(
                    state.FormationEvidenceSources ??
                    [],
                    StringComparer.Ordinal);

            if (edge)
                sources.Add(
                    "roc-edge");

            if (slowDrift)
                sources.Add(
                    "slow-drift");

            if (levelConfirmed)
                sources.Add(
                    "level-shift");

            if (persistent)
                sources.Add(
                    "persistence-temporal-correlated");

            if (outside)
                sources.Add(
                    "outside-active-regime");

            if (formed)
            {
                sources.Add(
                    "minimum-points");

                sources.Add(
                    "minimum-duration");

                sources.Add(
                    "sustained-displacement");
            }

            state =
                state with
                {
                    FormationEvidenceSources =
                        sources
                            .OrderBy(
                                value =>
                                    value,
                                StringComparer.Ordinal)
                            .ToArray()
                };
        }

        var stability =
            state.CandidateStarted
                ? _stability.Calculate(
                    state,
                    cadence,
                    options)
                : CandidateStability
                    .Insufficient;

        var candidateValues =
            state.Points
                .Select(
                    point =>
                        point.Value)
                .ToArray();

        var candidateMedian =
            candidateValues.Length > 0
                ? DetectorMath.Median(
                    candidateValues)
                : (double?)null;

        var candidateMad =
            candidateMedian.HasValue
                ? DetectorMath.Mad(
                    candidateValues,
                    candidateMedian.Value)
                : (double?)null;

        var candidateRobustScale =
            candidateMad.HasValue
                ? Math.Max(
                    1.4826 *
                    candidateMad.Value,
                    state.Deadband)
                : (double?)null;
        state =
            state with
            {
                NoiseReferenceRegimeId =
                    increasingNoiseCalculation.ReferenceRegimeId,

                NoiseReferenceCenter =
                    increasingNoiseCalculation.ReferenceCenter,

                NoiseReferenceScale =
                    increasingNoiseCalculation.ReferenceScale,

                NoiseReferenceEstablishedAt =
                    increasingNoiseCalculation.ReferenceEstablishedAt,

                NoiseReferencePointCount =
                    increasingNoiseCalculation.ReferencePointCount,

                NoiseReferenceDurationMinutes =
                    increasingNoiseCalculation.ReferenceDurationMinutes
            };

        var evidence =
            new RegimeEvidenceSnapshot(
                hasMaterialTransitionEdge:
                    edge,

                hasLevelShiftConfirmation:
                    levelConfirmed,

                hasSlowDriftEvidence:
                    slowDrift,

                isOutsideActiveRegime:
                    outside,

                hasReturnedToActiveRegime:
                    returned,

                hasPersistentDeviation:
                    persistent,

                candidateFormationConfirmed:
                    formed,

                candidateInvalidated:
                    returned,

                proposedCandidateRegimeId:
                    formed
                        ? $"candidate:{input.TagName}:{sequence}"
                        : null,

                candidateStability:
                    stability,

                legitimacy:
                    LegitimacyEvidence.None,

                sourceEvidenceIds:
                    Sources(
                        edge,
                        levelConfirmed,
                        persistent,
                        slowDrift,
                        increasingNoiseActive),

                hasIncreasingNoiseEvidence:
                    increasingNoiseActive);

        var observation =
            new ReadingObservation(
                sequence,
                input.DataHoraAtual,
                input.ValorAtual,
                cadence,
                kind,
                double.IsFinite(
                    input.ValorAtual),
                qualityGap)
            {
                HasFrozenSensor =
                    frozenSensor
            };

        var audit =
            new Dictionary<string, object?>
            {
                ["rocEdge"] =
                    edge,

                ["rawRocEdge"] =
                    rawRocEdge,

                ["rocSuppressedByIncreasingNoise"] =
                    rocSuppressedByIncreasingNoise,

                ["currentWithinIncreasingNoiseEnvelope"] =
                    currentWithinIncreasingNoiseEnvelope,

                ["increasingNoiseEnvelopeLimit"] =
                    increasingNoiseEnvelopeLimit,

                ["increasingNoiseCurrentDistance"] =
                    increasingNoiseCurrentDistance,

                ["levelShiftConfirmation"] =
                    levelConfirmed,

                ["rawLevelShiftConfirmation"] =
                    rawLevelConfirmed,

                ["levelShiftSuppressedBySlowDrift"] =
                    rawLevelConfirmed &&
                    slowDriftActive,

                ["outsideActive"] =
                    outside,

                ["slowDriftEvidence"] =
                    slowDrift,

                ["slowDriftUnderlyingEvidence"] =
                    slowDriftActive,

                ["slowDriftApplicable"] =
                    slowDriftCalculation
                        .Applicable,

                ["slowDriftReason"] =
                    slowDriftCalculation
                        .Reason,

                ["slowDriftPointCount"] =
                    slowDriftCalculation
                        .PointCount,

                ["slowDriftDurationMinutes"] =
                    slowDriftCalculation
                        .DurationMinutes,

                ["slowDriftSlopePerMinute"] =
                    slowDriftCalculation
                        .SlopePerMinute,

                ["slowDriftResidualScale"] =
                    slowDriftCalculation
                        .ResidualScale,

                ["slowDriftTrendDisplacement"] =
                    slowDriftCalculation
                        .TrendDisplacement,

                ["slowDriftStructuralDisplacement"] =
                    slowDriftCalculation
                        .StructuralDisplacement,

                ["slowDriftTrendToNoiseRatio"] =
                    slowDriftCalculation
                        .TrendToNoiseRatio,

                ["slowDriftStructuralToNoiseRatio"] =
                    slowDriftCalculation
                        .StructuralToNoiseRatio,

                ["slowDriftDirectionalConsistency"] =
                    slowDriftCalculation
                        .DirectionalConsistency,

                ["slowDriftFirstMedian"] =
                    slowDriftCalculation
                        .FirstMedian,

                ["slowDriftLastMedian"] =
                    slowDriftCalculation
                        .LastMedian,

                ["frozenSensorEvidence"] =
                    frozenSensor,

                ["frozenSensorApplicable"] =
                    frozenCalculation
                        .Applicable,

                ["frozenSensorReason"] =
                    frozenCalculation
                        .Reason,

                ["frozenSensorConsecutivePoints"] =
                    frozenCalculation
                        .ConsecutivePoints,

                ["frozenSensorDurationMinutes"] =
                    frozenCalculation
                        .DurationMinutes,

                ["frozenSensorValue"] =
                    frozenCalculation
                        .FrozenValue,

                ["frozenSensorPriorVariationCount"] =
                    frozenCalculation
                        .PriorVariationCount,                ["frozenSensorPriorZeroRate"] =
                    frozenCalculation
                        .PriorZeroRate,

                ["increasingNoiseEvidence"] =
                    increasingNoiseActive,

                ["increasingNoiseUnderlyingEvidence"] =
                    increasingNoiseUnderlying,

                ["increasingNoiseSuppressedBySlowDrift"] =
                    increasingNoiseUnderlying &&
                    slowDriftActive,

                ["increasingNoiseApplicable"] =
                    increasingNoiseCalculation.Applicable,

                ["increasingNoiseReason"] =
                    increasingNoiseCalculation.Reason,

                ["increasingNoiseReferenceRegimeId"] =
                    increasingNoiseCalculation.ReferenceRegimeId,

                ["increasingNoiseReferenceUpdated"] =
                    increasingNoiseCalculation.ReferenceUpdated,

                ["increasingNoiseReferencePointCount"] =
                    increasingNoiseCalculation.ReferencePointCount,

                ["increasingNoiseReferenceDurationMinutes"] =
                    increasingNoiseCalculation.ReferenceDurationMinutes,

                ["increasingNoiseReferenceCenter"] =
                    increasingNoiseCalculation.ReferenceCenter,

                ["increasingNoiseReferenceScale"] =
                    increasingNoiseCalculation.ReferenceScale,

                ["increasingNoiseReferenceEstablishedAt"] =
                    increasingNoiseCalculation.ReferenceEstablishedAt,

                ["increasingNoiseRecentPointCount"] =
                    increasingNoiseCalculation.RecentPointCount,

                ["increasingNoiseRecentDurationMinutes"] =
                    increasingNoiseCalculation.RecentDurationMinutes,

                ["increasingNoiseRecentCenter"] =
                    increasingNoiseCalculation.RecentCenter,

                ["increasingNoiseRecentScale"] =
                    increasingNoiseCalculation.RecentScale,

                ["increasingNoiseScaleRatio"] =
                    increasingNoiseCalculation.ScaleRatio,

                ["increasingNoiseCenterShift"] =
                    increasingNoiseCalculation.CenterShift,

                ["increasingNoiseCenterShiftScales"] =
                    increasingNoiseCalculation.CenterShiftScales,

                ["slowDriftEpisode"] =
                    formationSources.Contains(
                        "slow-drift"),

                ["postSlowDriftStabilization"] =
                    formationSources.Contains(
                        "post-slow-drift-stabilization"),

                ["activeCenter"] =
                    activeCenter,

                ["activeScale"] =
                    activeScale,

                ["deadband"] =
                    deadband,

                ["displacementLimit"] =
                    displacementLimit,

                ["candidatePoints"] =
                    state.Points.Count,

                ["candidateDurationMinutes"] =
                    duration,

                ["persistenceSourceUsesMad"] =
                    Metric(
                        persistence,
                        "usaMad") >= .5,

                ["profileType"] =
                    profile.Type.ToString(),

                ["profileConfidence"] =
                    profile.Confidence,

                ["episodeId"] =
                    state.EpisodeId,

                ["candidateBufferStartedMinuteIndex"] =
                    state.BufferStartedMinuteIndex,

                ["candidateBufferStartedTimestamp"] =
                    state.BufferStartedTimestamp,

                ["candidateBufferInitialValue"] =
                    state.BufferInitialValue,

                ["candidateBufferPointCountBeforeCurrentEpisode"] =
                    state.PointCountBeforeCurrentEpisode,

                ["formationEvidenceSources"] =
                    state.FormationEvidenceSources ??
                    [],

                ["dispersionIsStable"] =
                    stability
                        .DispersionIsStable,

                ["residualSlopeIsStable"] =
                    stability
                        .ResidualSlopeIsStable,

                ["candidateMedian"] =
                    candidateMedian,

                ["candidateMad"] =
                    candidateMad,

                ["candidateRobustScale"] =
                    candidateRobustScale,

                ["candidateCriticalGap"] =
                    stability
                        .HasCriticalGap
            };

        return new RegimeShadowAdaptation(
            observation,
            evidence,
            state,
            audit);
    }

    private static CandidateEvidenceState
        StartCandidateBuffer(
            CandidateEvidenceState state,
            long sequence,
            DetectorInput input,
            string source)
    {
        var sources =
            new HashSet<string>(
                state.FormationEvidenceSources ??
                [],
                StringComparer.Ordinal);

        sources.Add(
            source);

        return state with
        {
            Points =
                [
                    new CandidatePoint(
                        sequence,
                        input.DataHoraAtual,
                        input.ValorAtual)
                ],

            BufferStartedMinuteIndex =
                sequence,

            BufferStartedTimestamp =
                input.DataHoraAtual,

            BufferInitialValue =
                input.ValorAtual,

            PointCountBeforeCurrentEpisode =
                0,

            FormationEvidenceSources =
                sources
                    .OrderBy(
                        value =>
                            value,
                        StringComparer.Ordinal)
                    .ToArray(),

            ConsecutiveReturnPoints =
                0
        };
    }

    private static DetectorDebugItem?
        Find(
            DetectorResult result,
            string fragment) =>
        result.DetalhesDetectores
            .FirstOrDefault(
                detector =>
                    detector.Nome.Contains(
                        fragment,
                        StringComparison
                            .OrdinalIgnoreCase));

    private static double Metric(
        DetectorDebugItem? item,
        string name) =>
        item is not null &&
        item.Metricas.TryGetValue(
            name,
            out var value)
            ? value
            : 0;

    private static string[] Sources(
        bool edge,
        bool level,
        bool persistent,
        bool slowDrift,
        bool increasingNoise) =>
        new[]
        {
            edge
                ? "roc-edge"
                : null,

            level
                ? "level-shift"
                : null,

            persistent
                ? "persistence-temporal-correlated"
                : null,

            slowDrift
                ? "slow-drift"
                : null,

            increasingNoise
                ? "increasing-noise"
                : null
        }
        .Where(
            value =>
                value is not null)
        .Cast<string>()
        .ToArray();
}


