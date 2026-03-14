namespace BeatTrack.Core.Queries;

public static class TopArtistsQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var window = ParseStringFlag(args, "--window") ?? "30d";
        var limit = ParseIntFlag(args, "--limit") ?? 50;

        var cutoffMs = ParseWindowCutoff(window);
        var filtered = cutoffMs > 0
            ? scrobbles.Where(s => s.TimestampMs >= cutoffMs && s.TimestampMs > 0).ToList()
            : scrobbles.Where(static s => s.TimestampMs > 0).ToList();

        if (filtered.Count == 0)
        {
            Console.WriteLine($"(no scrobbles found in {window} window)");
            return 0;
        }

        var ranked = filtered
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.First().ArtistName, Canonical: g.Key, Count: g.Count()))
            .OrderByDescending(static x => x.Count)
            .Take(limit)
            .ToList();

        var maxCount = ranked[0].Count;

        Console.WriteLine($"top_artists ({window}, {filtered.Count:N0} scrobbles):");
        Console.WriteLine();

        foreach (var (name, _, count) in ranked)
        {
            var tier = GetTier(count, maxCount);
            var bar = new string('█', Math.Max(1, (int)(30.0 * count / maxCount)));
            Console.WriteLine($"  {bar} {name} ({count:N0})");
        }

        return 0;
    }

    private static int GetTier(int count, int maxCount)
    {
        var ratio = (double)count / maxCount;
        return ratio switch
        {
            >= 0.6 => 5,
            >= 0.3 => 4,
            >= 0.15 => 3,
            >= 0.05 => 2,
            _ => 1,
        };
    }

    internal static long ParseWindowCutoff(string window)
    {
        var lower = window.ToLowerInvariant().Trim();
        if (lower is "all")
        {
            return 0;
        }

        if (lower.EndsWith('d') && int.TryParse(lower[..^1], out var days))
        {
            return DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();
        }

        if (lower.EndsWith('m') && int.TryParse(lower[..^1], out var months))
        {
            return DateTimeOffset.UtcNow.AddDays(-months * 30).ToUnixTimeMilliseconds();
        }

        if (lower.EndsWith('y') && int.TryParse(lower[..^1], out var years))
        {
            return DateTimeOffset.UtcNow.AddDays(-years * 365).ToUnixTimeMilliseconds();
        }

        return DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
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
}
