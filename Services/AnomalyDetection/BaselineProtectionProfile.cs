namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Proteção comum contra scores artificiais causados por baixa variância.
///
/// A proteção combina:
/// - deadband explícito recebido no DetectorInput;
/// - deadband automático estimado pelo P95 das variações consecutivas;
/// - piso relativo de escala quando o histórico é praticamente constante.
///
/// O objetivo não é substituir o threshold estatístico.
/// O objetivo é impedir que uma diferença fisicamente mínima seja amplificada
/// apenas porque sigma/MAD estão próximos de zero.
/// </summary>
public sealed class BaselineProtectionProfile
{
    public double Deadband { get; init; }

    public string DeadbandSource { get; init; } = "none";

    public double DeadbandConfidence { get; init; }

    public double RelativeScaleFloor { get; init; }

    public double AutomaticDeadbandP95 { get; init; }

    public int VariationCount { get; init; }

    public static BaselineProtectionProfile Build(
        DetectorInput input,
        double reference)
    {
        ArgumentNullException.ThrowIfNull(input);

        var automatic =
            CalculateAutomaticDeadband(
                input.Historico);

        var explicitDeadband =
            double.IsFinite(
                input.VariacaoMinimaRelevante) &&
            input.VariacaoMinimaRelevante > 0
                ? input.VariacaoMinimaRelevante
                : 0;

        var deadband =
            explicitDeadband > 0
                ? explicitDeadband
                : automatic.P95;

        var source =
            explicitDeadband > 0
                ? input.VariacaoMinimaDefinidaPeloCliente
                    ? "client"
                    : "profile"
                : automatic.P95 > 0
                    ? "history-p95"
                    : "relative-floor";

        var confidence =
            explicitDeadband > 0
                ? NormalizeConfidence(
                    input.ConfiancaVariacaoMinima)
                : CalculateAutomaticConfidence(
                    automatic.VariationCount,
                    automatic.ZeroRate,
                    automatic.P95);

        /*
         * Último mecanismo de segurança quando não existe qualquer
         * deadband aprendido e a série é praticamente constante.
         *
         * 0,1% da referência é deliberadamente conservador e corresponde
         * ao critério já usado pelos relatórios de auditoria para identificar
         * baseline de variância muito baixa.
         */
        var relativeScaleFloor =
            Math.Max(
                Math.Abs(reference) * 0.001,
                1e-6);

        return new BaselineProtectionProfile
        {
            Deadband =
                Math.Max(
                    0,
                    deadband),

            DeadbandSource =
                source,

            DeadbandConfidence =
                confidence,

            RelativeScaleFloor =
                relativeScaleFloor,

            AutomaticDeadbandP95 =
                automatic.P95,

            VariationCount =
                automatic.VariationCount
        };
    }

    /// <summary>
    /// Retorna uma escala mínima coerente com o deadband e o threshold.
    ///
    /// Se o deadband é D e o threshold é K, usamos pelo menos D/K como
    /// escala. Assim uma mudança menor que D não pode virar um score enorme
    /// apenas por sigma/MAD quase zero.
    /// </summary>
    public double ResolveEffectiveScale(
        double rawScale,
        double threshold)
    {
        var normalizedThreshold =
            double.IsFinite(threshold) &&
            threshold > 0
                ? threshold
                : 1;

        var deadbandScaleFloor =
            Deadband > 0
                ? Deadband /
                  normalizedThreshold
                : 0;

        var effective =
            Math.Max(
                double.IsFinite(rawScale) &&
                rawScale > 0
                    ? rawScale
                    : 0,
                Math.Max(
                    deadbandScaleFloor,
                    RelativeScaleFloor));

        return effective;
    }

    public bool IsInsideDeadband(
        double currentValue,
        double reference)
    {
        if (Deadband <= 0)
        {
            return false;
        }

        var delta =
            Math.Abs(
                currentValue -
                reference);

        return double.IsFinite(delta) &&
               delta <= Deadband;
    }

    private static AutomaticDeadbandResult
        CalculateAutomaticDeadband(
            IReadOnlyList<HistoricoDetectorPoint> history)
    {
        if (history.Count < 2)
        {
            return new AutomaticDeadbandResult();
        }

        var variations =
            new List<double>(
                history.Count - 1);

        var zeroCount = 0;

        for (var index = 1;
             index < history.Count;
             index++)
        {
            var variation =
                Math.Abs(
                    history[index].Valor -
                    history[index - 1].Valor);

            if (!double.IsFinite(variation))
            {
                continue;
            }

            if (variation <= 1e-12)
            {
                zeroCount++;
            }

            variations.Add(
                variation);
        }

        if (variations.Count == 0)
        {
            return new AutomaticDeadbandResult();
        }

        variations.Sort();

        var p95 =
            PercentileSorted(
                variations,
                0.95);

        var zeroRate =
            zeroCount /
            (double)variations.Count;

        return new AutomaticDeadbandResult
        {
            P95 =
                p95,

            VariationCount =
                variations.Count,

            ZeroRate =
                zeroRate
        };
    }

    private static double PercentileSorted(
        IReadOnlyList<double> sorted,
        double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        if (sorted.Count == 1)
        {
            return sorted[0];
        }

        var position =
            Math.Clamp(
                percentile,
                0,
                1) *
            (sorted.Count - 1);

        var lower =
            (int)Math.Floor(
                position);

        var upper =
            (int)Math.Ceiling(
                position);

        if (lower == upper)
        {
            return sorted[lower];
        }

        var fraction =
            position - lower;

        return sorted[lower] +
               ((sorted[upper] -
                 sorted[lower]) *
                fraction);
    }

    private static double CalculateAutomaticConfidence(
        int variationCount,
        double zeroRate,
        double deadband)
    {
        if (variationCount < 9 ||
            deadband <= 0)
        {
            return 0;
        }

        var confidence =
            variationCount switch
            {
                >= 10_000 => 0.98,
                >= 5_000 => 0.96,
                >= 1_000 => 0.94,
                >= 500 => 0.90,
                >= 100 => 0.85,
                >= 30 => 0.75,
                _ => 0.65
            };

        if (zeroRate > 0.95)
        {
            confidence -= 0.25;
        }
        else if (zeroRate > 0.80)
        {
            confidence -= 0.15;
        }
        else if (zeroRate > 0.60)
        {
            confidence -= 0.08;
        }

        return Math.Clamp(
            confidence,
            0,
            1);
    }

    private static double NormalizeConfidence(
        double value)
    {
        if (!double.IsFinite(value) ||
            value <= 0)
        {
            return 0;
        }

        /*
         * O perfil atual persiste confiança em porcentagem (0..100).
         * Também aceitamos 0..1 para facilitar a migração futura.
         */
        return value > 1
            ? Math.Clamp(
                value / 100.0,
                0,
                1)
            : Math.Clamp(
                value,
                0,
                1);
    }

    private sealed class AutomaticDeadbandResult
    {
        public double P95 { get; init; }

        public int VariationCount { get; init; }

        public double ZeroRate { get; init; }
    }
}
