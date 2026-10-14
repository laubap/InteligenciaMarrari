namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Infere o comportamento da tag usando três fontes:
///
/// 1. TipoTag/metadado explícito;
/// 2. nome da tag como evidência complementar;
/// 3. comportamento estatístico do histórico.
///
/// Regras explícitas fortes têm precedência. Inferência automática só recebe
/// confiança alta quando o histórico fornece evidência suficientemente clara.
/// </summary>
public sealed class TagBehaviorProfileResolver
{
    private const int MinimumSamplesForAutomaticInference = 12;
    private const double IntegerTolerance = 1e-9;

    public TagBehaviorProfile Resolve(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var history =
            input.Historico;

        var explicitType =
            ResolveExplicitType(
                input.TipoTag);

        if (explicitType != TagBehaviorType.Unknown)
        {
            return BuildProfile(
                explicitType,
                0.99,
                $"TipoTag explícito reconhecido: '{input.TipoTag}'.",
                history);
        }

        /*
         * O nome nunca decide sozinho que uma tag é Continuous.
         * Ele só é usado para semânticas industriais relativamente fortes.
         */
        var semanticType =
            ResolveSemanticType(
                input.TagName);

        var statistical =
            AnalyzeHistory(
                history);

        if (semanticType == TagBehaviorType.Setpoint)
        {
            return BuildProfile(
                TagBehaviorType.Setpoint,
                statistical.SampleCount >= 3
                    ? 0.93
                    : 0.88,
                "Nome/metadado da tag indica setpoint.",
                statistical);
        }

        if (semanticType == TagBehaviorType.Counter &&
            statistical.CounterConfidence >= 0.70)
        {
            return BuildProfile(
                TagBehaviorType.Counter,
                Math.Max(
                    0.90,
                    statistical.CounterConfidence),
                "Semântica de contador confirmada pelo comportamento monotônico.",
                statistical);
        }

        if (semanticType == TagBehaviorType.Discrete &&
            statistical.DiscreteConfidence >= 0.65)
        {
            return BuildProfile(
                TagBehaviorType.Discrete,
                Math.Max(
                    0.90,
                    statistical.DiscreteConfidence),
                "Semântica de estado confirmada pelo histórico discreto.",
                statistical);
        }

        if (statistical.SampleCount <
            MinimumSamplesForAutomaticInference)
        {
            return TagBehaviorProfile.Unknown(
                "Histórico insuficiente para inferência comportamental segura.",
                statistical.SampleCount);
        }

        /*
         * Discrete:
         * poucos estados, alta repetição e valores predominantemente inteiros.
         *
         * Não usamos somente DistinctValueCount para evitar classificar
         * sensores contínuos quantizados como estados discretos.
         */
        if (statistical.DiscreteConfidence >= 0.88)
        {
            return BuildProfile(
                TagBehaviorType.Discrete,
                statistical.DiscreteConfidence,
                "Histórico apresenta poucos estados repetidos e " +
                "predominantemente inteiros.",
                statistical);
        }

        /*
         * Counter:
         * valores predominantemente inteiros, alta monotonicidade,
         * alterações efetivas e cardinalidade suficiente.
         *
         * Reset ocasional é tolerado pela razão de passos não decrescentes.
         */
        if (statistical.CounterConfidence >= 0.88)
        {
            return BuildProfile(
                TagBehaviorType.Counter,
                statistical.CounterConfidence,
                "Histórico apresenta comportamento monotônico compatível " +
                "com contador.",
                statistical);
        }

        /*
         * Continuous só recebe confiança alta quando há diversidade real.
         * Caso contrário permanecemos Unknown para não bloquear detectores
         * por uma inferência fraca.
         */
        if (statistical.ContinuousConfidence >= 0.85)
        {
            return BuildProfile(
                TagBehaviorType.Continuous,
                statistical.ContinuousConfidence,
                "Histórico apresenta cardinalidade e variação compatíveis " +
                "com sinal contínuo.",
                statistical);
        }

        return BuildProfile(
            TagBehaviorType.Unknown,
            Math.Max(
                statistical.DiscreteConfidence,
                Math.Max(
                    statistical.CounterConfidence,
                    statistical.ContinuousConfidence)),
            "O comportamento não foi conclusivo; o roteamento legado será mantido.",
            statistical);
    }

    private static TagBehaviorType ResolveExplicitType(
        string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return TagBehaviorType.Unknown;
        }

        var normalized =
            Normalize(rawType);

        if (ContainsAny(
                normalized,
                "continuous",
                "continuo",
                "analog",
                "analogico",
                "float",
                "double",
                "real"))
        {
            return TagBehaviorType.Continuous;
        }

        if (ContainsAny(
                normalized,
                "discrete",
                "discreto",
                "boolean",
                "bool",
                "digital",
                "binary",
                "binario",
                "state",
                "estado"))
        {
            return TagBehaviorType.Discrete;
        }

        if (ContainsAny(
                normalized,
                "counter",
                "contador",
                "totalizer",
                "totalizador",
                "accumulator",
                "acumulador"))
        {
            return TagBehaviorType.Counter;
        }

        if (ContainsAny(
                normalized,
                "setpoint",
                "set point",
                "sp"))
        {
            return TagBehaviorType.Setpoint;
        }

