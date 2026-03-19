using BeatTrack.Core.Views;
using Markout;

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

        var longest = overallStreaks.OrderByDescending(static s => s.Days).FirstOrDefault();

        var view = new StreaksView
        {
            Longest = longest is not null
                ? $"{longest.Days} days ({longest.Start} to {longest.End})"
                : "0",
            Current = currentStreak is not null
                ? $"{currentStreak.Days} days ({currentStreak.Start} to {currentStreak.End})"
                : "0 (not scrobbling today)",
        };

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

            var artistStreaks = FindStreaks(artistDates);
            var filtered = artistStreaks
                .Where(s => s.Days >= minDays)
                .OrderByDescending(static s => s.Days)
                .Select(s => new StreakRow
                {
                    Days = s.Days,
                    Start = s.Start.ToString("yyyy-MM-dd"),
                    End = s.End.ToString("yyyy-MM-dd"),
                })
                .ToList();

            view.Title = $"Streaks: {targetArtist}";
            view.TopStreaks = filtered.Count > 0 ? filtered : null;
        }
        else
        {
            // Top overall streaks
            view.TopStreaks = overallStreaks
                .OrderByDescending(static s => s.Days)
                .Take(10)
                .Select(s => new StreakRow
                {
                    Days = s.Days,
                    Start = s.Start.ToString("yyyy-MM-dd"),
                    End = s.End.ToString("yyyy-MM-dd"),
                })
                .ToList();

            // Top artist streaks
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

            view.ArtistStreaks = artistStreakResults
                .Select(r => new ArtistStreakRow
                {
                    Artist = r.OriginalName,
                    Days = r.LongestStreak!.Days,
                    Start = r.LongestStreak.Start.ToString("yyyy-MM-dd"),
                    End = r.LongestStreak.End.ToString("yyyy-MM-dd"),
                    TotalPlays = r.TotalPlays,
                })
                .ToList();
        }

        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

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
