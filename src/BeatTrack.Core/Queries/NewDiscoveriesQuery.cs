using BeatTrack.Core.Views;
using Markout;

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

        // --- Build view model ---
        var view = new NewDiscoveriesView
        {
            Title = "Engagement (30d)",
            ActiveArtists = classified.Count,
            Window = window,
        };

        foreach (var group in grouped)
        {
            switch (group.Key)
            {
                case EngagementCategory.NewDiscovery:
                    view.NewDiscoveries = group.Take(limit).Select(a => new NewDiscoveryRow
                    {
                        Artist = a.Name,
                        Plays = a.RecentCount,
                        Discovered = FormatDate(a.FirstScrobble),
                    }).ToList();
                    break;

                case EngagementCategory.FirstClick:
                    view.FirstClicks = group.Take(limit).Select(a => new FirstClickRow
                    {
                        Artist = a.Name,
                        RecentPlays = a.RecentCount,
                        PriorPlays = a.PriorPlays,
                        FirstHeard = FormatDate(a.FirstScrobble),
                    }).ToList();
                    break;

                case EngagementCategory.Rediscovery:
                    view.Rediscoveries = group.Take(limit).Select(a => new RediscoveryRow
                    {
                        Artist = a.Name,
                        RecentPlays = a.RecentCount,
                        PriorPlays = a.PriorPlays,
                        Absent = FormatGap(cutoffMs - a.LastPriorMs),
                    }).ToList();
                    break;

                case EngagementCategory.LongtimeFan:
                    view.LongtimeFans = group.Take(limit).Select(a => new LongtimeFanRow
                    {
                        Artist = a.Name,
                        Recent = a.RecentCount,
                        Total = a.TotalCount,
                    }).ToList();
                    break;
            }
        }

        // Build summary line
        var parts = new List<string>();
        if (view.NewDiscoveries is { Count: > 0 }) parts.Add($"{view.NewDiscoveries.Count} new discovery");
        if (view.FirstClicks is { Count: > 0 }) parts.Add($"{view.FirstClicks.Count} first click");
        if (view.Rediscoveries is { Count: > 0 }) parts.Add($"{view.Rediscoveries.Count} rediscovery");
        if (view.LongtimeFans is { Count: > 0 }) parts.Add($"{view.LongtimeFans.Count} longtime fan");
        view.Summary = parts.Count > 0 ? string.Join(" | ", parts) : "No engagement categories detected.";

        // Serialize
        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
    }

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
