using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

/// <summary>
/// Motor observacional coverage-aware. Nao promove regimes e nao altera o
/// baseline operacional. Toda avaliacao usa exclusivamente snapshots ate t-1.
/// </summary>
public sealed class DualBaselineShadowEngine
{
    private readonly DualBaselineShadowOptions _options;
    public DualBaselineShadowEngine(DualBaselineShadowOptions options) => _options = options;

    public DualBaselineTraceRecord Evaluate(
        int window, long minute, DetectorInput input, DetectorResult original,
        RegimePolicyDecision policy, DualBaselineTagState state)
    {
        var point = new DualBaselinePoint(minute, input.DataHoraAtual, input.ValorAtual);
        if (policy.Action == RegimePolicyAction.ObserveTransition &&
            policy.PreviousState.Lifecycle == RegimeLifecycleState.StableKnownRegime &&
            (state.Active is null || state.EpisodeId is null))
        {
            var prior = input.Historico.Select(x => new DualBaselinePoint(-1, x.DataHora, x.Valor)).ToArray();
            var version = ++state.ActiveBaselineVersion;
            state.Active = DualBaselineMath.BuildReference(
                $"active:{window}:{input.TagName}:v{version}", version, prior, input, _options);
            state.ActiveEwma = DualBaselineMath.InitializeEwma(state.Active, _options);
            state.EpisodeId = $"dual-episode:{window}:{input.TagName}:{minute}";
            state.LastResetReason = null;
        }

        // Snapshot t-1 -> readiness -> avaliacao t -> inclusao posterior de t.
        var readiness = AuditCoverage(state, input, policy.NextState.Lifecycle);
        if (!state.CandidateReady && readiness.Status == DualBaselineReadiness.Ready)
        {
            var coverage = readiness.InferredCoverageSamples;
            state.FormationPoints.AddRange(state.CandidateCollectionPoints.Take(coverage));
            state.ValidationPoints.AddRange(state.CandidateCollectionPoints.Skip(coverage).Take(coverage));
            var referencePoints = state.FormationPoints.Concat(state.ValidationPoints).ToArray();
            state.CandidateBaselineVersion++;
            state.Candidate = DualBaselineMath.BuildReference(
                $"candidate:{window}:{input.TagName}:v{state.CandidateBaselineVersion}",
                state.CandidateBaselineVersion, referencePoints, input, _options);
            state.CandidateEwma = DualBaselineMath.InitializeEwma(state.Candidate, _options);
            state.CandidateReady = true;
        }

        var activeMad = state.Active is null ? null : DualBaselineMath.EvaluateMad(input.ValorAtual, state.Active, _options.MadThreshold);
        var activeZ = state.Active is null ? null : DualBaselineMath.EvaluateZ(input.ValorAtual, state.Active, _options.ZScoreThreshold);
        var activeEwma = state.ActiveEwma is null ? null : DualBaselineMath.EvaluateAndAdvance(
            state.ActiveEwma, input.DataHoraAtual, input.ValorAtual, state.Active!.Deadband, _options);
        var candidateMad = state.Candidate is null ? NotReadyDetector() : DualBaselineMath.EvaluateMad(input.ValorAtual, state.Candidate, _options.MadThreshold);
        var candidateZ = state.Candidate is null ? NotReadyDetector() : DualBaselineMath.EvaluateZ(input.ValorAtual, state.Candidate, _options.ZScoreThreshold);
        var candidateEwma = state.CandidateEwma is null ? NotReadyEwma() : DualBaselineMath.EvaluateAndAdvance(
            state.CandidateEwma, input.DataHoraAtual, input.ValorAtual, state.Candidate!.Deadband, _options);
        var activeState = Aggregate(activeMad, activeZ, activeEwma);
        var candidateState = Aggregate(candidateMad, candidateZ, candidateEwma);
        if (state.CandidateReady)
            state.ConsecutiveCandidateFitAnomalies = candidateState == DualStatisticalState.Abnormal
                ? state.ConsecutiveCandidateFitAnomalies + 1 : 0;

        var provisional = state.CandidateCollectionPoints;
        var provisionalHistory = provisional.Select(P).ToArray();
        var provisionalContext = new DetectorContext(provisionalHistory);
        var currentInActive = state.Active?.Points.Any(x => x.DataHora == input.DataHoraAtual) == true;
        var currentInCandidate = state.Candidate?.Points.Any(x => x.DataHora == input.DataHoraAtual) == true;
        var record = new DualBaselineTraceRecord
        {
            FastWindowIndex = window, MinuteIndex = minute, Timestamp = input.DataHoraAtual,
            Tag = input.TagName, CurrentValue = input.ValorAtual,
            RegimeState = policy.NextState.Lifecycle.ToString(), EpisodeId = state.EpisodeId,
            CandidateId = policy.NextState.CandidateRegimeId ?? state.CandidateId,
            CandidateBaselineId = state.Candidate?.Id,
            PolicyAction = policy.Action.ToString(),
            FrozenSensorDataQuality = policy.Reasons.Any(x =>
                x.Code == RegimeTransitionReasonCode.FrozenSensorDetected),
            ActiveBaselineId = state.Active?.Id,
            ActiveBaselineVersion = state.Active?.Version, ActiveBaselineFrozen = state.Active is not null,
            ActiveStartTimestamp = state.Active?.StartTimestamp, ActiveEndTimestamp = state.Active?.EndTimestamp,
            ActiveSampleCount = state.Active?.SampleCount, ActiveMean = state.Active?.Mean,
            ActiveStd = state.Active?.StdDev, ActiveMedian = state.Active?.Median,
            ActiveMad = state.Active?.Mad, ActiveRobustScale = state.Active?.RobustScale,
            ActiveDeadband = state.Active?.Deadband, ActiveDeadbandSource = state.Active?.DeadbandSource,
            ActiveCadenceMinutes = state.Active?.CadenceMinutes,
            CandidateReadiness = state.CandidateReady ? "Ready" : readiness.Status.ToString(),
            CandidateFormationStarted = state.CandidateOpened,
            CandidateFormationCoverage = readiness.FormationCoverage.ToString(),
            CandidateValidationCoverage = readiness.ValidationCoverage.ToString(),
            CandidateBaselineReady = state.CandidateReady,
            CandidateBaselineVersion = state.CandidateBaselineVersion,
            CandidateReadinessReasons = readiness.Reasons,
            TransitionPointCount = state.TransitionPoints.Count,
            CandidateCollectionPointCount = state.CandidateCollectionPoints.Count,
            CandidateFormationPointCount = state.FormationPoints.Count,
            CandidateValidationPointCount = state.ValidationPoints.Count,
            CandidatePostReadyObservationCount = state.PostReadyObservationPoints.Count,
            CandidateInferredCoverageSamples = readiness.InferredCoverageSamples,
            CandidateSampleCount = state.Candidate?.SampleCount ?? readiness.SampleCount,
            CandidateIndependentSamples = readiness.IndependentSampleCount,
            CandidateDurationMinutes = readiness.ObservedDurationMinutes,
            CandidateCadenceMinutes = readiness.CadenceMinutes,
            CandidateLargestGapMinutes = readiness.LargestGapMinutes,
            CandidateCriticalGap = readiness.HasCriticalGap,
            CandidateCenterStable = readiness.CenterIsStable,
            CandidateScaleStable = readiness.ScaleIsStable,
            CandidateResidualSlopeStable = readiness.ResidualSlopeIsStable,
            CandidateFormationDurationMinutes = readiness.FormationDurationMinutes,
            CandidateValidationDurationMinutes = readiness.ValidationDurationMinutes,
            CandidateFormationMean = readiness.FormationMean,
            CandidateValidationMean = readiness.ValidationMean,
            CandidateFormationStdDev = readiness.FormationStdDev,
            CandidateValidationStdDev = readiness.ValidationStdDev,
            CandidateFormationRange = readiness.FormationRange,
            CandidateValidationRange = readiness.ValidationRange,
            CandidateMaximumQuantileDelta = readiness.MaximumQuantileDelta,
            CandidateAlignedMeanAbsoluteDifference = readiness.AlignedMeanAbsoluteDifference,
            CandidateStructuralRepetitionObserved = readiness.StructuralRepetitionObserved,
            CandidateFormationMedian = readiness.FormationMedian,
            CandidateValidationMedian = readiness.ValidationMedian,
            CandidateFormationRobustScale = readiness.FormationRobustScale,
            CandidateValidationRobustScale = readiness.ValidationRobustScale,
            CandidateMean = state.Candidate?.Mean ?? (provisional.Count > 0 ? provisional.Average(x => x.Value) : null),
            CandidateStd = state.Candidate?.StdDev ?? (provisional.Count > 0 ? provisionalContext.DesvioPadrao : null),
            CandidateMedian = state.Candidate?.Median ?? (provisional.Count > 0 ? provisionalContext.Mediana : null),
            CandidateMad = state.Candidate?.Mad ?? (provisional.Count > 0 ? provisionalContext.Mad : null),
            CandidateEffectiveScaleOrigin = state.Candidate?.MadEffectiveScaleOrigin ?? "NotReady:NoEffectiveScale",
            MadActive = activeMad, MadCandidate = candidateMad, ZActive = activeZ, ZCandidate = candidateZ,
            EwmaActive = activeEwma, EwmaCandidate = candidateEwma,
            AgainstActive = activeState.ToString(), AgainstCandidate = candidateState.ToString(),
            CandidateFit = candidateState == DualStatisticalState.Normal ? "Compatible" : candidateState.ToString(),
            CandidateDegradationObserved = state.CandidateReady && candidateState == DualStatisticalState.Abnormal,
            ConsecutiveCandidateFitAnomalies = state.ConsecutiveCandidateFitAnomalies,
            CurrentReadingInActiveReference = currentInActive,
            CurrentReadingInCandidateReference = currentInCandidate,
            OperationalBaselineChanged = false, Promotion = false, Legitimacy = "None",
            OriginalFinalAnomaly = original.EhAnomalia, OriginalFinalScore = original.Score,
            CandidateResetReason = state.LastResetReason
        };
        ApplyAfterEvaluation(point, policy, state);
        return record;
    }

