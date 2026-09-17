namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Constrói um baseline estatístico condicionado pelo regime operacional
/// atual de uma tag.
///
/// Responsabilidades:
///
/// 1. Converter os históricos brutos para HistoricoDetectorPoint.
/// 2. Utilizar OperationalRegimeHistoryResolver para selecionar apenas
///    as amostras pertencentes ao regime atual.
/// 3. Calcular as estatísticas desse subconjunto utilizando
///    TagProfileStatisticsCalculator.
/// 4. Retornar fallback seguro quando o baseline condicionado não puder
///    ser construído.
///
/// Este serviço ainda não altera PredictionService, DetectorInput,
/// ensemble ou detectores.
/// </summary>
public sealed class OperationalRegimeBaselineService
{
    private readonly OperationalRegimeHistoryResolver _historyResolver;

    private readonly TagProfileStatisticsCalculator _statisticsCalculator;

    public OperationalRegimeBaselineService(
        OperationalRegimeHistoryResolver historyResolver,
        TagProfileStatisticsCalculator statisticsCalculator)
    {
        _historyResolver =
            historyResolver ??
            throw new ArgumentNullException(
                nameof(historyResolver));

        _statisticsCalculator =
            statisticsCalculator ??
            throw new ArgumentNullException(
                nameof(statisticsCalculator));
    }

    public OperationalRegimeBaselineResolution Resolve(
        TagRegimeOperacionalIa configuration,
        IReadOnlyList<LeituraBruta> targetHistory,
        IReadOnlyList<LeituraBruta> regimeHistory,
        DateTime currentTimestamp)
    {
        ArgumentNullException.ThrowIfNull(
            configuration);

        ArgumentNullException.ThrowIfNull(
            targetHistory);

        ArgumentNullException.ThrowIfNull(
            regimeHistory);

        var targetPoints =
            targetHistory
                .Where(IsValidReading)
                .Select(ToDetectorPoint)
                .ToArray();

        var regimePoints =
            regimeHistory
                .Where(IsValidReading)
                .Select(ToDetectorPoint)
                .ToArray();

        var historyResolution =
            _historyResolver.Resolve(
                configuration,
                targetPoints,
                regimePoints,
                currentTimestamp);

        /*
         * O resolver já implementa o comportamento fail-safe.
         *
         * Se não houver configuração válida, estado atual conhecido,
         * alinhamento suficiente ou quantidade mínima de amostras,
         * não construímos baseline condicionado.
         */
        if (!historyResolution.Applied)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                historyResolution.Reason,
                historyResolution.CurrentRegimeValue,
                historyResolution.OriginalTargetSamples,
                historyResolution.AlignedSamples,
                historyResolution.SelectedSamples);
        }

        /*
         * O TagProfileStatisticsCalculator exige no mínimo dez pontos.
         *
         * Normalmente MinimoAmostrasPorRegime será maior que isso
         * em produção, porém mantemos a proteção aqui para que as
         * responsabilidades dos componentes não fiquem acopladas.
         */
        if (historyResolution.History.Count < 10)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Histórico do regime operacional possui menos de " +
                "10 amostras e não permite calcular estatísticas.",
                historyResolution.CurrentRegimeValue,
                historyResolution.OriginalTargetSamples,
                historyResolution.AlignedSamples,
                historyResolution.SelectedSamples);
        }

        /*
         * Reconstruímos LeituraBruta somente para o cálculo estatístico.
         *
         * O histórico retornado aos detectores continuará sendo
         * historyResolution.History, isto é, o histórico condicionado
         * antes da limpeza IQR.
         */
        var selectedRawHistory =
            historyResolution.History
                .Select(ToRawReading)
                .ToList();

        TagProfileStatisticsResult statistics;

        try
        {
            statistics =
                _statisticsCalculator.Calculate(
                    selectedRawHistory);
        }
        catch (InvalidOperationException exception)
        {
            /*
             * Exemplo:
             *
             * havia amostras suficientes antes da limpeza,
             * mas após remoção de inválidos/outliers restaram
             * menos de dez.
             *
             * Nesse caso fazemos fallback para o baseline global.
             */
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                $"Não foi possível calcular o baseline do regime operacional: " +
                $"{exception.Message}",
                historyResolution.CurrentRegimeValue,
                historyResolution.OriginalTargetSamples,
                historyResolution.AlignedSamples,
                historyResolution.SelectedSamples);
        }

        return OperationalRegimeBaselineResolution.CreateApplied(
            historyResolution,
            statistics);
    }

    private static bool IsValidReading(
        LeituraBruta reading)
    {
        return reading is not null &&
               reading.DataHora != default &&
               float.IsFinite(
                   reading.Valor);
    }

    private static HistoricoDetectorPoint ToDetectorPoint(
        LeituraBruta reading)
    {
        return new HistoricoDetectorPoint
        {
            DataHora =
                reading.DataHora,

            Valor =
                Convert.ToDouble(
                    reading.Valor)
        };
    }

    private static LeituraBruta ToRawReading(
        HistoricoDetectorPoint point)
    {
        return new LeituraBruta
        {
            DataHora =
                point.DataHora,

            Valor =
                (float)point.Valor
        };
    }
}

/// <summary>
/// Resultado da tentativa de construção de um baseline condicionado
/// por regime operacional.
///
/// Applied = false significa que o chamador deverá continuar utilizando
/// o histórico e o perfil global já existentes.
/// </summary>
public sealed class OperationalRegimeBaselineResolution
{
    public bool Applied { get; init; }

    public double? CurrentRegimeValue { get; init; }

    public int OriginalTargetSamples { get; init; }

    public int AlignedSamples { get; init; }

    public int SelectedSamples { get; init; }

    /// <summary>
    /// Histórico pertencente ao regime atual.
    ///
    /// Este histórico NÃO passou pela limpeza IQR.
    /// É o histórico destinado futuramente ao DetectorInput.
    /// </summary>
    public IReadOnlyList<HistoricoDetectorPoint> History { get; init; } =
        Array.Empty<HistoricoDetectorPoint>();

    /// <summary>
    /// Estatísticas calculadas sobre o histórico condicionado,
    /// utilizando a mesma matemática compartilhada pelo PerfilTagService.
    /// </summary>
    public TagProfileStatisticsResult? Statistics { get; init; }

    public string Reason { get; init; } = "";

    public static OperationalRegimeBaselineResolution CreateApplied(
        OperationalRegimeHistoryResolution historyResolution,
        TagProfileStatisticsResult statistics)
    {
        ArgumentNullException.ThrowIfNull(
            historyResolution);

        ArgumentNullException.ThrowIfNull(
            statistics);

        return new OperationalRegimeBaselineResolution
        {
            Applied =
                true,

            CurrentRegimeValue =
                historyResolution.CurrentRegimeValue,

            OriginalTargetSamples =
                historyResolution.OriginalTargetSamples,

            AlignedSamples =
                historyResolution.AlignedSamples,

            SelectedSamples =
                historyResolution.SelectedSamples,

            History =
                historyResolution.History.ToArray(),

            Statistics =
                statistics,

            Reason =
                "Baseline estatístico condicionado por regime operacional aplicado."
        };
    }

    public static OperationalRegimeBaselineResolution CreateNotApplied(
        string reason,
        double? currentRegimeValue = null,
        int originalTargetSamples = 0,
        int alignedSamples = 0,
        int selectedSamples = 0)
    {
        return new OperationalRegimeBaselineResolution
        {
            Applied =
                false,

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

            Statistics =
                null,

            Reason =
                reason
        };
    }
}