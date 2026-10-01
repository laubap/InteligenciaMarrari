using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Configuração conservadora para descoberta inicial de mudanças de patamar.
///
/// Nesta etapa procuramos somente mudanças persistentes de nível.
/// Outros tipos de mudança comportamental serão adicionados posteriormente.
/// </summary>
public sealed record BehaviorChangePointOptions
{
    /// <summary>
    /// Quantidade de leituras comparadas antes e depois de um possível corte.
    /// </summary>
    public int WindowSize { get; init; } = 30;

    /// <summary>
    /// Diferença mínima entre medianas em unidades de escala robusta local.
    /// </summary>
    public double MinimumRobustEffect { get; init; } = 4.0;

    /// <summary>
    /// O deslocamento deve representar ao menos esta fração
    /// da amplitude observada no segmento.
    /// </summary>
    public double MinimumRangeFraction { get; init; } = 0.10;

    /// <summary>
    /// Distância mínima entre candidatos independentes.
    /// </summary>
    public int SuppressionWindow { get; init; } = 30;
}

/// <summary>
/// Possível mudança comportamental encontrada dentro
/// de um segmento temporal contínuo.
///
/// Ainda não representa um comportamento BEH-xxx.
/// </summary>
public sealed record BehaviorChangePointCandidate
{
    public int SegmentIndex { get; init; }

    public int ReadingIndex { get; init; }

    public DateTime Timestamp { get; init; }

    public double MedianBefore { get; init; }

    public double MedianAfter { get; init; }

    public double AbsoluteShift { get; init; }

    public double RobustScale { get; init; }

    public double RobustEffect { get; init; }

    public double RangeFraction { get; init; }

    /// <summary>
    /// Salto observado exatamente na fronteira candidata:
    ///
    /// abs(valor[index] - valor[index - 1])
    ///
    /// É usado principalmente para desempatar vários candidatos
    /// equivalentes produzidos pelas janelas deslizantes.
    /// </summary>
    public double BoundaryJump { get; init; }

    public string Reason { get; init; } = "";
}

/// <summary>
/// Procura mudanças persistentes de patamar em um
/// segmento temporal contínuo.
///
/// Compara duas janelas adjacentes:
///
///     janela anterior | possível corte | janela posterior
///
/// A mediana reduz sensibilidade a picos.
///
/// Quando vários candidatos vizinhos possuem força estatística
/// equivalente, damos preferência à fronteira que apresenta
/// o maior salto entre leituras consecutivas.
///
/// Este componente:
/// - não cria BEH-001;
/// - não persiste dados;
/// - não classifica normal/anormal.
/// </summary>
public sealed class BehaviorChangePointDetector
{
    private const double Epsilon = 1e-9;

    private readonly BehaviorChangePointOptions _options;

    public BehaviorChangePointDetector(
        BehaviorChangePointOptions? options = null)
    {
        _options =
            options ??
            new BehaviorChangePointOptions();

        ValidateOptions(
            _options);
    }

