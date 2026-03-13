using System.Globalization;
using System.Text;

namespace BeatTrack.Core;

public static class BeatTrackAnalysis
{
    public static BeatTrackSnapshotAnalysis Analyze(BeatTrackSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var recentTopArtists = snapshot.RecentTracks
            .Select(static x => x.ArtistName)
            .OfType<string>()
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .Select(static g => new BeatTrackArtistCount(g.First(), g.Count()))
            .OrderByDescending(static x => x.Count)
            .ThenBy(static x => x.ArtistName, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        var overall = snapshot.TopArtistsByPeriod.TryGetValue("Overall", out var overallArtists)
            ? overallArtists
            : [];

        var overallMap = overall
            .Where(static x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.First().Name, static g => g.First().PlayCount, StringComparer.OrdinalIgnoreCase);

        var surge = recentTopArtists
            .Select(item =>
            {
                overallMap.TryGetValue(item.ArtistName, out var overallPlayCount);
                var denominator = Math.Max(1d, Math.Log10((overallPlayCount ?? 1) + 10));
                var surgeScore = item.Count / denominator;
                return new BeatTrackArtistDelta(item.ArtistName, item.Count, overallPlayCount, surgeScore);
            })
            .OrderByDescending(static x => x.SurgeScore)
            .ThenByDescending(static x => x.RecentCount)
            .Take(20)
            .ToArray();

        var stableCore = snapshot.TopArtistsByPeriod
            .Values
            .Select(list => list.Take(15).Select(static x => x.Name).Where(static x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase))
            .Aggregate((HashSet<string>?)null, static (acc, next) =>
            {
                if (acc is null)
                {
                    return new HashSet<string>(next, StringComparer.OrdinalIgnoreCase);
                }

                acc.IntersectWith(next);
                return acc;
            })
            ?? [];

        var stableCoreArtists = stableCore
            .Select(name =>
            {
                var recentCount = recentTopArtists.FirstOrDefault(x => string.Equals(x.ArtistName, name, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
                return new BeatTrackArtistCount(name, recentCount);
            })
            .OrderByDescending(static x => x.Count)
            .ThenBy(static x => x.ArtistName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var aliasCandidates = overall
            .Where(static x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(static x => CanonicalizeArtistName(x.Name))
            .Select(static g => new BeatTrackAliasCandidate(
                CanonicalName: g.Key,
                Variants: g.Select(static x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(static x => x, StringComparer.Ordinal).ToArray(),
                VariantCount: g.Select(static x => x.Name).Distinct(StringComparer.Ordinal).Count()))
            .Where(static x => x.VariantCount > 1)
            .OrderByDescending(static x => x.VariantCount)
            .ThenBy(static x => x.CanonicalName, StringComparer.Ordinal)
            .ToArray();

        return new BeatTrackSnapshotAnalysis(recentTopArtists, surge, stableCoreArtists, aliasCandidates);
    }

    public static string CanonicalizeArtistName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var lowered = value.Trim().ToLowerInvariant();
        lowered = lowered.Replace("&", " and ", StringComparison.Ordinal);

        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        var normalized = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized;
    }
}
