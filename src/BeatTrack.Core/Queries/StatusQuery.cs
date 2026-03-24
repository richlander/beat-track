using BeatTrack.Core.Views;
using Markout;

namespace BeatTrack.Core.Queries;

/// <summary>
/// Shows data availability, freshness, and configuration status.
/// Helps the user understand what data they have, how old it is,
/// and what they need to do to refresh or set up missing sources.
/// </summary>
public static class StatusQuery
{
    public static int Run(string dataDir, string cacheDir, string configFile, string[] searchDirs)
    {
        var view = new StatusView();

        // Config
        var config = new BeatTrackConfig(configFile);
        view.Config =
        [
            new() { Name = "config_file", Value = $"{configFile}{(File.Exists(configFile) ? "" : " (not found)")}" },
            new() { Name = "lastfm_api_key", Value = config.LastFmApiKey is not null ? "set" : "not set" },
            new() { Name = "lastfm_user", Value = config.LastFmUser ?? "(not set)" },
        ];

        // Directories
        view.Directories =
        [
            new() { Name = "data_dir", Value = $"{dataDir}{(Directory.Exists(dataDir) ? "" : " (not created)")}" },
            new() { Name = "cache_dir", Value = $"{cacheDir}{(Directory.Exists(cacheDir) ? "" : " (not created)")}" },
            new() { Name = "search_dirs", Value = string.Join(", ", searchDirs) },
        ];

        // Data sources
        var dataSources = new List<StatusItemRow>();

        var scrobbleFile = FindFile("scrobbles-*.jsonl", searchDirs.Select(d => Path.Combine(d, "lastfmstats")).ToArray());
        dataSources.Add(new() { Name = "lastfm_scrobbles", Value = FormatSource(scrobbleFile) });

        var snapshot = FindFile("*-snapshot.json", searchDirs);
        dataSources.Add(new() { Name = "lastfm_snapshot", Value = FormatSource(snapshot) });

        var discogs = FindFile("*-collection-*.csv", searchDirs.Select(d => Path.Combine(d, "collection-csv")).ToArray());
        dataSources.Add(new() { Name = "discogs_collection", Value = FormatSource(discogs) });

        var takeoutDirs = searchDirs.Select(d => Path.Combine(d, "takeout", "extracted", "Takeout", "YouTube and YouTube Music", "history")).ToArray();
        var youtube = FindFile("watch-history.html", takeoutDirs);
        dataSources.Add(new() { Name = "youtube_takeout", Value = FormatSource(youtube) });

        var misses = FindExact(Path.Combine(dataDir, "known-misses.md"));
        dataSources.Add(new() { Name = "known_misses", Value = FormatSource(misses) });

        var favorites = FindExact(Path.Combine(dataDir, "my-favorites.md"));
        dataSources.Add(new() { Name = "my_favorites", Value = FormatSource(favorites) });

        var userSimilar = FindExact(Path.Combine(dataDir, "my-similar-artists.md"));
        dataSources.Add(new() { Name = "my_similar_artists", Value = FormatSource(userSimilar) });

        view.DataSources = dataSources;

        // Cache
        var cacheItems = new List<StatusItemRow>();

        var mbidCache = FindExact(Path.Combine(cacheDir, "mbid-cache.md"));
        cacheItems.Add(new() { Name = "mbid_cache", Value = FormatSource(mbidCache) });

        var similarDir = Path.Combine(cacheDir, "similar-artists");
        if (Directory.Exists(similarDir))
        {
            var count = Directory.EnumerateFiles(similarDir, "*.md").Count();
            cacheItems.Add(new() { Name = "similar_artists", Value = $"{count} cached" });
        }
        else
        {
            cacheItems.Add(new() { Name = "similar_artists", Value = "(none)" });
        }

        view.Cache = cacheItems;

        // Suggestions
        var suggestions = new List<SuggestionRow>();
        if (scrobbleFile is null)
            suggestions.Add(new() { Action = "Run 'beat-track pull' to fetch scrobble history from Last.fm" });
        if (config.LastFmApiKey is null && Environment.GetEnvironmentVariable("LASTFM_API_KEY") is null)
            suggestions.Add(new() { Action = $"Add lastfm_api_key to {configFile} for snapshot fetching and live feed" });
        if (scrobbleFile is not null && snapshot is null && (config.LastFmApiKey is not null || Environment.GetEnvironmentVariable("LASTFM_API_KEY") is not null))
            suggestions.Add(new() { Action = "Run the Last.fm snapshot tool to enable full analysis with MBIDs" });
        if (scrobbleFile is not null)
        {
            var age = DateTime.Now - File.GetLastWriteTime(scrobbleFile);
            if (age.TotalDays > 30)
                suggestions.Add(new() { Action = $"Scrobble data is {(int)age.TotalDays} days old — run 'beat-track pull' to update" });
        }
        if (snapshot is not null)
        {
            var age = DateTime.Now - File.GetLastWriteTime(snapshot);
            if (age.TotalDays > 14)
                suggestions.Add(new() { Action = $"Snapshot is {(int)age.TotalDays} days old — consider re-fetching" });
        }

        if (suggestions.Count > 0)
            view.Suggestions = suggestions;

        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
    }

    private static string? FindFile(string pattern, string[] dirs)
    {
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            var match = Directory.EnumerateFiles(dir, pattern).FirstOrDefault();
            if (match is not null) return match;
        }
        return null;
    }

    private static string? FindExact(string path) => File.Exists(path) ? path : null;

    private static string FormatSource(string? path)
    {
        if (path is null)
            return "(not found)";

        var info = new FileInfo(path);
        var age = DateTime.Now - info.LastWriteTime;
        var ageStr = age.TotalDays switch
        {
            < 1 => "today",
            < 2 => "yesterday",
            < 30 => $"{(int)age.TotalDays} days ago",
            < 365 => $"{(int)(age.TotalDays / 30)} months ago",
            _ => $"{age.TotalDays / 365:F1} years ago"
        };

        var sizeStr = info.Length switch
        {
            < 1024 => $"{info.Length} B",
            < 1024 * 1024 => $"{info.Length / 1024.0:F0} KB",
            _ => $"{info.Length / (1024.0 * 1024.0):F1} MB"
        };

        return $"{Path.GetFileName(path)} ({sizeStr}, {ageStr})";
    }
}
