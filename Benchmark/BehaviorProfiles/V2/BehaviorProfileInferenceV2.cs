using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2;

/// <summary>
/// Inferidor experimental baseado em evidencias nomeadas. Os pesos abaixo
/// expressam a contribuicao de cada propriedade; a decisao conserva Unknown
/// quando a forca ou a separacao entre hipoteses e insuficiente.
/// </summary>
public sealed class BehaviorProfileInferenceV2
{
    public const int MinimumSamples = 24;
    public const double MinimumWinningEvidence = 0.55;
    public const double MinimumWinningMargin = 0.08;

    private readonly BehaviorProfileFeatureExtractor _extractor = new();

    public BehaviorProfileV2Result Infer(
        IReadOnlyList<HistoricoDetectorPoint> history)
    {
        var features = _extractor.Extract(history);
        var sufficient = features.SampleCount >= MinimumSamples;
        var evidence = new[]
        {
            Continuous(features, sufficient),
            Discrete(features, sufficient),
            Counter(features, sufficient),
            Setpoint(features, sufficient)
        };

        var ordered = evidence
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Type)
            .ToArray();
        var winner = ordered[0];
        var runnerUp = ordered[1];
        var margin = winner.Score - runnerUp.Score;

        string reason;
        TagBehaviorType resultType;
        double confidence;

        if (!sufficient)
        {
            resultType = TagBehaviorType.Unknown;
            confidence = 0;
            reason = $"Amostras insuficientes: {features.SampleCount}/{MinimumSamples}.";
        }
        else if (winner.Score < MinimumWinningEvidence)
        {
            resultType = TagBehaviorType.Unknown;
            confidence = winner.Score;
            reason = $"Evidencia maxima insuficiente: {winner.Type}={winner.Score:F3}.";
        }
        else if (margin < MinimumWinningMargin)
        {
            resultType = TagBehaviorType.Unknown;
            confidence = winner.Score;
            reason = $"Hipoteses ambiguas: {winner.Type}={winner.Score:F3}, " +
                     $"{runnerUp.Type}={runnerUp.Score:F3}, margem={margin:F3}.";
        }
        else
        {
            resultType = winner.Type;
            confidence = winner.Score;
            reason = $"{winner.Type} venceu por evidencias comportamentais; " +
                     $"segundo={runnerUp.Type}, margem={margin:F3}.";
        }