    public IReadOnlyList<BehaviorChangePointCandidate> Detect(
        BehaviorHistorySegment segment)
    {
        ArgumentNullException.ThrowIfNull(
            segment);

        var history =
            segment.Historico;

        var window =
            _options.WindowSize;

        if (history.Count < window * 2)
        {
            return Array.Empty<BehaviorChangePointCandidate>();
        }

        var values =
            history
                .Select(reading =>
                    Convert.ToDouble(
                        reading.Valor))
                .ToArray();

        var segmentMinimum =
            values.Min();

        var segmentMaximum =
            values.Max();

        var segmentRange =
            segmentMaximum -
            segmentMinimum;

        var rawCandidates =
            new List<BehaviorChangePointCandidate>();

        for (var index = window;
             index <= history.Count - window;
             index++)
        {
            var before =
                values
                    .Skip(index - window)
                    .Take(window)
                    .ToArray();

            var after =
                values
                    .Skip(index)
                    .Take(window)
                    .ToArray();

            var medianBefore =
                DetectorMath.Median(before);

            var medianAfter =
                DetectorMath.Median(after);

            var absoluteShift =
                Math.Abs(
                    medianAfter -
                    medianBefore);

            if (absoluteShift <= Epsilon)
            {
                continue;
            }

            var madBefore =
                DetectorMath.Mad(
                    before,
                    medianBefore);

            var madAfter =
                DetectorMath.Mad(
                    after,
                    medianAfter);

            /*
             * 1.4826 transforma aproximadamente MAD em escala
             * comparável ao desvio padrão para distribuição normal.
             */
            var robustScale =
                Math.Max(
                    Math.Max(
                        madBefore,
                        madAfter) * 1.4826,
                    Epsilon);

            var robustEffect =
                absoluteShift /
                robustScale;

            var rangeFraction =
                segmentRange > Epsilon
                    ? absoluteShift /
                      segmentRange
                    : 0;

            if (robustEffect <
                _options.MinimumRobustEffect)
            {
                continue;
            }

            if (rangeFraction <
                _options.MinimumRangeFraction)
            {
                continue;
            }

            /*
             * Mede a alteração exatamente na fronteira proposta.
             *
             * Exemplo:
             *
             * índice 29 = 40
             * índice 30 = 75
             *
             * BoundaryJump = 35
             *
             * Isso permite distinguir o corte verdadeiro de índices
             * próximos que apresentam a mesma diferença entre medianas
             * apenas por causa da sobreposição das janelas.
             */
            var boundaryJump =
                index > 0
                    ? Math.Abs(
                        values[index] -
                        values[index - 1])
                    : 0;

            rawCandidates.Add(
                new BehaviorChangePointCandidate
                {
                    SegmentIndex =
                        segment.Index,

                    ReadingIndex =
                        index,

                    Timestamp =
                        history[index].DataHora,

                    MedianBefore =
                        medianBefore,

                    MedianAfter =
                        medianAfter,

                    AbsoluteShift =
                        absoluteShift,

                    RobustScale =
                        robustScale,

                    RobustEffect =
                        robustEffect,

                    RangeFraction =
                        rangeFraction,

                    BoundaryJump =
                        boundaryJump,

                    Reason =
                        $"Mudança persistente de nível: " +
                        $"mediana {medianBefore:G6} -> {medianAfter:G6}; " +
                        $"efeito robusto={robustEffect:F2}; " +
                        $"fração do range={rangeFraction:P1}; " +
                        $"salto na fronteira={boundaryJump:G6}."
                });
        }

        return SuppressNearbyCandidates(
            rawCandidates);
    }

    private IReadOnlyList<BehaviorChangePointCandidate>
        SuppressNearbyCandidates(
            IReadOnlyList<BehaviorChangePointCandidate> candidates)
    {
        if (candidates.Count <= 1)
        {
            return candidates;
        }

        /*
         * Uma mudança verdadeira normalmente produz vários candidatos
         * vizinhos enquanto as janelas atravessam a transição.
         *
         * Critérios:
         *
         * 1. maior efeito robusto;
         * 2. maior deslocamento entre medianas;
         * 3. maior salto exatamente na fronteira.
         *
         * O terceiro critério resolve empates como:
         *
         * 40 40 40 | 75 75 75
         *
         * nos quais vários índices podem apresentar medianas
         * anterior/posterior igualmente fortes.
         */
        var orderedByStrength =
            candidates
                .OrderByDescending(candidate =>
                    candidate.RobustEffect)
                .ThenByDescending(candidate =>
                    candidate.AbsoluteShift)
                .ThenByDescending(candidate =>
                    candidate.BoundaryJump)
                .ThenBy(candidate =>
                    candidate.ReadingIndex)
                .ToArray();

        var accepted =
            new List<BehaviorChangePointCandidate>();

        foreach (var candidate in orderedByStrength)
        {
            var tooClose =
                accepted.Any(existing =>
                    Math.Abs(
                        existing.ReadingIndex -
                        candidate.ReadingIndex) <=
                    _options.SuppressionWindow);

            if (tooClose)
            {
                continue;
            }

            accepted.Add(
                candidate);
        }

        return accepted
            .OrderBy(candidate =>
                candidate.ReadingIndex)
            .ToArray();
    }

    private static void ValidateOptions(
        BehaviorChangePointOptions options)
    {
        if (options.WindowSize < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "WindowSize deve ser maior ou igual a 2.");
        }

        if (options.MinimumRobustEffect <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MinimumRobustEffect deve ser maior que zero.");
        }

        if (options.MinimumRangeFraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MinimumRangeFraction deve estar entre 0 e 1.");
        }

        if (options.SuppressionWindow < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "SuppressionWindow não pode ser negativo.");
        }
    }
}