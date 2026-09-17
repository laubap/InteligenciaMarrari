namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Resolve o subconjunto do histórico de uma tag que pertence ao mesmo
/// regime operacional da leitura atualmente analisada.
///
/// Exemplo:
///
/// INLET_FLOW_LPS em 10:01
/// LagMinutos = 1
///
/// O estado operacional esperado é consultado em:
///
/// PUMP_RUNNING em 10:00
///
/// Se o estado encontrado for 0, somente pontos históricos de
/// INLET_FLOW_LPS cujo estado operacional correspondente também seja
/// aproximadamente 0 participam do baseline condicionado.
///
/// O alinhamento é sempre causal:
/// uma leitura futura da tag de regime nunca é utilizada para explicar
/// uma leitura passada da tag analisada.
/// </summary>
public sealed class OperationalRegimeHistoryResolver
{
    public OperationalRegimeHistoryResolution Resolve(
        TagRegimeOperacionalIa configuration,
        IReadOnlyList<HistoricoDetectorPoint> targetHistory,
        IReadOnlyList<HistoricoDetectorPoint> regimeHistory,
        DateTime currentTimestamp)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(targetHistory);
        ArgumentNullException.ThrowIfNull(regimeHistory);

        if (!configuration.Ativo)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "A configuração de regime operacional está desativada.");
        }

        if (configuration.LagMinutos < 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "LagMinutos não pode ser negativo.");
        }

        if (configuration.ToleranciaAlinhamentoMinutos < 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "ToleranciaAlinhamentoMinutos não pode ser negativa.");
        }

        if (!double.IsFinite(configuration.ToleranciaEstado) ||
            configuration.ToleranciaEstado < 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "ToleranciaEstado deve ser um número finito maior ou igual a zero.");
        }

        if (configuration.MinimoAmostrasPorRegime <= 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "MinimoAmostrasPorRegime deve ser maior que zero.");
        }

        var targetOrdered =
            targetHistory
                .Where(IsValidPoint)
                .OrderBy(x => x.DataHora)
                .ToArray();

        var regimeOrdered =
            regimeHistory
                .Where(IsValidPoint)
                .OrderBy(x => x.DataHora)
                .ToArray();

        if (targetOrdered.Length == 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "O histórico da tag analisada não possui pontos válidos.");
        }

        if (regimeOrdered.Length == 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "O histórico da tag de regime não possui pontos válidos.");
        }

        var expectedCurrentRegimeTimestamp =
            currentTimestamp.AddMinutes(
                -configuration.LagMinutos);

        var currentRegimePoint =
            FindCausalRegimePoint(
                regimeOrdered,
                expectedCurrentRegimeTimestamp,
                configuration.ToleranciaAlinhamentoMinutos);

        if (currentRegimePoint is null)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "Não foi possível determinar o regime operacional atual " +
                "dentro da tolerância temporal configurada.");
        }

        var currentRegimeValue =
            currentRegimePoint.Valor;

        var selectedHistory =
            new List<HistoricoDetectorPoint>(
                targetOrdered.Length);

        var alignedPoints = 0;

        foreach (var targetPoint in targetOrdered)
        {
            /*
             * Para cada ponto histórico da tag analisada, buscamos
             * o estado operacional causal correspondente.
             *
             * Exemplo:
             *
             * INLET em 10:01
             * Lag = 1 minuto
             *
             * estado esperado:
             * PUMP_RUNNING em 10:00.
             */
            var expectedRegimeTimestamp =
                targetPoint.DataHora.AddMinutes(
                    -configuration.LagMinutos);

            var regimePoint =
                FindCausalRegimePoint(
                    regimeOrdered,
                    expectedRegimeTimestamp,
                    configuration.ToleranciaAlinhamentoMinutos);

            if (regimePoint is null)
            {
                continue;
            }

            alignedPoints++;

            if (BelongsToSameState(
                    regimePoint.Valor,
                    currentRegimeValue,
                    configuration.ToleranciaEstado))
            {
                selectedHistory.Add(
                    targetPoint);
            }
        }

        if (selectedHistory.Count <
            configuration.MinimoAmostrasPorRegime)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "Histórico insuficiente para o regime operacional atual.",
                currentRegimeValue,
                targetOrdered.Length,
                alignedPoints,
                selectedHistory.Count);
        }

        return OperationalRegimeHistoryResolution.CreateApplied(
            currentRegimeValue,
            targetOrdered.Length,
            alignedPoints,
            selectedHistory);
    }

    /// <summary>
    /// Retorna a leitura mais recente da tag de regime que:
    ///
    /// 1. não ocorre depois do instante esperado;
    /// 2. não é mais antiga que a tolerância configurada.
    ///
    /// Dessa forma o algoritmo não utiliza informação futura.
    /// </summary>
    private static HistoricoDetectorPoint? FindCausalRegimePoint(
        IReadOnlyList<HistoricoDetectorPoint> regimeHistory,
        DateTime expectedTimestamp,
        int toleranceMinutes)
    {
        HistoricoDetectorPoint? candidate =
            null;

        for (var index = 0;
             index < regimeHistory.Count;
             index++)
        {
            var point =
                regimeHistory[index];

            if (point.DataHora >
                expectedTimestamp)
            {
                break;
            }

            candidate =
                point;
        }

        if (candidate is null)
        {
            return null;
        }

        var age =
            expectedTimestamp -
            candidate.DataHora;

        if (age < TimeSpan.Zero)
        {
            return null;
        }

        if (age >
            TimeSpan.FromMinutes(
                toleranceMinutes))
        {
            return null;
        }

        return candidate;
    }

    private static bool BelongsToSameState(
        double historicalValue,
        double currentValue,
        double tolerance)
    {
        if (!double.IsFinite(historicalValue) ||
            !double.IsFinite(currentValue))
        {
            return false;
        }

        return Math.Abs(
                   historicalValue -
                   currentValue)
               <= tolerance;
    }

    private static bool IsValidPoint(
        HistoricoDetectorPoint point)
    {
        return point is not null &&
               point.DataHora != default &&
               double.IsFinite(point.Valor);
    }
}