        return TagBehaviorType.Unknown;
    }

    private static TagBehaviorType ResolveSemanticType(
        string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return TagBehaviorType.Unknown;
        }

        var normalized =
            Normalize(tagName);

        if (ContainsAnyToken(
                normalized,
                "setpoint",
                "set_point",
                "set-point",
                ".sp",
                "_sp"))
        {
            return TagBehaviorType.Setpoint;
        }

        if (ContainsAnyToken(
                normalized,
                "counter",
                "contador",
                "totalizer",
                "totalizador",
                "accumulator",
                "acumulador"))
        {
            return TagBehaviorType.Counter;
        }

        if (ContainsAnyToken(
                normalized,
                "state",
                "status",
                "estado",
                "online",
                "running",
                "run_status",
                "enabled",
                "alarm_state"))
        {
            return TagBehaviorType.Discrete;
        }

        return TagBehaviorType.Unknown;
    }

    private static StatisticalProfile AnalyzeHistory(
        IReadOnlyList<HistoricoDetectorPoint> history)
    {
        if (history.Count == 0)
        {
            return new StatisticalProfile();
        }

        var values =
            history
                .Select(point => point.Valor)
                .Where(double.IsFinite)
                .ToArray();

        if (values.Length == 0)
        {
            return new StatisticalProfile();
        }

        var distinct =
            values
                .Distinct()
                .Count();

        var distinctRatio =
            distinct /
            (double)values.Length;

        var integerLike =
            values.Count(
                value =>
                    Math.Abs(
                        value -
                        Math.Round(value)) <=
                    IntegerTolerance);

        var integerLikeRatio =
            integerLike /
            (double)values.Length;

        var transitions =
            Math.Max(
                0,
                values.Length - 1);

        var nonDecreasing = 0;
        var changes = 0;

        for (var index = 1;
             index < values.Length;
             index++)
        {
            var previous =
                values[index - 1];

            var current =
                values[index];

            if (current >= previous)
            {
                nonDecreasing++;
            }

            if (Math.Abs(
                    current - previous) >
                IntegerTolerance)
            {
                changes++;
            }
        }

        var nonDecreasingRatio =
            transitions > 0
                ? nonDecreasing /
                  (double)transitions
                : 0;

        var changeRatio =
            transitions > 0
                ? changes /
                  (double)transitions
                : 0;

        var lowCardinality =
            distinct <= 12 &&
            distinctRatio <= 0.20;

        var veryLowCardinality =
            distinct <= 4 &&
            distinctRatio <= 0.10;

        var discreteConfidence =
            0.0;

        if (veryLowCardinality &&
            integerLikeRatio >= 0.95)
        {
            discreteConfidence =
                0.97;
        }
        else if (lowCardinality &&
                 integerLikeRatio >= 0.90)
        {
            discreteConfidence =
                0.90;
        }
        else if (lowCardinality)
        {
            discreteConfidence =
                0.72;
        }

        var counterConfidence =
            0.0;

        if (integerLikeRatio >= 0.90 &&
            nonDecreasingRatio >= 0.97 &&
            changeRatio >= 0.20 &&
            distinct >= 8)
        {
            counterConfidence =
                0.95;
        }
        else if (integerLikeRatio >= 0.80 &&
                 nonDecreasingRatio >= 0.92 &&
                 changeRatio >= 0.15 &&
                 distinct >= 8)
        {
            counterConfidence =
                0.88;
        }

        var continuousConfidence =
            0.0;

        if (distinct >= 15 &&
            distinctRatio >= 0.35 &&
            integerLikeRatio < 0.95)
        {
            continuousConfidence =
                0.94;
        }
        else if (distinct >= 10 &&
                 distinctRatio >= 0.25)
        {
            continuousConfidence =
                0.86;
        }

        return new StatisticalProfile
        {
            SampleCount =
                values.Length,

            DistinctValueCount =
                distinct,

            DistinctValueRatio =
                distinctRatio,

            IntegerLikeRatio =
                integerLikeRatio,

            NonDecreasingRatio =
                nonDecreasingRatio,

            ChangeRatio =
                changeRatio,

            DiscreteConfidence =
                discreteConfidence,

            CounterConfidence =
                counterConfidence,

            ContinuousConfidence =
                continuousConfidence
        };
    }

    private static TagBehaviorProfile BuildProfile(
        TagBehaviorType type,
        double confidence,
        string reason,
        IReadOnlyList<HistoricoDetectorPoint> history)
    {
        return BuildProfile(
            type,
            confidence,
            reason,
            AnalyzeHistory(history));
    }

    private static TagBehaviorProfile BuildProfile(
        TagBehaviorType type,
        double confidence,
        string reason,
        StatisticalProfile statistical)
    {
        return new TagBehaviorProfile
        {
            Type =
                type,

            Confidence =
                Math.Clamp(
                    confidence,
                    0,
                    1),

            Reason =
                reason,

            SampleCount =
                statistical.SampleCount,

            DistinctValueCount =
                statistical.DistinctValueCount,

            DistinctValueRatio =
                statistical.DistinctValueRatio,

            IntegerLikeRatio =
                statistical.IntegerLikeRatio,

            NonDecreasingRatio =
                statistical.NonDecreasingRatio,

            ChangeRatio =
                statistical.ChangeRatio
        };
    }

    private static string Normalize(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant();
    }

    private static bool ContainsAny(
        string value,
        params string[] candidates)
    {
        return candidates.Any(
            candidate =>
                value.Equals(
                    candidate,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAnyToken(
        string value,
        params string[] candidates)
    {
        return candidates.Any(
            candidate =>
                value.Contains(
                    candidate,
                    StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StatisticalProfile
    {
        public int SampleCount { get; init; }

        public int DistinctValueCount { get; init; }

        public double DistinctValueRatio { get; init; }

        public double IntegerLikeRatio { get; init; }

        public double NonDecreasingRatio { get; init; }

        public double ChangeRatio { get; init; }

        public double DiscreteConfidence { get; init; }

        public double CounterConfidence { get; init; }

        public double ContinuousConfidence { get; init; }
    }
}