        return new BehaviorProfileV2Result
        {
            Type = resultType,
            Confidence = confidence,
            Winner = winner.Type,
            WinnerScore = winner.Score,
            RunnerUp = runnerUp.Type,
            RunnerUpScore = runnerUp.Score,
            Margin = margin,
            HasSufficientSamples = sufficient,
            Reason = reason,
            Features = features,
            EvidenceByType = evidence
        };
    }

    private static BehaviorEvidence Continuous(
        BehaviorProfileFeatures f,
        bool sufficient)
    {
        var b = new EvidenceBuilder(TagBehaviorType.Continuous, sufficient);
        b.Add(f.DistinctValueCount >= 16, 0.18, ">=16 niveis ordenados", "poucos niveis");
        b.Add(f.DistinctValueCount is >= 5 and < 16, 0.10, "5-15 niveis ordenados");
        b.Add(f.IntegerLikeRatio < 0.80, 0.18, "valores nao predominantemente inteiros", "valores predominantemente inteiros");
        b.Add(f.ChangeRatio >= 0.50, 0.15, "mudanca frequente");
        b.Add(f.ChangeRatio is >= 0.15 and < 0.50, 0.08, "mudanca recorrente");
        b.Add(f.AdjacentLevelTransitionRatio >= 0.75, 0.22, "trajetoria entre niveis adjacentes");
        b.Add(f.AdjacentLevelTransitionRatio is >= 0.50 and < 0.75, 0.12, "adjacencia moderada");
        b.Add(f.SmoothTransitionRatio >= 0.80, 0.15, "continuidade local suave");
        b.Add(f.ReversalRatio >= 0.05, 0.08, "reversoes graduais/ciclicas");
        b.Add(f.EstimatedResolution > 0 && f.DistinctValueCount >= 5 && f.StepToRangeRatio <= 0.15, 0.08, "quantizacao fina relativa ao range");
        b.Add(f.DistinctValueCount is >= 3 and <= 4 && f.IntegerLikeRatio < 0.80 && f.AdjacentLevelTransitionRatio >= 0.75 && f.ChangeRatio >= 0.50, 0.08, "poucos niveis quantizados com trajetoria analogica adjacente");
        b.Add(f.DownwardStepCount >= 3 && f.DownwardStepIntervalRegularityRatio >= 0.80 && f.IntegerLikeRatio < 0.20 && f.DistinctValueCount >= 12 && (f.AdjacentLevelTransitionRatio >= 0.65 || f.SmoothTransitionRatio >= 0.95), 0.25, "ciclo robustamente regular com continuidade local nao inteira");
        b.Subtract(f.DistinctValueCount <= 3, 0.15, "cardinalidade extremamente baixa");
        b.Subtract(f.ZeroDeltaRatio >= 0.85, 0.15, "dwell dominante");
        b.Subtract(f.NonDecreasingRatio >= 0.98 && f.ReversalRatio < 0.01, 0.15, "monotonicidade forte sem reversoes");
        b.Subtract(f.PositiveDeltaRatio >= 0.90 && f.ResetCount > 0 && f.ResetRatio <= 0.05, 0.20, "crescimento dominante com reset raro");
        b.Subtract(f.AbruptJumpRatio >= 0.30, 0.15, "saltos abruptos frequentes");
        b.Subtract(f.IntegerLikeRatio >= 0.95 && f.AdjacentLevelTransitionRatio < 0.50, 0.12, "inteiros sem continuidade entre niveis");
        return b.Build();
    }

    private static BehaviorEvidence Discrete(
        BehaviorProfileFeatures f,
        bool sufficient)
    {
        var b = new EvidenceBuilder(TagBehaviorType.Discrete, sufficient);
        b.Add(f.DistinctValueCount is >= 2 and <= 10, 0.28, "2-10 estados absolutos", "cardinalidade nao discreta");
        b.Add(f.DistinctValueCount <= 10 && f.DistinctValueRatio <= 0.20, 0.12, "estados repetidos");
        b.Add(f.ChangeRatio >= 0.10, 0.15, "transicoes observadas");
        b.Add(f.ZeroDeltaRatio >= 0.35, 0.15, "dwell entre transicoes");
        b.Add(f.AdjacentLevelTransitionRatio < 0.50 && f.ChangeRatio >= 0.10, 0.20, "transicoes entre estados separados");
        b.Add(f.MedianDwellSamples >= 2, 0.10, "niveis com permanencia");
        b.Subtract(f.DistinctValueCount > 12, 0.25, "muitos niveis absolutos");
        b.Subtract(f.DistinctValueCount >= 5 && f.ChangeRatio >= 0.70 && f.AdjacentLevelTransitionRatio >= 0.75 && f.SmoothTransitionRatio >= 0.80, 0.20, "trajetoria suave e adjacente");
        return b.Build();
    }

    private static BehaviorEvidence Counter(
        BehaviorProfileFeatures f,
        bool sufficient)
    {
        var b = new EvidenceBuilder(TagBehaviorType.Counter, sufficient);
        b.Add(f.NonDecreasingRatio >= 0.96, 0.30, "monotonicidade dominante", "monotonicidade insuficiente");
        b.Add(f.PositiveDeltaRatio >= 0.15 && f.NegativeDeltaRatio <= 0.05, 0.20, "incrementos positivos dominantes");
        b.Add(f.DistinctValueCount >= 8, 0.15, "cardinalidade crescente suficiente");
        b.Add(f.ChangeRatio >= 0.15, 0.12, "incrementos efetivos");
        b.Add(f.AdjacentLevelTransitionRatio >= 0.70, 0.08, "incrementos ordenados");
        b.Add(f.ResetCount > 0 && f.ResetRatio <= 0.05, 0.10, "reset/rollover ocasional toleravel");
        b.Add(f.ResetCount == 0 && f.NonDecreasingRatio >= 0.98, 0.05, "sem quedas");
        b.Subtract(f.ReversalRatio >= 0.10, 0.25, "reversoes frequentes");
        b.Subtract(f.NegativeDeltaRatio > 0.10, 0.25, "quedas frequentes");
        b.Subtract(f.ChangeRatio < 0.05, 0.15, "estacionario por tempo excessivo");
        b.Subtract(f.DownwardStepCount >= 3 && f.DownwardStepIntervalRegularityRatio >= 0.80 && f.IntegerLikeRatio < 0.20 && f.DistinctValueCount >= 12 && (f.AdjacentLevelTransitionRatio >= 0.65 || f.SmoothTransitionRatio >= 0.95), 0.25, "ciclo analogico nao inteiro mais forte que rollover");
        return b.Build();
    }

    private static BehaviorEvidence Setpoint(
        BehaviorProfileFeatures f,
        bool sufficient)
    {
        var b = new EvidenceBuilder(TagBehaviorType.Setpoint, sufficient);
        b.Add(f.DistinctValueCount is >= 2 and <= 12, 0.25, "poucos patamares configurados", "cardinalidade incompativel");
        b.Add(f.ZeroDeltaRatio >= 0.75, 0.28, "longos patamares estacionarios");
        b.Add(f.MedianDwellSamples >= 5, 0.22, "dwell mediano longo");
        b.Add(f.ChangeRatio is > 0 and <= 0.20, 0.12, "mudancas raras entre patamares");
        b.Add(f.StepToRangeRatio >= 0.15, 0.13, "degraus materiais em relacao ao range");
        b.Subtract(f.ChangeRatio >= 0.40, 0.25, "mudanca frequente");
        b.Subtract(f.DistinctValueCount > 12, 0.20, "muitos patamares");
        return b.Build();
    }

    private sealed class EvidenceBuilder
    {
        private readonly TagBehaviorType _type;
        private readonly bool _sufficient;
        private readonly List<string> _favorable = [];
        private readonly List<string> _contrary = [];
        private double _score;

        public EvidenceBuilder(TagBehaviorType type, bool sufficient)
        {
            _type = type;
            _sufficient = sufficient;
        }

        public void Add(bool condition, double weight, string favorable, string? contrary = null)
        {
            if (condition)
            {
                _score += weight;
                _favorable.Add($"+{weight:F2} {favorable}");
            }
            else if (contrary is not null)
                _contrary.Add(contrary);
        }

        public void Subtract(bool condition, double weight, string contrary)
        {
            if (!condition) return;
            _score -= weight;
            _contrary.Add($"-{weight:F2} {contrary}");
        }

        public BehaviorEvidence Build()
        {
            var score = Math.Clamp(_score, 0, 1);
            return new BehaviorEvidence
            {
                Type = _type,
                Score = score,
                HasSufficientSamples = _sufficient,
                FavorableFeatures = _favorable,
                ContraryFeatures = _contrary,
                Reason = _favorable.Count == 0
                    ? "Nenhuma evidencia favoravel material."
                    : string.Join("; ", _favorable)
            };
        }
    }
}
