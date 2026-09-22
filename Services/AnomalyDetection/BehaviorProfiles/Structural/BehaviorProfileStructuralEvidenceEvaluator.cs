using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural;

/// <summary>
/// Avaliacao Shadow pura entre score e maturidade. Quatro sub-horizontes
/// distinguem atividade distribuida de uma rampa concentrada na borda.
/// </summary>
public sealed class BehaviorProfileStructuralEvidenceEvaluator
{
    private const int MinimumSamples =
        BehaviorProfileInferenceV2.MinimumSamples;

    private const int SegmentCount = 4;

    public BehaviorProfileStructuralAssessment Evaluate(
        BehaviorProfileV2Result raw,
        IReadOnlyList<HistoricoDetectorPoint> history,
        bool wouldMatureUnderCurrentPolicy)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(history);

        var points = history
            .Where(p => double.IsFinite(p.Valor))
            .OrderBy(p => p.DataHora)
            .ToArray();

        var coverage =
            Coverage(
                points,
                raw.Features);

        var classes = new[]
        {
            Continuous(raw, coverage),
            Discrete(raw, points, coverage),
            Counter(raw, points, coverage),
            Setpoint(raw, points, coverage)
        };

        var winner =
            classes.Single(
                a => a.Class == raw.Winner);

        return new BehaviorProfileStructuralAssessment
        {
            RawWinner = raw.Winner,

            RawWinnerStatus =
                winner.StructuralStatus,

            WouldMatureUnderCurrentPolicy =
                wouldMatureUnderCurrentPolicy,

            WouldBeBlockedByStructuralGate =
                wouldMatureUnderCurrentPolicy &&
                winner.StructuralStatus !=
                StructuralEvidenceStatus.Valid,

            Classes = classes
        };
    }

    private static ClassEvidenceAssessment Counter(
        BehaviorProfileV2Result raw,
        IReadOnlyList<HistoricoDetectorPoint> p,
        ObservationCoverage c)
    {
        var f = raw.Features;

        var req = new List<string>();
        var sup = new List<string>();
        var contra = new List<string>();

        if (p.Count < MinimumSamples)
        {
            return Result(
                TagBehaviorType.Counter,
                StructuralEvidenceStatus.Insufficient,
                raw,
                c,
                req,
                sup,
                contra,
                "Cobertura insuficiente.");
        }

        var transitions =
            Math.Max(
                1,
                p.Count - 1);

        var increments =
            (int)Math.Round(
                f.PositiveDeltaRatio *
                transitions);

        var distributed =
            c.SegmentsWithPositiveProgression >= 3;

        var materialEvents =
            increments >=
            Math.Max(
                8,
                p.Count / 20);

        var net =
            p[^1].Valor -
            p[0].Valor;

        var materialNet =
            net >
            Math.Max(
                f.TypicalAbsoluteStep * 4,
                f.ValueRange * .25);

        var noDominantPlateau =
            f.MaximumDwellSamples <=
            p.Count * .75;

        var rareResets =
            f.ResetRatio <= .05;

        Add(
            req,
            distributed,
            "progressao positiva em >=3/4 sub-horizontes",
            contra);

        Add(
            req,
            materialEvents,
            "eventos incrementais materialmente distribuidos",
            contra);

        Add(
            req,
            materialNet,
            "progressao liquida material",
            contra);

        Add(
            req,
            noDominantPlateau,
            "ausencia de patamar operacional dominante",
            contra);

        Add(
            req,
            rareResets,
            "resets/rollovers ausentes ou raros",
            contra);

        if (f.NonDecreasingRatio >= .96)
        {
            sup.Add(
                "monotonicidade dominante");
        }

        if (f.RobustScale > 0)
        {
            sup.Add(
                "escala robusta nao nula");
        }

        var valid =
            distributed &&
            materialEvents &&
            materialNet &&
            noDominantPlateau &&
            rareResets;

        return Result(
            TagBehaviorType.Counter,
            valid
                ? StructuralEvidenceStatus.Valid
                : StructuralEvidenceStatus.Invalid,
            raw,
            c,
            req,
            sup,
            contra,
            valid
                ? "Progressao acumulativa material distribuida em multiplos sub-horizontes."
                : "Progressao acumulativa nao distribuida/material ao longo da janela.");
    }

    private static ClassEvidenceAssessment Continuous(
        BehaviorProfileV2Result raw,
        ObservationCoverage c)
    {
        var f = raw.Features;

        var req = new List<string>();
        var sup = new List<string>();
        var contra = new List<string>();

        if (f.SampleCount < MinimumSamples)
        {
            return Result(
                TagBehaviorType.Continuous,
                StructuralEvidenceStatus.Insufficient,
                raw,
                c,
                req,
                sup,
                contra,
                "Cobertura insuficiente.");
        }

        var levels =
            f.DistinctValueCount >= 5;

        var local =
            f.AdjacentLevelTransitionRatio >= .65 ||
            f.SmoothTransitionRatio >= .80;

        var distributed =
            c.SegmentsWithActivity >= 2;

        var material =
            f.RobustScale > 0 ||
            (
                f.EstimatedResolution > 0 &&
                f.ValueRange >=
                f.EstimatedResolution * 4
            );

        Add(
            req,
            levels,
            ">=5 niveis observados",
            contra);

        Add(
            req,
            local,
            "continuidade local/quantizacao ordenada",
            contra);

        Add(
            req,
            distributed,
            "atividade em >=2/4 sub-horizontes",
            contra);

        Add(
            req,
            material,
            "escala ou range material frente a resolucao",
            contra);

        sup.Add(
            f.ReversalRatio > 0
                ? "reversoes/ciclicidade observadas"
                : "monotonicidade aceita; reversao nao e requisito");

        var valid =
            levels &&
            local &&
            distributed &&
            material;

        return Result(
            TagBehaviorType.Continuous,
            valid
                ? StructuralEvidenceStatus.Valid
                : StructuralEvidenceStatus.Insufficient,
            raw,
            c,
            req,
            sup,
            contra,
            valid
                ? "Trajetoria numerica localmente continua com atividade distribuida."
                : "Estrutura continua plausivel, mas cobertura/atividade insuficiente.");
    }

    private static ClassEvidenceAssessment Discrete(
        BehaviorProfileV2Result raw,
        IReadOnlyList<HistoricoDetectorPoint> p,
        ObservationCoverage c)
    {
        var f = raw.Features;

        var req = new List<string>();
        var sup = new List<string>();
        var contra = new List<string>();

        if (p.Count < MinimumSamples)
        {
            return Result(
                TagBehaviorType.Discrete,
                StructuralEvidenceStatus.Insufficient,
                raw,
                c,
                req,
                sup,
                contra,
                "Cobertura insuficiente.");
        }

        var occupancies =
            p.GroupBy(x => x.Valor)
                .Select(
                    g =>
                        g.Count() /
                        (double)p.Count)
                .ToArray();

        var levels =
            f.DistinctValueCount is >= 2 and <= 10;

        var occupied =
            occupancies.Count(
                x => x >= .01) >= 2;

        var transitions =
            f.ChangeRatio > 0;

        var dwell =
            f.ZeroDeltaRatio >= .50 ||
            f.MedianDwellSamples >= 2 ||
            (
                levels &&
                f.ChangeRatio >= .50
            );

        Add(
            req,
            levels,
            "2-10 niveis",
            contra);

        Add(
            req,
            occupied,
            ">=2 niveis com ocupacao material",
            contra);

        Add(
            req,
            transitions,
            "ao menos uma transicao observada",
            contra);

        Add(
            req,
            dwell,
            "dwell entre transicoes",
            contra);

        if (f.ChangeRatio < .01 &&
            transitions)
        {
            sup.Add(
                "transicoes raras reduzem cobertura temporal, nao a estrutura discreta");
        }

        var valid =
            levels &&
            occupied &&
            transitions &&
            dwell;

        var constant =
            f.DistinctValueCount <= 1;

        return Result(
            TagBehaviorType.Discrete,
            valid
                ? StructuralEvidenceStatus.Valid
                : constant
                    ? StructuralEvidenceStatus.Insufficient
                    : StructuralEvidenceStatus.Invalid,
            raw,
            c,
            req,
            sup,
            contra,
            valid
                ? "Niveis discretos ocupados com dwell e transicoes."
                : constant
                    ? "Estado constante nao confirma estrutura discreta."
                    : "Estrutura discreta nao confirmada.");
    }

    private static ClassEvidenceAssessment Setpoint(
        BehaviorProfileV2Result raw,
        IReadOnlyList<HistoricoDetectorPoint> p,
        ObservationCoverage c)
    {
        var f = raw.Features;

        var req = new List<string>();
        var sup = new List<string>();
        var contra = new List<string>();

        var stepwise =
            f.DistinctValueCount is >= 2 and <= 12 &&
            f.ZeroDeltaRatio >= .75 &&
            f.ChangeRatio > 0 &&
            f.ChangeRatio <= .20;

        if (stepwise)
        {
            sup.Add(
                "ObservedStructure=StepwiseLevels");
        }
        else
        {
            contra.Add(
                "estrutura stepwise nao observada");
        }

        req.Add(
            "semantica externa de valor-alvo/configuracao (nao observavel na serie isolada)");

        return Result(
            TagBehaviorType.Setpoint,
            p.Count < MinimumSamples ||
            stepwise
                ? StructuralEvidenceStatus.Insufficient
                : StructuralEvidenceStatus.Invalid,
            raw,
            c,
            req,
            sup,
            contra,
            stepwise
                ? "Estruturalmente stepwise; sem metadado nao se afirma Setpoint semantico."
                : "Estrutura stepwise e semantica de Setpoint nao confirmadas.");
    }

    private static ObservationCoverage Coverage(
        IReadOnlyList<HistoricoDetectorPoint> p,
        BehaviorProfileFeatures f)
    {
        var changes =
            new int[SegmentCount];

        var positive =
            new int[SegmentCount];

        var net =
            new double[SegmentCount];

        for (var i = 1;
             i < p.Count;
             i++)
        {
            var segment =
                Math.Min(
                    SegmentCount - 1,
                    i * SegmentCount /
                    p.Count);

            var delta =
                p[i].Valor -
                p[i - 1].Valor;

            if (delta != 0)
            {
                changes[segment]++;
            }

            if (delta > 0)
            {
                positive[segment]++;
            }

            net[segment] += delta;
        }

        return new ObservationCoverage
        {
            SampleCount =
                p.Count,

            DurationMinutes =
                f.ObservedDurationMinutes,

            SegmentCount =
                SegmentCount,

            SegmentsWithActivity =
                changes.Count(
                    x => x > 0),

            SegmentsWithPositiveProgression =
                positive.Count(
                    x => x > 0),

            ChangesBySegment =
                changes,

            PositiveChangesBySegment =
                positive,

            NetProgressionBySegment =
                net,

            CriticalGapCount =
                f.CriticalGapCount
        };
    }

    private static void Add(
        List<string> req,
        bool met,
        string text,
        List<string> contra)
    {
        req.Add(
            $"{(met ? "MET" : "MISSING")}: {text}");

        if (!met)
        {
            contra.Add(text);
        }
    }

    private static ClassEvidenceAssessment Result(
        TagBehaviorType type,
        StructuralEvidenceStatus status,
        BehaviorProfileV2Result raw,
        ObservationCoverage c,
        IReadOnlyList<string> req,
        IReadOnlyList<string> sup,
        IReadOnlyList<string> contra,
        string reason)
    {
        var evidence =
            raw.EvidenceByType.First(
                e => e.Type == type);

        return new ClassEvidenceAssessment
        {
            Class =
                type,

            StructuralStatus =
                status,

            Score =
                evidence.Score,

            Margin =
                type == raw.Winner
                    ? raw.Margin
                    : evidence.Score -
                      raw.WinnerScore,

            RequiredEvidence =
                req,

            SupportingEvidence =
                sup,

            ContradictoryEvidence =
                contra,

            ObservationCoverage =
                c,

            Reason =
                reason
        };
    }
}