/// <summary>
/// Resultado da seleção do histórico condicionado pelo regime operacional.
///
/// Applied = false significa fallback:
/// o chamador deve continuar utilizando o histórico/baseline global existente.
/// </summary>
public sealed class OperationalRegimeHistoryResolution
{
    public bool Applied { get; init; }

    public double? CurrentRegimeValue { get; init; }

    public int OriginalTargetSamples { get; init; }

    public int AlignedSamples { get; init; }

    public int SelectedSamples { get; init; }

    public IReadOnlyList<HistoricoDetectorPoint> History { get; init; } =
        Array.Empty<HistoricoDetectorPoint>();

    public string Reason { get; init; } = "";

    public static OperationalRegimeHistoryResolution CreateApplied(
        double currentRegimeValue,
        int originalTargetSamples,
        int alignedSamples,
        IReadOnlyList<HistoricoDetectorPoint> history)
    {
        return new OperationalRegimeHistoryResolution
        {
            Applied = true,

            CurrentRegimeValue =
                currentRegimeValue,

            OriginalTargetSamples =
                originalTargetSamples,

            AlignedSamples =
                alignedSamples,

            SelectedSamples =
                history.Count,

            History =
                history,

            Reason =
                "Baseline condicionado por regime operacional aplicado."
        };
    }

    public static OperationalRegimeHistoryResolution CreateNotApplied(
        string reason,
        double? currentRegimeValue = null,
        int originalTargetSamples = 0,
        int alignedSamples = 0,
        int selectedSamples = 0)
    {
        return new OperationalRegimeHistoryResolution
        {
            Applied = false,

            CurrentRegimeValue =
                currentRegimeValue,

            OriginalTargetSamples =
                originalTargetSamples,

            AlignedSamples =
                alignedSamples,

            SelectedSamples =
                selectedSamples,

            History =
                Array.Empty<HistoricoDetectorPoint>(),

            Reason =
                reason
        };
    }
}