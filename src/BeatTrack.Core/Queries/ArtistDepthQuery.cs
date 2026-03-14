using System.Text.RegularExpressions;

namespace BeatTrack.Core.Queries;

/// <summary>
/// Analyzes how deeply the user engages with each artist — catalog explorer vs one-hit wonder.
/// Shows unique tracks, albums, top track concentration, and period spread.
/// In shallow mode, detects covers/remixes and generates differentiated recommendations.
/// </summary>
public static partial class ArtistDepthQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var window = ParseStringFlag(args, "--window") ?? "all";
        var limit = ParseIntFlag(args, "--limit") ?? 50;
        var minPlays = ParseIntFlag(args, "--min") ?? 20;
        var mode = ParseStringFlag(args, "--mode") ?? "deep"; // deep, shallow, all

        var cutoffMs = TopArtistsQuery.ParseWindowCutoff(window);
        var filtered = cutoffMs > 0
            ? scrobbles.Where(s => s.TimestampMs >= cutoffMs && s.TimestampMs > 0).ToList()
            : scrobbles.Where(static s => s.TimestampMs > 0).ToList();

        if (filtered.Count == 0)
        {
            Console.WriteLine($"(no scrobbles found in {window} window)");
            return 0;
        }

        // Build play-count lookup for all artists (used to decide recommendation direction)
        var artistPlayCounts = scrobbles
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Compute total quarters in the window for fan ratio
        var totalQuarters = GetTotalQuarters(filtered);

        var artists = filtered
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= minPlays)
            .Select(g =>
            {
                var plays = g.Count();
                var displayName = g.First().ArtistName;

                var tracks = g
                    .Where(static s => !string.IsNullOrWhiteSpace(s.Track))
                    .GroupBy(static s => s.Track!.ToLowerInvariant())
                    .OrderByDescending(static t => t.Count())
                    .ToList();

                var uniqueTracks = tracks.Count;
                var topTrackPlays = tracks.Count > 0 ? tracks[0].Count() : 0;
                var topTrackName = tracks.Count > 0 ? tracks[0].First().Track : null;
                var topTrackShare = plays > 0 ? (double)topTrackPlays / plays : 0;

                var albums = g
                    .Where(static s => !string.IsNullOrWhiteSpace(s.Album))
                    .Select(static s => s.Album!.ToLowerInvariant())
                    .Distinct()
                    .Count();

                // Period spread: distinct months with activity
                var periods = g
                    .Select(static s => DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToString("yyyy-MM"))
                    .Distinct()
                    .Count();

                // Quarter spread: distinct quarters with activity
                var quarters = g
                    .Select(static s =>
                    {
                        var dt = DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs);
                        return $"{dt.Year}-Q{(dt.Month - 1) / 3 + 1}";
                    })
                    .Distinct()
                    .Count();

                var fanRatio = totalQuarters > 0 ? (double)quarters / totalQuarters : 0;

                return new ArtistDepth(displayName, g.Key, plays, uniqueTracks, albums, periods,
                    topTrackPlays, topTrackName, topTrackShare, quarters, fanRatio);
            })
            .ToList();

        // Sort based on mode
        var sorted = mode.ToLowerInvariant() switch
        {
            "shallow" => artists.OrderByDescending(static a => a.TopTrackShare)
                .ThenByDescending(static a => a.Plays).ToList(),
            "all" => artists.OrderByDescending(static a => a.Plays).ToList(),
            _ => artists.OrderByDescending(static a => a.UniqueTracks)
                .ThenByDescending(static a => a.Periods).ToList(), // deep
        };

        var label = mode.ToLowerInvariant() switch
        {
            "shallow" => "one-hit wonders — high plays, few tracks",
            "all" => "all artists by plays",
            _ => "deep cuts — catalog explorers",
        };

        Console.WriteLine($"artist_depth ({window}, min {minPlays} plays, {label}):");
        Console.WriteLine();
        Console.WriteLine($"  {"Artist",-30} {"Plays",6} {"Tracks",7} {"Albums",7} {"Months",7} {"Top%",5}  Top track");
        Console.WriteLine($"  {"------------------------------",-30} {"------",6} {"-------",7} {"-------",7} {"-------",7} {"-----",5}  ---------");

        foreach (var a in sorted.Take(limit))
        {
            var topTrack = a.TopTrackName ?? "";
            if (topTrack.Length > 30) topTrack = topTrack[..27] + "...";
            Console.WriteLine($"  {Truncate(a.DisplayName, 30),-30} {a.Plays,6:N0} {a.UniqueTracks,7} {a.Albums,7} {a.Periods,7} {a.TopTrackShare,5:P0}  {topTrack}");
        }

        // In shallow mode, generate recommendations
        if (mode.Equals("shallow", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine("recommendations:");
            Console.WriteLine();

            foreach (var a in sorted.Take(limit))
            {
                if (a.TopTrackName is null) continue;

                var coverArtist = DetectCoverArtist(a.TopTrackName);
                var remixArtist = DetectRemixArtist(a.TopTrackName);
                var detectedArtist = coverArtist ?? remixArtist;
                var relationKind = coverArtist is not null ? "cover" : "remix";

                if (detectedArtist is not null)
                {
                    var detectedCanonical = BeatTrackAnalysis.CanonicalizeArtistName(detectedArtist);
                    artistPlayCounts.TryGetValue(detectedCanonical, out var detectedPlays);

                    if (detectedPlays >= minPlays)
                    {
                        // You already love the detected artist — recommend the covering/remixing artist instead
                        Console.WriteLine($"  {a.DisplayName} — {relationKind} of {detectedArtist} ({detectedPlays:N0} plays) → explore {a.DisplayName}'s original work");
                    }
                    else if (detectedPlays > 0)
                    {
                        Console.WriteLine($"  {a.DisplayName} — \"{Truncate(a.TopTrackName, 40)}\" is a {relationKind} → explore more of {detectedArtist}");
                    }
                    else
                    {
                        Console.WriteLine($"  {a.DisplayName} — \"{Truncate(a.TopTrackName, 40)}\" is a {relationKind} → check out {detectedArtist}");
                    }
                }
                else if (a.UniqueTracks <= 2 && a.TopTrackShare >= 0.5)
                {
                    Console.WriteLine($"  {a.DisplayName} — you love \"{Truncate(a.TopTrackName, 40)}\" → explore their catalog");
                }
            }
        }

        // In deep mode, highlight the strongest artist relationships
        if (mode.Equals("deep", StringComparison.OrdinalIgnoreCase))
        {
            // Scale thresholds to the window size
            var windowMonths = EstimateWindowMonths(window, filtered);
            var minTracks = Math.Max(5, Math.Min(20, windowMonths));
            var minPeriods = Math.Max(2, Math.Min(6, windowMonths / 2));

            Console.WriteLine();
            Console.WriteLine($"true_love_artists ({window}, min {minTracks} tracks, {minPeriods}+ months, <15% top track):");
            Console.WriteLine();

            var trueLove = artists
                .Where(a => a.UniqueTracks >= minTracks && a.Periods >= minPeriods && a.TopTrackShare < 0.15)
                .OrderByDescending(static a => a.UniqueTracks * a.Periods)
                .Take(20)
                .ToList();

            foreach (var a in trueLove)
            {
                Console.WriteLine($"  {a.DisplayName}  ({a.UniqueTracks} tracks, {a.Albums} albums, {a.Periods} months, top track only {a.TopTrackShare:P0})");
            }

            Console.WriteLine();
            Console.WriteLine("Use these as seeds for similar-artist recommendations — they represent deep taste, not passing interest.");

            // Fan tiers by quarter consistency
            Console.WriteLine();
            Console.WriteLine($"fan_tiers ({window}, {totalQuarters} quarters):");

            var staples = artists.Where(static a => a.FanRatio >= 0.75)
                .OrderByDescending(static a => a.FanRatio).ThenByDescending(static a => a.Quarters).ToList();
            var regulars = artists.Where(static a => a.FanRatio >= 0.50 && a.FanRatio < 0.75)
                .OrderByDescending(static a => a.FanRatio).ThenByDescending(static a => a.Quarters).ToList();
            var rotation = artists.Where(static a => a.FanRatio >= 0.25 && a.FanRatio < 0.50)
                .OrderByDescending(static a => a.FanRatio).ThenByDescending(static a => a.Quarters).ToList();

            PrintFanTier("staple", ">=75% of quarters — always in rotation", staples, totalQuarters);
            PrintFanTier("regular", ">=50% of quarters — shows up more than not", regulars, totalQuarters);
            PrintFanTier("rotation", ">=25% of quarters — cycles in and out", rotation, totalQuarters);
        }

        return 0;
    }

    /// <summary>
    /// Detects if a track name indicates a cover and extracts the original artist.
    /// Patterns: "(Artist Cover)", "(Artist cover)", "Artist Cover"
    /// </summary>
    internal static string? DetectCoverArtist(string trackName)
    {
        // "(Daft Punk cover)" or "(Neutral Milk Hotel Cover)"
        var match = CoverPattern().Match(trackName);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // "Get Me Away From Here I'm Dying (Belle and Sebastian cover)"
        // Already caught above. Check for bare "Cover" suffix without parens
        // but only if preceded by a dash: "Song - Artist Cover"
        match = DashCoverPattern().Match(trackName);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Detects if a track name indicates a remix and extracts the remixer or original.
    /// Patterns: "(Artist Remix)", "(Artist Mix)", "(Artist Edit)"
    /// </summary>
    internal static string? DetectRemixArtist(string trackName)
    {
        var match = RemixPattern().Match(trackName);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    [GeneratedRegex(@"\((.+?)\s+[Cc]over\)", RegexOptions.Compiled)]
    private static partial Regex CoverPattern();

    [GeneratedRegex(@" - (.+?)\s+[Cc]over$", RegexOptions.Compiled)]
    private static partial Regex DashCoverPattern();

    [GeneratedRegex(@"\((.+?)\s+(?:[Rr]emix|[Mm]ix|[Ee]dit)\)", RegexOptions.Compiled)]
    private static partial Regex RemixPattern();

    private static void PrintFanTier(string tierName, string description, List<ArtistDepth> artists, int totalQuarters)
    {
        Console.WriteLine();
        Console.WriteLine($"  {tierName} ({description}):");
        if (artists.Count == 0)
        {
            Console.WriteLine("    (none)");
            return;
        }

        foreach (var a in artists.Take(20))
        {
            Console.WriteLine($"    {a.DisplayName}  ({a.Quarters}/{totalQuarters} quarters = {a.FanRatio:P0}, {a.Plays:N0} plays)");
        }

        if (artists.Count > 20)
        {
            Console.WriteLine($"    ... and {artists.Count - 20} more");
        }
    }

    private static int GetTotalQuarters(IReadOnlyList<LastFmScrobble> scrobbles)
    {
        if (scrobbles.Count == 0) return 0;
        var earliest = scrobbles.Min(static s => s.TimestampMs);
        var latest = scrobbles.Max(static s => s.TimestampMs);
        var days = (latest - earliest) / (24.0 * 60 * 60 * 1000);
        return Math.Max(1, (int)Math.Round(days / 91.25)); // 365.25 / 4
    }

    private static int EstimateWindowMonths(string window, IReadOnlyList<LastFmScrobble> filtered)
    {
        if (window.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (filtered.Count == 0) return 12;
            var earliest = filtered.Min(static s => s.TimestampMs);
            var latest = filtered.Max(static s => s.TimestampMs);
            return Math.Max(1, (int)((latest - earliest) / (30.0 * 24 * 60 * 60 * 1000)));
        }

        var lower = window.ToLowerInvariant().Trim();
        if (lower.EndsWith('d') && int.TryParse(lower[..^1], out var days))
            return Math.Max(1, days / 30);
        if (lower.EndsWith('m') && int.TryParse(lower[..^1], out var months))
            return months;
        if (lower.EndsWith('y') && int.TryParse(lower[..^1], out var years))
            return years * 12;
        return 12;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
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

public record ArtistDepth(
    string DisplayName,
    string Canonical,
    int Plays,
    int UniqueTracks,
    int Albums,
    int Periods,
    int TopTrackPlays,
    string? TopTrackName,
    double TopTrackShare,
    int Quarters,
    double FanRatio);
