using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime;

namespace IAApi.Benchmark.RegimeShadow;

/// <summary>
/// B3.22.4 — produz evidência de sensor congelado para a camada
/// de qualidade de dados/regime.
///
/// Não participa do score ou voto do Ensemble Legacy.
///
/// V1 deliberadamente conservadora:
/// - somente sinais contínuos;
/// - somente congelamento praticamente exato;
/// - exige quantidade e duração mínimas;
/// - exige histórico anterior suficientemente variável;
/// - não tenta detectar micro-jitter.
/// </summary>
public sealed class FrozenSensorEvidenceCalculator
{
    private const double EqualityTolerance = 1e-12;

    public FrozenSensorEvidenceResult Calculate(
        DetectorInput input,
        RegimeSignalKind signalKind,
        RegimeShadowOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        if (signalKind != RegimeSignalKind.Continuous)
        {
            return FrozenSensorEvidenceResult.NotApplicable(
                "signal-not-continuous");
        }

        if (input.QualidadeHistorico.HasCriticalGap)
        {
            return FrozenSensorEvidenceResult.NotApplicable(
                "critical-gap");
        }

        if (!double.IsFinite(input.ValorAtual) ||
            input.DataHoraAtual == default)
        {
            return FrozenSensorEvidenceResult.NotApplicable(
                "invalid-current-reading");
        }

        var points = BuildSeries(input);

        if (points.Count < 2)
        {
            return FrozenSensorEvidenceResult.NotApplicable(
                "insufficient-points");
        }

        var tailStart = points.Count - 1;

        while (tailStart > 0)
        {
            var current = points[tailStart];
            var previous = points[tailStart - 1];

            var variation =
                Math.Abs(
                    current.Valor -
                    previous.Valor);

            if (!double.IsFinite(variation) ||
                variation > EqualityTolerance)
            {
                break;
            }

            tailStart--;
        }

        var consecutivePoints =
            points.Count -
            tailStart;

        var frozenStart =
            points[tailStart];

        var frozenEnd =
            points[^1];

        var durationMinutes =
            (frozenEnd.DataHora -
             frozenStart.DataHora)
            .TotalMinutes;

        var minimumPoints =
            Math.Max(
                3,
                options.FrozenMinimumPoints);

        if (consecutivePoints < minimumPoints)
        {
            return new FrozenSensorEvidenceResult(
                HasEvidence: false,
                Applicable: true,
                Reason: "insufficient-frozen-points",
                ConsecutivePoints: consecutivePoints,
                DurationMinutes: durationMinutes,
                FrozenValue: frozenEnd.Valor,
                PriorVariationCount: 0,
                PriorZeroRate: 0);
        }

        if (!double.IsFinite(durationMinutes) ||
            durationMinutes <
            options.FrozenMinimumMinutes)
        {
            return new FrozenSensorEvidenceResult(
                HasEvidence: false,
                Applicable: true,
                Reason: "insufficient-frozen-duration",
                ConsecutivePoints: consecutivePoints,
                DurationMinutes: durationMinutes,
                FrozenValue: frozenEnd.Valor,
                PriorVariationCount: 0,
                PriorZeroRate: 0);
        }

        var priorWindowPoints =
            Math.Max(
                minimumPoints,
                options.FrozenPriorWindowPoints);

        var prior =
            points
                .Take(tailStart)
                .TakeLast(priorWindowPoints)
                .ToArray();

        /*
         * Precisamos de histórico anterior suficiente para distinguir
         * um sensor que congelou de uma tag naturalmente quase constante.
         */
        if (prior.Length < minimumPoints)
        {
            return new FrozenSensorEvidenceResult(
                HasEvidence: false,
                Applicable: false,
                Reason: "insufficient-prior-history",
                ConsecutivePoints: consecutivePoints,
                DurationMinutes: durationMinutes,
                FrozenValue: frozenEnd.Valor,
                PriorVariationCount: Math.Max(0, prior.Length - 1),
                PriorZeroRate: 0);
        }

        var priorVariationCount = 0;
        var priorZeroCount = 0;

        for (var index = 1;
             index < prior.Length;
             index++)
        {
            var variation =
                Math.Abs(
                    prior[index].Valor -
                    prior[index - 1].Valor);

            if (!double.IsFinite(variation))
            {
                continue;
            }

            priorVariationCount++;

            if (variation <= EqualityTolerance)
            {
                priorZeroCount++;
            }
        }

        if (priorVariationCount == 0)
        {
            return new FrozenSensorEvidenceResult(
                HasEvidence: false,
                Applicable: false,
                Reason: "insufficient-prior-variation-data",
                ConsecutivePoints: consecutivePoints,
                DurationMinutes: durationMinutes,
                FrozenValue: frozenEnd.Valor,
                PriorVariationCount: 0,
                PriorZeroRate: 0);
        }

        var priorZeroRate =
            priorZeroCount /
            (double)priorVariationCount;

        var maximumPriorZeroRate =
            Math.Clamp(
                options.FrozenMaximumPriorZeroRate,
                0,
                1);

        /*
         * Se a tag já era predominantemente constante antes da cauda
         * atual, não classificamos conservadoramente como congelamento.
         */
        if (priorZeroRate >
            maximumPriorZeroRate)
        {
            return new FrozenSensorEvidenceResult(
                HasEvidence: false,
                Applicable: true,
                Reason: "historically-low-variation",
                ConsecutivePoints: consecutivePoints,
                DurationMinutes: durationMinutes,
                FrozenValue: frozenEnd.Valor,
                PriorVariationCount: priorVariationCount,
                PriorZeroRate: priorZeroRate);
        }

        return new FrozenSensorEvidenceResult(
            HasEvidence: true,
            Applicable: true,
            Reason: "frozen-sensor-evidence",
            ConsecutivePoints: consecutivePoints,
            DurationMinutes: durationMinutes,
            FrozenValue: frozenEnd.Valor,
            PriorVariationCount: priorVariationCount,
            PriorZeroRate: priorZeroRate);
    }

    private static List<HistoricoDetectorPoint>
        BuildSeries(
            DetectorInput input)
    {
        var result =
            new List<HistoricoDetectorPoint>(
                input.Historico.Count + 1);

        foreach (var point in input.Historico)
        {
            if (!double.IsFinite(point.Valor) ||
                point.DataHora == default)
            {
                continue;
            }

            result.Add(
                new HistoricoDetectorPoint
                {
                    DataHora = point.DataHora,
                    Valor = point.Valor
                });
        }

        if (result.Count > 0)
        {
            var last =
                result[^1];

            if (input.DataHoraAtual <
                last.DataHora)
            {
                return [];
            }

            if (input.DataHoraAtual ==
                last.DataHora)
            {
                result[^1] =
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            input.DataHoraAtual,

                        Valor =
                            input.ValorAtual
                    };

                return result;
            }
        }

        result.Add(
            new HistoricoDetectorPoint
            {
                DataHora =
                    input.DataHoraAtual,

                Valor =
                    input.ValorAtual
            });

        return result;
    }
}

public sealed record FrozenSensorEvidenceResult(
    bool HasEvidence,
    bool Applicable,
    string Reason,
    int ConsecutivePoints,
    double DurationMinutes,
    double FrozenValue,
    int PriorVariationCount,
    double PriorZeroRate)
{
    public static FrozenSensorEvidenceResult NotApplicable(
        string reason) =>
        new(
            HasEvidence: false,
            Applicable: false,
            Reason: reason,
            ConsecutivePoints: 0,
            DurationMinutes: 0,
            FrozenValue: 0,
            PriorVariationCount: 0,
            PriorZeroRate: 0);
}