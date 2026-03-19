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

        // --- Heating up: recent rate >> comparison rate ---
        var heatingUp = recentArtists
            .Select(r =>
            {
                if (!comparisonArtists.TryGetValue(r.Key, out var comp)) return null;
                var compRate = comp.Count / 3.0; // comparison is 3x the window (excluding recent)
                var recentRate = (double)r.Value.Count;
                // Only "heating up" if they had some prior presence and rate increased
                if (recentRate <= compRate) return null;
                var surge = compRate > 0 ? recentRate / compRate : recentRate;
                depthLookup.TryGetValue(r.Key, out var depth);
                return new MomentumArtist(r.Value.DisplayName, r.Key, r.Value.Count, comp.Count, surge, depth);
            })
            .OfType<MomentumArtist>()
            .Where(static a => a.Surge > 1.5) // at least 50% above baseline
            .OrderByDescending(static a => a.Surge)
            .Take(limit)
            .ToList();

        // --- New to you: zero plays before the window ---
        var newToYou = recentArtists
            .Where(r =>
            {
                if (!allTimeArtists.TryGetValue(r.Key, out var allTime)) return false;
                var priorPlays = allTime.Count - r.Value.Count;
                return priorPlays == 0 && r.Value.Count >= 2;
            })
            .Select(r => new NewArtist(r.Value.DisplayName, r.Value.Count, FindFirstDate(recent, r.Key)))
            .OrderByDescending(static a => a.Count)
            .Take(limit)
            .ToList();

        // --- Comeback: dormant for 90+ days, now active ---
        var comebackCutoff = recentCutoff - 90L * 24 * 60 * 60 * 1000;
        var comeback = recentArtists
            .Select(r =>
            {
                // Find the most recent scrobble before the current window
                var priorScrobbles = timed
                    .Where(s => s.TimestampMs < recentCutoff &&
                                BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName)
                                    .Equals(r.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (priorScrobbles.Count < 3) return null; // need meaningful prior engagement
                if (r.Value.Count < 2) return null; // need more than a single accidental play

                var lastPriorMs = priorScrobbles.Max(static s => s.TimestampMs);
                if (lastPriorMs > comebackCutoff) return null; // wasn't dormant

                var gapDays = (int)((recentCutoff - lastPriorMs) / (24.0 * 60 * 60 * 1000));
                return new ComebackArtist(r.Value.DisplayName, r.Value.Count, priorScrobbles.Count, gapDays);
            })
            .OfType<ComebackArtist>()
            .OrderByDescending(static a => a.GapDays)
            .Take(limit)
            .ToList();

        // --- Cooling off: was active in comparison, dropped in recent ---
        var coolingOff = comparisonArtists
            .Select(c =>
            {
                recentArtists.TryGetValue(c.Key, out var rec);
                var recentCount = rec?.Count ?? 0;
                var compRate = c.Value.Count / 3.0;
                if (compRate < 2) return null; // wasn't meaningfully active
                if (recentCount >= compRate * 0.5) return null; // hasn't really dropped
                return new CoolingArtist(c.Value.DisplayName, recentCount, c.Value.Count, compRate);
            })
            .OfType<CoolingArtist>()
            .OrderByDescending(static a => a.ComparisonRate - a.RecentCount)
            .Take(limit)
            .ToList();

        // --- On repeat: tracks played 3+ times in the window ---
        var onRepeat = recentTracks
            .Where(static t => t.Count >= 3)
            .Take(limit)
            .ToList();

        // --- Output ---
        Console.WriteLine($"momentum ({window}):");
        Console.WriteLine();

        if (heatingUp.Count > 0)
        {
            Console.WriteLine("  heating up:");
            foreach (var a in heatingUp)
            {
                var depthNote = a.Depth > 15 ? $"deep catalog ({a.Depth} tracks)" : a.Depth > 0 ? $"{a.Depth} tracks explored" : "";
                var surgeNote = $"{a.RecentCount} plays this {WindowLabel(window)}, was ~{a.ComparisonCount / 3.0:F0}/wk";
                Console.WriteLine($"    {a.DisplayName} — {surgeNote}. {depthNote}".TrimEnd());
            }
            Console.WriteLine();
        }

        if (onRepeat.Count > 0)
        {
            Console.WriteLine("  on repeat:");
            foreach (var t in onRepeat)
            {
                Console.WriteLine($"    \"{t.Track}\" by {t.Artist} — {t.Count}x this {WindowLabel(window)}");
            }
            Console.WriteLine();
        }

        if (newToYou.Count > 0)
        {
            Console.WriteLine("  new to you:");
            foreach (var a in newToYou)
            {
                Console.WriteLine($"    {a.DisplayName} — {a.Count} plays, first listen {FormatDate(a.FirstScrobbleMs)}");
            }
            Console.WriteLine();
        }

        if (comeback.Count > 0)
        {
            Console.WriteLine("  comeback:");
            foreach (var a in comeback)
            {
                Console.WriteLine($"    {a.DisplayName} — {a.RecentCount} {Plural(a.RecentCount, "play")}, back after {FormatGap(a.GapDays)}. {a.PriorPlays} {Plural(a.PriorPlays, "play")} before that.");
            }
            Console.WriteLine();
        }

        if (coolingOff.Count > 0)
        {
            Console.WriteLine("  cooling off:");
            foreach (var a in coolingOff)
            {
                var recentNote = a.RecentCount == 0 ? "quiet" : $"{a.RecentCount} {Plural(a.RecentCount, "play")}";
                Console.WriteLine($"    {a.DisplayName} — {recentNote} this {WindowLabel(window)}, was ~{a.ComparisonRate:F0}/wk");
            }
            Console.WriteLine();
        }

        if (heatingUp.Count == 0 && onRepeat.Count == 0 && newToYou.Count == 0 && comeback.Count == 0 && coolingOff.Count == 0)
        {
            Console.WriteLine("  steady state — no significant momentum shifts detected.");
            Console.WriteLine();
        }

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
    private sealed record MomentumArtist(string DisplayName, string Canonical, int RecentCount, int ComparisonCount, double Surge, int Depth);
    private sealed record NewArtist(string DisplayName, int Count, long FirstScrobbleMs);
    private sealed record ComebackArtist(string DisplayName, int RecentCount, int PriorPlays, int GapDays);
    private sealed record CoolingArtist(string DisplayName, int RecentCount, int ComparisonCount, double ComparisonRate);
}