    public void ResetCandidate(DualBaselineTagState state, string reason)
    {
        state.TransitionPoints.Clear(); state.CandidateCollectionPoints.Clear();
        state.FormationPoints.Clear(); state.ValidationPoints.Clear(); state.PostReadyObservationPoints.Clear();
        state.Candidate = null; state.CandidateEwma = null; state.CandidateOpened = false;
        state.CandidateReady = false; state.CandidateId = null; state.CandidateBaselineVersion = 0;
        state.ConsecutiveCandidateFitAnomalies = 0; state.LastResetReason = reason;
    }

    public void ResetEpisode(DualBaselineTagState state, string reason)
    {
        ResetCandidate(state, reason);
        state.Active = null;
        state.ActiveEwma = null;
        state.EpisodeId = null;
    }

    private void ApplyAfterEvaluation(DualBaselinePoint point, RegimePolicyDecision policy, DualBaselineTagState state)
    {
        if (policy.Action == RegimePolicyAction.RestoreActiveRegime)
        { ResetEpisode(state, policy.Action.ToString()); return; }
        if (policy.Action is RegimePolicyAction.RejectCandidate or RegimePolicyAction.SuspendForDataQuality)
        { ResetCandidate(state, policy.Action.ToString()); return; }
        if (policy.Action == RegimePolicyAction.ObserveTransition && !state.CandidateOpened)
        { state.TransitionPoints.Add(point); return; }
        if (policy.Action == RegimePolicyAction.OpenCandidate)
        { state.CandidateOpened = true; state.CandidateId = policy.NextState.CandidateRegimeId; }
        if (!state.CandidateOpened) return;
        if (state.CandidateReady) state.PostReadyObservationPoints.Add(point);
        else state.CandidateCollectionPoints.Add(point);
    }

