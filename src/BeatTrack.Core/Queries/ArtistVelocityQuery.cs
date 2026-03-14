namespace BeatTrack.Core.Queries;

public static class ArtistVelocityQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var artistsArg = ParseStringFlag(args, "--artists");
        var topN = ParseIntFlag(args, "--top") ?? 10;
        var bucket = ParseStringFlag(args, "--bucket") ?? "monthly";

        var timed = scrobbles.Where(static s => s.TimestampMs > 0).ToList();
        if (timed.Count == 0)
        {
            Console.WriteLine("(no scrobbles with timestamps found)");
            return 0;
        }

        // Determine target artists
        List<string> targetCanonical;
        if (artistsArg is not null)
        {
            targetCanonical = artistsArg.Split(',')
                .Select(static a => BeatTrackAnalysis.CanonicalizeArtistName(a.Trim()))
                .Where(static a => !string.IsNullOrWhiteSpace(a))
                .ToList();
        }
        else
        {
            targetCanonical = timed
                .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(static g => g.Count())
                .Take(topN)
                .Select(static g => g.Key)
                .ToList();
        }

        if (targetCanonical.Count == 0)
        {
            Console.WriteLine("(no target artists specified)");
            return 0;
        }

        var targetSet = new HashSet<string>(targetCanonical, StringComparer.OrdinalIgnoreCase);

        // Get display names (first occurrence in data)
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in timed)
        {
            var canonical = BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName);
            if (targetSet.Contains(canonical))
            {
                displayNames.TryAdd(canonical, s.ArtistName);
            }
        }

        // Filter to target artists and bucket
        var filtered = timed
            .Where(s => targetSet.Contains(BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName)))
            .Select(s => (
                Canonical: BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName),
                Period: GetPeriodKey(s.TimestampMs, bucket)))
            .ToList();

        // Get all unique periods sorted
        var allPeriods = filtered.Select(static x => x.Period).Distinct().Order().ToList();

        // Build cumulative counts per artist per period
        var counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonical in targetCanonical)
        {
            counts[canonical] = [];
        }

        foreach (var (canonical, period) in filtered)
        {
            var artistCounts = counts[canonical];
            artistCounts.TryGetValue(period, out var current);
            artistCounts[period] = current + 1;
        }

        // Convert to cumulative
        var cumulative = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonical in targetCanonical)
        {
            var cum = new Dictionary<string, int>(StringComparer.Ordinal);
            var running = 0;
            foreach (var period in allPeriods)
            {
                counts[canonical].TryGetValue(period, out var periodCount);
                running += periodCount;
                cum[period] = running;
            }
            cumulative[canonical] = cum;
        }

        // Output as table
        var nameWidth = targetCanonical.Max(c => (displayNames.GetValueOrDefault(c, c)).Length);
        nameWidth = Math.Max(nameWidth, 8);

        Console.WriteLine($"artist_velocity ({bucket}, {targetCanonical.Count} artists):");
        Console.WriteLine();

        // Header
        Console.Write($"  {"period",-10}");
        foreach (var canonical in targetCanonical)
        {
            var name = displayNames.GetValueOrDefault(canonical, canonical);
            if (name.Length > 20) name = name[..17] + "...";
            Console.Write($" | {name,20}");
        }
        Console.WriteLine();

        Console.Write($"  {"----------",-10}");
        foreach (var _ in targetCanonical)
        {
            Console.Write($" | {"--------------------",20}");
        }
        Console.WriteLine();

        // Data rows — skip periods where no target artist has any cumulative plays
        foreach (var period in allPeriods)
        {
            if (!targetCanonical.Any(c => cumulative[c].GetValueOrDefault(period) > 0))
            {
                continue;
            }

            Console.Write($"  {period,-10}");
            foreach (var canonical in targetCanonical)
            {
                var value = cumulative[canonical].GetValueOrDefault(period);
                Console.Write($" | {value,20:N0}");
            }
            Console.WriteLine();
        }

        return 0;
    }

    private static string GetPeriodKey(long timestampMs, string bucket)
    {
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).ToLocalTime();
        return bucket.ToLowerInvariant() switch
        {
            "daily" => dt.ToString("yyyy-MM-dd"),
            "weekly" => $"{dt:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(dt.DateTime):D2}",
            "yearly" => dt.ToString("yyyy"),
            _ => dt.ToString("yyyy-MM"), // monthly
        };
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
