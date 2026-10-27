namespace IAApi.IntelligenceLab.Evaluation;

public static class AlertEpisodeNormalizer
{
    public static IReadOnlyList<AlertEpisode> Normalize(
        IReadOnlyList<AiReadingEvaluationResult> readings,
        TimeSpan cadence)
    {
        ArgumentNullException.ThrowIfNull(readings);
        if (cadence <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cadence));
        }

        var episodes = new List<AlertEpisode>();
        var episodeNumber = 0;

        foreach (var group in readings
                     .OrderBy(x => x.TestClientId, StringComparer.Ordinal)
                     .ThenBy(x => x.TagName, StringComparer.Ordinal)
                     .ThenBy(x => x.TimestampUtc)
                     .ThenBy(x => x.ReadingId, StringComparer.Ordinal)
                     .GroupBy(x => (x.TestClientId, x.TagName)))
        {
            var current = new List<AiReadingEvaluationResult>();
            DateTimeOffset? previousTimestamp = null;

            foreach (var reading in group)
            {
                // Uma leitura negativa entre positivos encerra o episódio,
                // mesmo que os timestamps positivos estejam dentro da cadência.
                if (!reading.FinalAlert)
                {
                    if (current.Count > 0)
                    {
                        episodes.Add(CreateEpisode(++episodeNumber, group.Key.TestClientId,
                            group.Key.TagName, current));
                        current = [];
                    }

                    previousTimestamp = null;
                    continue;
                }

                if (previousTimestamp.HasValue && reading.TimestampUtc - previousTimestamp > cadence)
                {
                    episodes.Add(CreateEpisode(++episodeNumber, group.Key.TestClientId,
                        group.Key.TagName, current));
                    current = [];
                }

                current.Add(reading);
                previousTimestamp = reading.TimestampUtc;
            }

            if (current.Count > 0)
            {
                episodes.Add(CreateEpisode(++episodeNumber, group.Key.TestClientId,
                    group.Key.TagName, current));
            }
        }

        return episodes
            .OrderBy(x => x.StartedAtUtc)
            .ThenBy(x => x.EpisodeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static AlertEpisode CreateEpisode(
        int number,
        string clientId,
        string tagName,
        IReadOnlyList<AiReadingEvaluationResult> readings) =>
        new(
            $"episode-{number:D6}",
            clientId,
            tagName,
            readings[0].TimestampUtc,
            readings[^1].TimestampUtc,
            readings.Count,
            readings.Select(x => x.ReadingId).ToArray());
}