    private CandidateReadinessAudit AuditCoverage(
        DualBaselineTagState state, DetectorInput input, RegimeLifecycleState lifecycle)
    {
        var points = state.CandidateCollectionPoints;
        var cadence = points.Count > 1 ? new DetectorContext(points.Select(P).ToArray()).IntervaloMedianoMinutos : input.Contexto.IntervaloMedianoMinutos;
        var duration = points.Count > 1 ? (points[^1].Timestamp - points[0].Timestamp).TotalMinutes : 0;
        var gaps = points.Zip(points.Skip(1), (a, b) => (b.Timestamp - a.Timestamp).TotalMinutes).Where(x => x > 0).ToArray();
        var largestGap = gaps.Length == 0 ? 0 : gaps.Max();
        var criticalLimit = input.QualidadeHistorico.CriticalGapThresholdMinutes > 0
            ? input.QualidadeHistorico.CriticalGapThresholdMinutes : cadence * 5;
        var critical = criticalLimit > 0 && largestGap > criticalLimit;
        var independent = cadence > 0 ? Math.Min(points.Count, 1 + (int)Math.Floor(duration / cadence)) : 0;

        CoverageComparison? best = null;
        if (points.Count >= _options.FormationMinimumSamples * 2 &&
            points.Count % 2 == 0)
        {
            var coverage = points.Count / 2;
            var comparison = CompareCoverage(points.Take(coverage).ToArray(),
                points.Skip(coverage).Take(coverage).ToArray(), state.Active?.Deadband ?? 0);
            best = comparison with { Samples = coverage };
        }

        var enough = best is not null && best.Repeatable && independent >= best.Samples * 2;
        var reasons = new List<string>();
        if (!state.CandidateOpened) reasons.Add("CandidateFormationNotStarted");
        if (points.Count < _options.FormationMinimumSamples) reasons.Add("FormationCoverageInsufficient");
        if (points.Count < _options.FormationMinimumSamples * 2) reasons.Add("ValidationCoverageInsufficient");
        if (best is not null && !best.CenterStable) reasons.Add("CenterNotRepeatable");
        if (best is not null && !best.ScaleStable) reasons.Add("ScaleNotRepeatable");
        if (best is not null && !best.RangeStable) reasons.Add("RangeNotRepeatable");
        if (best is not null && !best.QuantilesStable) reasons.Add("QuantilesNotRepeatable");
        if (best is not null && !best.SlopeStable) reasons.Add("ResidualSlopeNotRepeatable");
        if (critical) reasons.Add("CriticalGap");
        if (lifecycle != RegimeLifecycleState.StableUnresolvedDeviation) reasons.Add("RegimePolicyNotStableUnresolved");
        if (!enough && points.Count >= _options.FormationMinimumSamples * 2) reasons.Add("NoComparableNonOverlappingCoverageYet");
        var ready = enough && !critical && lifecycle == RegimeLifecycleState.StableUnresolvedDeviation;
        var b = best ?? CoverageComparison.Empty;
        return new(ready ? DualBaselineReadiness.Ready : DualBaselineReadiness.NotReady,
            reasons.Distinct().ToArray(), points.Count, independent, duration, cadence, largestGap, critical,
            b.CenterStable, b.ScaleStable, b.SlopeStable, b.Formation.Median, b.Validation.Median,
            b.Formation.RobustScale, b.Validation.RobustScale, b.CombinedSlope,
            state.CandidateOpened ? (b.Samples > 0 ? CandidateCoverageStatus.Covered : CandidateCoverageStatus.Collecting) : CandidateCoverageStatus.NotStarted,
            ready ? CandidateCoverageStatus.Validated : (points.Count >= _options.FormationMinimumSamples * 2 ? CandidateCoverageStatus.Collecting : CandidateCoverageStatus.NotStarted),
            b.Samples, b.Formation.Duration, b.Validation.Duration, b.Formation.Mean, b.Validation.Mean,
            b.Formation.StdDev, b.Validation.StdDev, b.Formation.Range, b.Validation.Range,
            b.MaximumQuantileDelta, b.AlignedMeanAbsoluteDifference, b.StructuralRepetition);
    }

