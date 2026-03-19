using BeatTrack.Core.Views;
using Markout;

namespace BeatTrack.Core.Queries;

/// <summary>
/// Recent momentum report — what's heating up, what's new, what's fading.
/// Composes surge, discovery, depth, and velocity signals into short interpreted output.
/// </summary>
public static class MomentumQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var window = ParseStringFlag(args, "--window") ?? "7d";
        var limit = ParseIntFlag(args, "--limit") ?? 5;

        var timed = scrobbles.Where(static s => s.TimestampMs > 0).ToList();
        if (timed.Count == 0)
        {
            Console.WriteLine("(no scrobbles with timestamps found)");
            return 0;
        }

        var nowMs = timed.Max(static s => s.TimestampMs);
        var windowMs = ParseWindowMs(window);
        var recentCutoff = nowMs - windowMs;
        var comparisonCutoff = nowMs - windowMs * 4;

        var recent = timed.Where(s => s.TimestampMs >= recentCutoff).ToList();
        var comparison = timed.Where(s => s.TimestampMs >= comparisonCutoff && s.TimestampMs < recentCutoff).ToList();

        if (recent.Count == 0)
        {
            Console.WriteLine($"(no scrobbles in the last {window})");
            return 0;
        }

        // Build artist aggregates for recent and comparison periods
        var recentArtists = GroupArtists(recent);
        var comparisonArtists = GroupArtists(comparison);
        var allTimeArtists = GroupArtists(timed);

        // Track-level aggregates for recent period
        var recentTracks = recent
            .Where(static s => !string.IsNullOrWhiteSpace(s.Track))
            .GroupBy(s => (
                Artist: BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName),
                Track: s.Track!.ToLowerInvariant()))
            .Select(g => new TrackCount(
                g.First().ArtistName,
                g.First().Track!,
                g.Count()))
            .OrderByDescending(static t => t.Count)
            .ToList();

        // All-time depth per artist (unique tracks)
        var depthLookup = timed
            .Where(static s => !string.IsNullOrWhiteSpace(s.Track))
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static g => g.Key,
                static g => g.Select(static s => s.Track!.ToLowerInvariant()).Distinct().Count(),
                StringComparer.OrdinalIgnoreCase);

        var windowLabel = WindowLabel(window);

        // --- Build view model ---
        var view = new MomentumView
        {
            Title = $"Momentum ({window})",
            Scrobbles = recent.Count,
            Window = window,
        };

        // Heating up: recent rate >> comparison rate
        var heatingUp = recentArtists
            .Select(r =>
            {
                if (!comparisonArtists.TryGetValue(r.Key, out var comp)) return null;
                var compRate = comp.Count / 3.0;
                var recentRate = (double)r.Value.Count;
                if (recentRate <= compRate) return null;
                var surge = compRate > 0 ? recentRate / compRate : recentRate;
                depthLookup.TryGetValue(r.Key, out var depth);
                if (surge <= 1.5) return null;
                return new HeatingUpRow
                {
                    Name = r.Value.DisplayName,
                    RecentPlays = r.Value.Count,
                    Baseline = $"~{comp.Count / 3.0:F0}/wk",
                    Depth = depth > 15 ? $"{depth} tracks" : depth > 0 ? $"{depth} tracks" : null,
                };
            })
            .OfType<HeatingUpRow>()
            .Take(limit)
            .ToList();

        if (heatingUp.Count > 0) view.HeatingUp = heatingUp;

        // On repeat: tracks played 3+ times in the window
        var onRepeat = recentTracks
            .Where(static t => t.Count >= 3)
            .Take(limit)
            .Select(t => new OnRepeatRow
            {
                Track = t.Track,
                Artist = t.Artist,
                Plays = $"{t.Count}x",
            })
            .ToList();

        if (onRepeat.Count > 0) view.OnRepeat = onRepeat;

        // New to you: zero plays before the window
        var newToYou = recentArtists
            .Where(r =>
            {
                if (!allTimeArtists.TryGetValue(r.Key, out var allTime)) return false;
                var priorPlays = allTime.Count - r.Value.Count;
                return priorPlays == 0 && r.Value.Count >= 2;
            })
            .Select(r => new NewToYouRow
            {
                Artist = r.Value.DisplayName,
                Plays = r.Value.Count,
                FirstListen = FormatDate(FindFirstDate(recent, r.Key)),
            })
            .OrderByDescending(static a => a.Plays)
            .Take(limit)
            .ToList();

        if (newToYou.Count > 0) view.NewToYou = newToYou;

        // Comeback: dormant for 90+ days, now active
        var comebackCutoff = recentCutoff - 90L * 24 * 60 * 60 * 1000;
        var comeback = recentArtists
            .Select(r =>
            {
                var priorScrobbles = timed
                    .Where(s => s.TimestampMs < recentCutoff &&
                                BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName)
                                    .Equals(r.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (priorScrobbles.Count < 3) return null;
                if (r.Value.Count < 2) return null;

                var lastPriorMs = priorScrobbles.Max(static s => s.TimestampMs);
                if (lastPriorMs > comebackCutoff) return null;

                var gapDays = (int)((recentCutoff - lastPriorMs) / (24.0 * 60 * 60 * 1000));
                return new ComebackRow
                {
                    Artist = r.Value.DisplayName,
                    RecentPlays = r.Value.Count,
                    Gap = FormatGap(gapDays),
                    PriorPlays = priorScrobbles.Count,
                };
            })
            .OfType<ComebackRow>()
            .OrderByDescending(static a => a.Gap)
            .Take(limit)
            .ToList();

        if (comeback.Count > 0) view.Comeback = comeback;

        // Cooling off: was active in comparison, dropped in recent
        var coolingOff = comparisonArtists
            .Select(c =>
            {
                recentArtists.TryGetValue(c.Key, out var rec);
                var recentCount = rec?.Count ?? 0;
                var compRate = c.Value.Count / 3.0;
                if (compRate < 2) return null;
                if (recentCount >= compRate * 0.5) return null;
                return new CoolingOffRow
                {
                    Artist = c.Value.DisplayName,
                    RecentActivity = recentCount == 0 ? "quiet" : $"{recentCount} {Plural(recentCount, "play")}",
                    Baseline = $"~{compRate:F0}/wk",
                };
            })
            .OfType<CoolingOffRow>()
            .Take(limit)
            .ToList();

        if (coolingOff.Count > 0) view.CoolingOff = coolingOff;

        // Build summary line
        var parts = new List<string>();
        if (view.HeatingUp is { Count: > 0 }) parts.Add($"{view.HeatingUp.Count} heating up");
        if (view.OnRepeat is { Count: > 0 }) parts.Add($"{view.OnRepeat.Count} on repeat");
        if (view.NewToYou is { Count: > 0 }) parts.Add($"{view.NewToYou.Count} new");
        if (view.Comeback is { Count: > 0 }) parts.Add($"{view.Comeback.Count} comeback");
        if (view.CoolingOff is { Count: > 0 }) parts.Add($"{view.CoolingOff.Count} cooling off");
        view.Summary = parts.Count > 0 ? string.Join(" | ", parts) : "Steady state — no significant momentum shifts detected.";

        // Serialize
        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
    }

    private static Dictionary<string, ArtistAggregate> GroupArtists(IReadOnlyList<LastFmScrobble> scrobbles)
    {
        return scrobbles
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static g => g.Key,
                static g => new ArtistAggregate(g.First().ArtistName, g.Count()),
                StringComparer.OrdinalIgnoreCase);
    }

    private static long FindFirstDate(IReadOnlyList<LastFmScrobble> scrobbles, string canonical)
    {
        return scrobbles
            .Where(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName)
                .Equals(canonical, StringComparison.OrdinalIgnoreCase))
            .Min(static s => s.TimestampMs);
    }

    private static string WindowLabel(string window)
    {
        var lower = window.ToLowerInvariant().Trim();
        if (lower == "7d") return "week";
        if (lower == "30d") return "month";
        if (lower == "14d") return "fortnight";
        return window;
    }

    private static long ParseWindowMs(string window)
    {
        var lower = window.ToLowerInvariant().Trim();
        if (lower.EndsWith('d') && int.TryParse(lower[..^1], out var days))
            return days * 24L * 60 * 60 * 1000;
        if (lower.EndsWith('m') && int.TryParse(lower[..^1], out var months))
            return months * 30L * 24 * 60 * 60 * 1000;
        return 7L * 24 * 60 * 60 * 1000; // default 7d
    }

    private static string Plural(int count, string word) =>
        count == 1 ? word : word + "s";

    private static string FormatDate(long timestampMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).ToLocalTime().ToString("yyyy-MM-dd");

    private static string FormatGap(int days)
    {
        if (days >= 365) return $"{days / 365}y {(days % 365) / 30}mo";
        if (days >= 30) return $"{days / 30}mo";
        return $"{days}d";
    }

    private static string? ParseStringFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static int? ParseIntFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var value))
                return value;
        }
        return null;
    }

    private sealed record ArtistAggregate(string DisplayName, int Count);
    private sealed record TrackCount(string Artist, string Track, int Count);
}
