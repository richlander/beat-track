using BeatTrack.Core.Views;
using Markout;

namespace BeatTrack.Core.Queries;

/// <summary>
/// Measures how thoroughly the listener has explored the similarity cluster
/// around an artist. Loads the cached similar-artists graph and cross-references
/// against scrobble history to produce an exploration percentage and surface
/// the highest-value unexplored neighbors.
/// </summary>
public static class ClusterExplorationQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var artistName = ParseStringFlag(args, "--artist");
        var top = ParseIntFlag(args, "--top") ?? 10;
        var minScore = ParseIntFlag(args, "--min-score") ?? 100;

        var cacheDir = BeatTrackPaths.CacheDir;

        var mbidCachePath = Path.Combine(cacheDir, "mbid-cache.md");
        var similarCacheDir = Path.Combine(cacheDir, "similar-artists");

        if (!File.Exists(mbidCachePath) || !Directory.Exists(similarCacheDir))
        {
            Console.Error.WriteLine("(no caches found — run the full analysis first to populate MBID and similar artist caches)");
            return 1;
        }

        var mbidCache = new MbidCache(mbidCachePath);
        var shelfItems = new Shelf.Core.Items.ShelfItems(Shelf.Core.ShelfPaths.ItemsDir);
        var shelfRels = new Shelf.Core.Relationships.ShelfRelationships(Shelf.Core.ShelfPaths.RelationshipsDir);
        var userSimilar = new UserSimilarArtists(shelfItems, shelfRels);

        // Build canonical name → (display name, total plays) from all scrobbles
        var artistPlays = scrobbles
            .Where(static s => s.TimestampMs > 0)
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static g => g.Key,
                static g => (Name: g.First().ArtistName, Count: g.Count()),
                StringComparer.OrdinalIgnoreCase);

        var listenedCanonical = new HashSet<string>(artistPlays.Keys, StringComparer.OrdinalIgnoreCase);

        // Determine seed artists
        List<(string Canonical, string DisplayName, int Plays, string Mbid)> seeds;

        if (artistName is not null)
        {
            var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artistName);
            var mbid = mbidCache.GetMbid(canonical);
            if (mbid is null)
            {
                Console.Error.WriteLine($"(no MBID cached for '{artistName}' — run full analysis to populate)");
                return 1;
            }

            var cacheFile = Path.Combine(similarCacheDir, $"{mbid}.md");
            if (!File.Exists(cacheFile))
            {
                Console.Error.WriteLine($"(no similar artists cached for '{artistName}' — run full analysis to populate)");
                return 1;
            }

            var plays = artistPlays.TryGetValue(canonical, out var p) ? p.Count : 0;
            seeds = [(canonical, artistName, plays, mbid)];
        }
        else
        {
            // Top N artists by play count, filtered to those with cached similar artists
            seeds = artistPlays
                .OrderByDescending(static kvp => kvp.Value.Count)
                .Select(kvp =>
                {
                    var mbid = mbidCache.GetMbid(kvp.Key);
                    if (mbid is null) return default;
                    var cacheFile = Path.Combine(similarCacheDir, $"{mbid}.md");
                    if (!File.Exists(cacheFile)) return default;
                    return (kvp.Key, kvp.Value.Name, kvp.Value.Count, mbid);
                })
                .Where(x => x.mbid is not null)
                .Take(top)
                .ToList();
        }

        if (seeds.Count == 0)
        {
            Console.Error.WriteLine("(no artists with cached similar artist data found)");
            return 0;
        }

        var results = new List<ClusterResult>();

        foreach (var (canonical, displayName, plays, mbid) in seeds)
        {
            var cacheFile = Path.Combine(similarCacheDir, $"{mbid}.md");
            var (_, rows) = MarkdownTableStore.Read(cacheFile);

            var similar = rows
                .Where(static r => r.Length >= 3)
                .Select(static r =>
                {
                    int.TryParse(r[2], out var score);
                    return (Mbid: r[0], Name: r[1], Score: score);
                })
                .Where(s => s.Score >= minScore)
                .ToList();

            // Merge user-defined similarities for this artist
            var userSims = userSimilar.GetSimilar(canonical);
            var existingNames = new HashSet<string>(similar.Select(static s => BeatTrackAnalysis.CanonicalizeArtistName(s.Name)), StringComparer.OrdinalIgnoreCase);
            foreach (var simName in userSims)
            {
                if (!existingNames.Contains(simName))
                {
                    similar.Add(("user", simName, 500));
                    existingNames.Add(simName);
                }
            }

            if (similar.Count == 0) continue;

            var explored = new List<(string Name, int Score, int Plays)>();
            var unexplored = new List<(string Name, int Score)>();

            foreach (var s in similar)
            {
                var simCanonical = BeatTrackAnalysis.CanonicalizeArtistName(s.Name);
                if (listenedCanonical.Contains(simCanonical))
                {
                    var ap = artistPlays.TryGetValue(simCanonical, out var apVal) ? apVal.Count : 0;
                    explored.Add((s.Name, s.Score, ap));
                }
                else
                {
                    unexplored.Add((s.Name, s.Score));
                }
            }

            var pct = 100.0 * explored.Count / similar.Count;
            var exploredWeight = explored.Sum(e => e.Score);
            var totalWeight = similar.Sum(static s => s.Score);
            var weightedPct = totalWeight > 0 ? 100.0 * exploredWeight / totalWeight : 0;

            results.Add(new ClusterResult(
                displayName, plays, similar.Count,
                explored.Count, pct, weightedPct,
                explored.OrderByDescending(static e => e.Plays).ToList(),
                unexplored.OrderByDescending(static u => u.Score).ToList()));
        }

        if (results.Count == 0)
        {
            Console.Error.WriteLine("(no cluster data available)");
            return 0;
        }

        // Sort: least explored first (most interesting)
        results.Sort(static (a, b) => a.ExploredPct.CompareTo(b.ExploredPct));

        var singleArtist = artistName is not null;

        var view = new ClusterExplorationView
        {
            Artists = results.Count,
            MinScore = minScore,
        };

        var clusterRows = new List<ClusterRow>();

        foreach (var r in results)
        {
            var topUnexplored = singleArtist
                ? null
                : r.Unexplored.Count > 0
                    ? string.Join(", ", r.Unexplored.Take(5).Select(static u => u.Name))
                    : null;

            clusterRows.Add(new ClusterRow
            {
                Artist = r.Name,
                Plays = r.Plays,
                ExploredLabel = $"{r.ExploredCount}/{r.TotalSimilar}",
                Coverage = $"{r.ExploredPct:F0}%",
                TopUnexplored = topUnexplored,
            });
        }

        if (clusterRows.Count > 0)
            view.Clusters = clusterRows;

        // Single-artist mode: add detail sections
        if (singleArtist && results.Count == 1)
        {
            var r = results[0];

            if (r.Unexplored.Count > 0)
            {
                view.Unexplored = r.Unexplored
                    .Select(static u => new UnexploredRow
                    {
                        Artist = u.Name,
                        Similarity = u.Score,
                    })
                    .ToList();
            }

            if (r.Explored.Count > 0)
            {
                view.Explored = r.Explored
                    .Select(static e => new ExploredRow
                    {
                        Artist = e.Name,
                        Plays = e.Plays,
                        Similarity = e.Score,
                    })
                    .ToList();
            }
        }

        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
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

    private sealed record ClusterResult(
        string Name,
        int Plays,
        int TotalSimilar,
        int ExploredCount,
        double ExploredPct,
        double WeightedPct,
        List<(string Name, int Score, int Plays)> Explored,
        List<(string Name, int Score)> Unexplored);
}