    private CoverageComparison CompareCoverage(
        IReadOnlyList<DualBaselinePoint> formation,
        IReadOnlyList<DualBaselinePoint> validation,
        double deadband)
    {
        var f = Stats(formation); var v = Stats(validation);
        var floor = Math.Max(deadband, Math.Max(f.RobustScale, v.RobustScale));
        floor = Math.Max(floor, 1e-12);
        var center = Math.Abs(f.Mean - v.Mean) <= floor && Math.Abs(f.Median - v.Median) <= floor;
        var minStd = Math.Max(Math.Min(f.StdDev, v.StdDev), Math.Max(deadband, 1e-12));
        var minRobust = Math.Max(Math.Min(f.RobustScale, v.RobustScale), Math.Max(deadband, 1e-12));
        var scale = Math.Max(f.StdDev, v.StdDev) / minStd <= _options.MaximumFormationValidationScaleRatio &&
                    Math.Max(f.RobustScale, v.RobustScale) / minRobust <= _options.MaximumFormationValidationScaleRatio;
        var range = Math.Abs(f.Range - v.Range) <= floor;
        var qDelta = new[] { Math.Abs(f.P05-v.P05), Math.Abs(f.P25-v.P25), Math.Abs(f.P75-v.P75), Math.Abs(f.P95-v.P95) }.Max();
        var quantiles = qDelta <= floor;
        var combined = formation.Concat(validation).ToArray();
        var combinedSlope = Slope(combined);
        var slope = Math.Abs(f.Slope - v.Slope) * Math.Max(f.Duration, v.Duration) <= floor &&
                    Math.Abs(combinedSlope) * (combined[^1].Timestamp - combined[0].Timestamp).TotalMinutes <= floor;
        var aligned = formation.Zip(validation, (a, x) => Math.Abs(a.Value - x.Value)).Average();
        var structural = aligned <= floor;
        return new(0, f, v, center, scale, range, quantiles, slope,
            center && scale && range && quantiles && slope, qDelta, aligned, structural, combinedSlope);
    }

