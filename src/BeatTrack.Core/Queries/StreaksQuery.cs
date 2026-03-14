namespace BeatTrack.Core.Queries;

public static class StreaksQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var targetArtist = ParseStringFlag(args, "--artist");
        var topCount = ParseIntFlag(args, "--top") ?? 10;
        var minDays = ParseIntFlag(args, "--min-days") ?? 3;

        var timed = scrobbles.Where(static s => s.TimestampMs > 0).ToList();
        if (timed.Count == 0)
        {
            Console.WriteLine("(no scrobbles with timestamps found)");
            return 0;
        }

        // Overall listening streaks
        var allDates = timed
            .Select(static s => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().DateTime))
            .Distinct()
            .Order()
            .ToList();

        var overallStreaks = FindStreaks(allDates);
        var currentStreak = overallStreaks.LastOrDefault();
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Check if current streak is actually current (ends today or yesterday)
        if (currentStreak is not null && currentStreak.End < today.AddDays(-1))
        {
            currentStreak = null;
        }

        Console.WriteLine("overall_streaks:");
        var longest = overallStreaks.OrderByDescending(static s => s.Days).FirstOrDefault();
        if (longest is not null)
        {
            Console.WriteLine($"  longest: {longest.Days} days ({longest.Start} to {longest.End})");
        }

        if (currentStreak is not null)
        {
            Console.WriteLine($"  current: {currentStreak.Days} days ({currentStreak.Start} to {currentStreak.End})");
        }
        else
        {
            Console.WriteLine("  current: 0 (not scrobbling today)");
        }

        Console.WriteLine($"  top_streaks:");
        foreach (var streak in overallStreaks.OrderByDescending(static s => s.Days).Take(10))
        {
            Console.WriteLine($"    {streak.Days} days: {streak.Start} to {streak.End}");
        }

        // Per-artist streaks
        if (targetArtist is not null)
        {
            var canonical = BeatTrackAnalysis.CanonicalizeArtistName(targetArtist);
            var artistDates = timed
                .Where(s => string.Equals(BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), canonical, StringComparison.OrdinalIgnoreCase))
                .Select(static s => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().DateTime))
                .Distinct()
                .Order()
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"streaks for \"{targetArtist}\" ({artistDates.Count} active days):");
            var artistStreaks = FindStreaks(artistDates);
            foreach (var streak in artistStreaks.Where(s => s.Days >= minDays).OrderByDescending(static s => s.Days))
            {
                Console.WriteLine($"  {streak.Days} days: {streak.Start} to {streak.End}");
            }

            if (!artistStreaks.Any(s => s.Days >= minDays))
            {
                Console.WriteLine($"  (no streaks >= {minDays} days)");
            }
        }
        else
        {
            // Top artist streaks
            Console.WriteLine();
            Console.WriteLine($"artist_streaks (top {topCount} by longest streak, min {minDays}d):");

            var artistStreakResults = timed
                .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var dates = g
                        .Select(static s => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().DateTime))
                        .Distinct()
                        .Order()
                        .ToList();

                    var streaks = FindStreaks(dates);
                    var longestStreak = streaks.OrderByDescending(static s => s.Days).FirstOrDefault();
                    return (Name: g.Key, OriginalName: g.First().ArtistName, LongestStreak: longestStreak, TotalPlays: g.Count());
                })
                .Where(x => x.LongestStreak is not null && x.LongestStreak.Days >= minDays)
                .OrderByDescending(static x => x.LongestStreak!.Days)
                .Take(topCount)
                .ToList();

            foreach (var result in artistStreakResults)
            {
                Console.WriteLine($"  {result.OriginalName}: {result.LongestStreak!.Days} days ({result.LongestStreak.Start} to {result.LongestStreak.End}), {result.TotalPlays:N0} total plays");
            }
        }

        return 0;
    }

    internal static IReadOnlyList<Streak> FindStreaks(IReadOnlyList<DateOnly> sortedDates)
    {
        if (sortedDates.Count == 0)
        {
            return [];
        }

        var streaks = new List<Streak>();
        var start = sortedDates[0];
        var prev = sortedDates[0];

        for (var i = 1; i < sortedDates.Count; i++)
        {
            var current = sortedDates[i];
            if (current.DayNumber - prev.DayNumber > 1)
            {
                streaks.Add(new Streak(start, prev, prev.DayNumber - start.DayNumber + 1));
                start = current;
            }
            prev = current;
        }

        streaks.Add(new Streak(start, prev, prev.DayNumber - start.DayNumber + 1));
        return streaks;
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

public sealed record Streak(DateOnly Start, DateOnly End, int Days);
