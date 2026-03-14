namespace BeatTrack.Core.Queries;

/// <summary>
/// Classifies artists active in a window along an engagement gradient:
///   New Discovery    — zero prior plays, artist is completely new
///   First Click      — had a few prior plays that never stuck, now truly engaged
///   Rediscovery      — meaningful prior engagement, went dormant, now back
///   Longtime Fan     — continuously engaged, no significant gap
/// </summary>
public static class NewDiscoveriesQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var window = ParseStringFlag(args, "--window") ?? "30d";
        var limit = ParseIntFlag(args, "--limit") ?? 50;
        var minPlays = ParseIntFlag(args, "--min") ?? 3;
        var priorThreshold = ParseIntFlag(args, "--prior-threshold") ?? 5;
        var gapDays = ParseIntFlag(args, "--gap") ?? 180;

        var cutoffMs = TopArtistsQuery.ParseWindowCutoff(window);
        var timed = scrobbles.Where(static s => s.TimestampMs > 0).ToList();

        if (timed.Count == 0)
        {
            Console.WriteLine("(no scrobbles with timestamps found)");
            return 0;
        }

        var gapThresholdMs = (long)gapDays * 24 * 60 * 60 * 1000;

        // Group by canonical artist name, compute engagement metrics
        var classified = timed
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var recentCount = cutoffMs > 0 ? g.Count(s => s.TimestampMs >= cutoffMs) : g.Count();
                var priorScrobbles = g.Where(s => s.TimestampMs < cutoffMs).ToList();
                var priorPlays = priorScrobbles.Count;
                var firstScrobble = g.Min(static s => s.TimestampMs);
                var lastPriorMs = priorScrobbles.Count > 0
                    ? priorScrobbles.Max(static s => s.TimestampMs)
                    : 0L;

                var category = priorPlays == 0
                    ? EngagementCategory.NewDiscovery
                    : priorPlays < priorThreshold
                        ? EngagementCategory.FirstClick
                        : cutoffMs - lastPriorMs > gapThresholdMs
                            ? EngagementCategory.Rediscovery
                            : EngagementCategory.LongtimeFan;

                return new ClassifiedArtist(
                    g.First().ArtistName,
                    g.Key,
                    firstScrobble,
                    recentCount,
                    priorPlays,
                    lastPriorMs,
                    g.Count(),
                    category);
            })
            .Where(a => a.RecentCount >= minPlays)
            .OrderByDescending(static a => a.RecentCount)
            .ToList();

        if (classified.Count == 0)
        {
            Console.WriteLine($"new_discoveries ({window}): none with >= {minPlays} plays");
            return 0;
        }

        var grouped = classified
            .GroupBy(static a => a.Category)
            .OrderBy(static g => g.Key)
            .ToList();

        // Summary line
        var counts = grouped.Select(g => $"{CategoryLabel(g.Key)}: {g.Count()}");
        Console.WriteLine($"engagement ({window}, {classified.Count} active artists): {string.Join(", ", counts)}");
        Console.WriteLine();

        foreach (var group in grouped)
        {
            Console.WriteLine($"  {CategoryLabel(group.Key)}:");

            foreach (var a in group.Take(limit))
            {
                var detail = group.Key switch
                {
                    EngagementCategory.NewDiscovery =>
                        $"{a.RecentCount:N0} plays, discovered {FormatDate(a.FirstScrobble)}",

                    EngagementCategory.FirstClick =>
                        $"{a.RecentCount:N0} plays now, {a.PriorPlays} prior play{(a.PriorPlays == 1 ? "" : "s")}, first heard {FormatDate(a.FirstScrobble)}",

                    EngagementCategory.Rediscovery =>
                        $"{a.RecentCount:N0} plays now, {a.PriorPlays:N0} prior, absent {FormatGap(cutoffMs - a.LastPriorMs)}",

                    EngagementCategory.LongtimeFan =>
                        $"{a.RecentCount:N0} recent, {a.TotalCount:N0} total",

                    _ => $"{a.RecentCount:N0} plays",
                };

                Console.WriteLine($"    {a.Name}  ({detail})");
            }

            if (group.Count() > limit)
            {
                Console.WriteLine($"    ... and {group.Count() - limit} more");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static string CategoryLabel(EngagementCategory category) => category switch
    {
        EngagementCategory.NewDiscovery => "new discovery",
        EngagementCategory.FirstClick => "first click",
        EngagementCategory.Rediscovery => "rediscovery",
        EngagementCategory.LongtimeFan => "longtime fan",
        _ => "unknown",
    };

    private static string FormatDate(long timestampMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).ToLocalTime().ToString("yyyy-MM-dd");

    private static string FormatGap(long gapMs)
    {
        var days = (int)(gapMs / (24L * 60 * 60 * 1000));
        if (days >= 365)
        {
            var years = days / 365;
            var remainingMonths = (days % 365) / 30;
            return remainingMonths > 0 ? $"{years}y {remainingMonths}mo" : $"{years}y";
        }

        if (days >= 30)
        {
            return $"{days / 30}mo";
        }

        return $"{days}d";
    }

    private static string? ParseStringFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static int? ParseIntFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }
        return null;
    }

    private enum EngagementCategory
    {
        NewDiscovery = 0,
        FirstClick = 1,
        Rediscovery = 2,
        LongtimeFan = 3,
    }

    private sealed record ClassifiedArtist(
        string Name,
        string Canonical,
        long FirstScrobble,
        int RecentCount,
        int PriorPlays,
        long LastPriorMs,
        int TotalCount,
        EngagementCategory Category);
}