    private static CoverageStats Stats(IReadOnlyList<DualBaselinePoint> points)
    {
        if (points.Count == 0) return CoverageStats.Empty;
        var values = points.Select(x => x.Value).ToArray(); var sorted = values.Order().ToArray();
        var mean = values.Average(); var median = Percentile(sorted, .5);
        var mad = Percentile(values.Select(x => Math.Abs(x-median)).Order().ToArray(), .5);
        var std = Math.Sqrt(values.Sum(x => (x-mean)*(x-mean))/values.Length);
        return new(mean, median, std, mad, mad*1.4826, sorted[^1]-sorted[0],
            Percentile(sorted,.05), Percentile(sorted,.25), Percentile(sorted,.75), Percentile(sorted,.95),
            Slope(points), points.Count > 1 ? (points[^1].Timestamp-points[0].Timestamp).TotalMinutes : 0);
    }
    private static double Percentile(IReadOnlyList<double> s, double p)
    { var x=p*(s.Count-1); var l=(int)Math.Floor(x); var h=(int)Math.Ceiling(x); return l==h?s[l]:s[l]+(x-l)*(s[h]-s[l]); }
    private static double Slope(IReadOnlyList<DualBaselinePoint> p)
    { if(p.Count<2)return 0;var x0=p[0].Timestamp;var xs=p.Select(x=>(x.Timestamp-x0).TotalMinutes).ToArray();var xm=xs.Average();var ym=p.Average(x=>x.Value);var d=xs.Sum(x=>(x-xm)*(x-xm));return d<=0?0:p.Select((x,i)=>(xs[i]-xm)*(x.Value-ym)).Sum()/d; }
    private static HistoricoDetectorPoint P(DualBaselinePoint p) => new() { DataHora=p.Timestamp, Valor=p.Value };
    private static DualDetectorEvaluation NotReadyDetector() => new(DualBaselineReadiness.NotReady,null,null,null,null,null,null);
    private static DualEwmaEvaluation NotReadyEwma() => new(DualBaselineReadiness.NotReady,null,null,null,null,null,null);
    private static DualStatisticalState Aggregate(DualDetectorEvaluation? m, DualDetectorEvaluation? z, DualEwmaEvaluation? e)
    { if(m?.Readiness!=DualBaselineReadiness.Ready||z?.Readiness!=DualBaselineReadiness.Ready||e?.Readiness!=DualBaselineReadiness.Ready)return DualStatisticalState.NotReady;return m.Vote==true||z.Vote==true||e.Vote==true?DualStatisticalState.Abnormal:DualStatisticalState.Normal; }

    private sealed record CoverageStats(double Mean,double Median,double StdDev,double Mad,double RobustScale,double Range,
        double P05,double P25,double P75,double P95,double Slope,double Duration)
    { public static CoverageStats Empty { get; } = new(0,0,0,0,0,0,0,0,0,0,0,0); }
    private sealed record CoverageComparison(int Samples,CoverageStats Formation,CoverageStats Validation,
        bool CenterStable,bool ScaleStable,bool RangeStable,bool QuantilesStable,bool SlopeStable,bool Repeatable,
        double MaximumQuantileDelta,double AlignedMeanAbsoluteDifference,bool StructuralRepetition,double CombinedSlope)
    { public static CoverageComparison Empty { get; } = new(0,CoverageStats.Empty,CoverageStats.Empty,false,false,false,false,false,false,0,0,false,0); }
}
