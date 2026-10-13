namespace IAApi.Benchmark;

/// <summary>
/// Plano determinístico do benchmark rápido.
///
/// A amostra é formada por várias janelas temporais distribuídas de forma
/// uniforme ao longo de todo o período disponível. Todas as tags continuam
/// participando; apenas o período total processado é reduzido.
/// </summary>
public sealed class BenchmarkFastPlan
{
    public long FullSampleCount { get; init; }

    public long PlannedSampleCount { get; init; }

    public int WindowCount { get; init; }

    public int WindowLengthMinutes { get; init; }

    public IReadOnlyList<BenchmarkFastWindow> Windows { get; init; } =
        Array.Empty<BenchmarkFastWindow>();
}

public sealed class BenchmarkFastWindow
{
    public int Index { get; init; }

    public int StartMinuteInclusive { get; init; }

    public int EndMinuteInclusive { get; init; }

    public long ExpectedSamples { get; init; }
}

public static class BenchmarkFastPlanBuilder
{
    public static BenchmarkFastPlan Build(
        SqliteDatasetReader reader,
        IReadOnlyCollection<string> tags,
        BenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(options);

        var bounds =
            reader.GetMinuteBounds(tags);

        if (bounds is null)
        {
            return new BenchmarkFastPlan();
        }

        var fullCount =
            reader.GetTotalSamples(tags);

        if (fullCount <= 0)
        {
            return new BenchmarkFastPlan();
        }

        var target =
            Math.Clamp(
                options.FastTargetSamples,
                1,
                fullCount);

        if (target >= fullCount)
        {
            var only =
                new BenchmarkFastWindow
                {
                    Index = 1,
                    StartMinuteInclusive = bounds.Value.MinMinute,
                    EndMinuteInclusive = bounds.Value.MaxMinute,
                    ExpectedSamples = fullCount
                };

            return new BenchmarkFastPlan
            {
                FullSampleCount = fullCount,
                PlannedSampleCount = fullCount,
                WindowCount = 1,
                WindowLengthMinutes =
                    bounds.Value.MaxMinute -
                    bounds.Value.MinMinute + 1,
                Windows = [only]
            };
        }

        var requestedWindows =
            Math.Max(
                1,
                options.FastWindowCount);

        var totalMinutes =
            (long)bounds.Value.MaxMinute -
            bounds.Value.MinMinute + 1L;

        var density =
            fullCount /
            (double)Math.Max(
                1,
                totalMinutes);

        /*
         * Quantos minutos, em média, precisamos para atingir o alvo.
         * Cada janela tem o mesmo tamanho e as janelas ficam espalhadas
         * por toda a duração do banco.
         */
        var sampledMinutesTotal =
            (long)Math.Ceiling(
                target /
                Math.Max(
                    density,
                    1e-9));

        var windowLength =
            (int)Math.Ceiling(
                sampledMinutesTotal /
                (double)requestedWindows);

        windowLength =
            Math.Max(
                windowLength,
                options.FastMinimumWindowMinutes);

        windowLength =
            (int)Math.Min(
                windowLength,
                totalMinutes);

        /*
         * Se as janelas ficarem grandes demais para serem realmente
         * distribuídas, reduzimos a quantidade delas até não haver
         * sobreposição relevante.
         */
        var maximumNonOverlappingWindows =
            Math.Max(
                1,
                (int)(totalMinutes /
                      Math.Max(
                          1,
                          windowLength)));

        var windowCount =
            Math.Min(
                requestedWindows,
                maximumNonOverlappingWindows);

        var maxStart =
            bounds.Value.MaxMinute -
            windowLength + 1;

        var windows =
            new List<BenchmarkFastWindow>(
                windowCount);

        for (var index = 0;
             index < windowCount;
             index++)
        {
            var start =
                windowCount == 1
                    ? bounds.Value.MinMinute +
                      (int)Math.Max(
                          0,
                          (totalMinutes -
                           windowLength) /
                          2)
                    : bounds.Value.MinMinute +
                      (int)Math.Round(
                          index *
                          (maxStart -
                           bounds.Value.MinMinute) /
                          (double)(windowCount - 1));

            var end =
                Math.Min(
                    bounds.Value.MaxMinute,
                    start +
                    windowLength -
                    1);

            var expected =
                reader.GetTotalSamples(
                    tags,
                    start,
                    end);

            windows.Add(
                new BenchmarkFastWindow
                {
                    Index =
                        index + 1,

                    StartMinuteInclusive =
                        start,

                    EndMinuteInclusive =
                        end,

                    ExpectedSamples =
                        expected
                });
        }

        return new BenchmarkFastPlan
        {
            FullSampleCount =
                fullCount,

            PlannedSampleCount =
                windows.Sum(
                    item =>
                        item.ExpectedSamples),

            WindowCount =
                windows.Count,

            WindowLengthMinutes =
                windowLength,

            Windows =
                windows.AsReadOnly()
        };
    }
}
