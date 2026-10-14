using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

public static class DualBaselineMath
{
    public static DualBaselineReference BuildReference(
        string id, int version, IReadOnlyList<DualBaselinePoint> source,
        DetectorInput template, DualBaselineShadowOptions options)
    {
        if (source.Count < 2) throw new ArgumentException("Baseline requer ao menos dois pontos.", nameof(source));
        var history = source.Select(x => new HistoricoDetectorPoint { DataHora = x.Timestamp, Valor = x.Value }).ToArray();
        var input = Clone(template, history);
        var context = input.Contexto;
        var mean = history.Average(x => x.Valor);
        var std = context.DesvioPadrao;
        var median = context.Mediana;
        var mad = context.Mad;
        var robust = double.IsFinite(mad) && mad > 0 ? mad * 1.4826 : 0;
        var protection = BaselineProtectionProfile.Build(input, median);
        var madScale = protection.ResolveEffectiveScale(robust, options.MadThreshold);
        var zProtection = BaselineProtectionProfile.Build(input, mean);
        var zScale = zProtection.ResolveEffectiveScale(std, options.ZScoreThreshold);
        return new(id, version, history[0].DataHora, history[^1].DataHora,
            source[0].MinuteIndex >= 0 ? source[0].MinuteIndex : null,
            source[^1].MinuteIndex >= 0 ? source[^1].MinuteIndex : null,
            history.Length, mean, std, median, mad, robust, protection.Deadband,
            protection.DeadbandSource, context.IntervaloMedianoMinutos,
            madScale, ScaleOrigin(robust, protection, options.MadThreshold),
            zScale, ScaleOrigin(std, zProtection, options.ZScoreThreshold), history);
    }

    public static DualDetectorEvaluation EvaluateMad(double value, DualBaselineReference reference, double threshold)
    {
        var delta = Math.Abs(value - reference.Median);
        var inside = reference.Deadband > 0 && delta <= reference.Deadband;
        var score = inside ? 0 : delta / reference.MadEffectiveScale;
        return new(DualBaselineReadiness.Ready, score, !inside && score >= threshold,
            reference.Median, reference.RobustScale, reference.MadEffectiveScale,
            reference.MadEffectiveScaleOrigin);
    }

    public static DualDetectorEvaluation EvaluateZ(double value, DualBaselineReference reference, double threshold)
    {
        var delta = Math.Abs(value - reference.Mean);
        var inside = reference.Deadband > 0 && delta <= reference.Deadband;
        var score = inside ? 0 : delta / reference.ZEffectiveScale;
        return new(DualBaselineReadiness.Ready, score, !inside && score >= threshold,
            reference.Mean, reference.StdDev, reference.ZEffectiveScale,
            reference.ZEffectiveScaleOrigin);
    }

    public static DualEwmaState InitializeEwma(DualBaselineReference reference, DualBaselineShadowOptions options)
    {
        var state = new DualEwmaState
        {
            BaselineId = reference.Id, Target = reference.Mean,
            EffectiveBaseScale = reference.ZEffectiveScale,
            CadenceMinutes = reference.CadenceMinutes,
            Value = reference.Mean, VarianceFactor = 0
        };
        foreach (var point in reference.Points)
            Advance(state, point.DataHora, point.Valor, options.EwmaLambda);
        return state;
    }

    public static DualEwmaEvaluation EvaluateAndAdvance(
        DualEwmaState state, DateTime timestamp, double value,
        double deadband, DualBaselineShadowOptions options)
    {
        var previous = state.Value;
        var alpha = EffectiveAlpha(options.EwmaLambda,
            state.PreviousTimestamp.HasValue ? (timestamp - state.PreviousTimestamp.Value).TotalMinutes : state.CadenceMinutes,
            state.CadenceMinutes);
        Advance(state, timestamp, value, options.EwmaLambda);
        var ewmaStd = state.EffectiveBaseScale * Math.Sqrt(Math.Max(state.VarianceFactor, 0));
        var delta = Math.Abs(state.Value - state.Target);
        var inside = deadband > 0 && delta <= deadband;
        var score = inside ? 0 : delta / ewmaStd;
        return new(DualBaselineReadiness.Ready, state.Target, previous, state.Value,
            alpha, score, !inside && score >= options.EwmaSigmaLimit);
    }

    public static DetectorInput Clone(DetectorInput template, IReadOnlyList<HistoricoDetectorPoint> history) => new()
    {
        ClienteId = template.ClienteId, TagName = template.TagName, TipoTag = template.TipoTag,
        ValorAtual = template.ValorAtual, DataHoraAtual = template.DataHoraAtual,
        MediaHistorica = history.Count == 0 ? 0 : history.Average(x => x.Valor),
        DesvioPadraoHistorico = new DetectorContext(history).DesvioPadrao,
        VariacaoMinimaRelevante = template.VariacaoMinimaRelevante,
        ConfiancaVariacaoMinima = template.ConfiancaVariacaoMinima,
        VariacaoMinimaDefinidaPeloCliente = template.VariacaoMinimaDefinidaPeloCliente,
        Historico = history
    };

    private static void Advance(DualEwmaState state, DateTime timestamp, double value, double lambda)
    {
        var interval = state.PreviousTimestamp.HasValue
            ? (timestamp - state.PreviousTimestamp.Value).TotalMinutes : state.CadenceMinutes;
        var alpha = EffectiveAlpha(lambda, interval, state.CadenceMinutes);
        state.Value = alpha * value + (1 - alpha) * state.Value;
        state.VarianceFactor = alpha * alpha + (1 - alpha) * (1 - alpha) * state.VarianceFactor;
        state.PreviousTimestamp = timestamp;
    }

    private static double EffectiveAlpha(double alphaBase, double interval, double cadence)
    {
        alphaBase = Math.Clamp(alphaBase, 0.01, 1.0);
        if (alphaBase >= 1) return 1;
        if (interval <= 0 || !double.IsFinite(interval) || cadence <= 0 || !double.IsFinite(cadence)) return alphaBase;
        var relative = Math.Clamp(interval / cadence, .05, 50);
        return Math.Clamp(1 - Math.Pow(1 - alphaBase, relative), .0001, 1);
    }

    private static string ScaleOrigin(double raw, BaselineProtectionProfile protection, double threshold)
    {
        var deadbandFloor = protection.Deadband > 0 ? protection.Deadband / threshold : 0;
        var finiteRaw = double.IsFinite(raw) && raw > 0 ? raw : 0;
        var max = Math.Max(finiteRaw, Math.Max(deadbandFloor, protection.RelativeScaleFloor));
        if (max == finiteRaw && finiteRaw > 0) return "ObservedScale";
        if (max == deadbandFloor && deadbandFloor > 0) return "ExistingDeadbandFloor:" + protection.DeadbandSource;
        return "ExistingRelativeScaleFloor";
    }
}
