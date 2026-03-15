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
        Console.WriteLine("beat-track status");
        Console.WriteLine();

        // Config
        var config = new BeatTrackConfig(configFile);
        Console.WriteLine("config:");
        Console.WriteLine($"  file: {configFile}{(File.Exists(configFile) ? "" : " (not found)")}");
        Console.WriteLine($"  lastfm_api_key: {(config.LastFmApiKey is not null ? "set" : "not set")}");
        Console.WriteLine($"  lastfm_user: {config.LastFmUser ?? "(not set)"}");
        Console.WriteLine();

        // Directories
        Console.WriteLine("directories:");
        Console.WriteLine($"  data: {dataDir}{(Directory.Exists(dataDir) ? "" : " (not created)")}");
        Console.WriteLine($"  cache: {cacheDir}{(Directory.Exists(cacheDir) ? "" : " (not created)")}");
        Console.WriteLine();

        // Data sources
        Console.WriteLine("data_sources:");

        // Scrobble CSV
        var scrobbleCsv = FindFile("lastfmstats-*.csv", searchDirs.Select(d => Path.Combine(d, "lastfmstats")).ToArray());
        PrintSource("  lastfm_scrobbles", scrobbleCsv);

        // Snapshot
        var snapshot = FindFile("*-snapshot.json", searchDirs);
        PrintSource("  lastfm_snapshot", snapshot);

        // Discogs
        var discogs = FindFile("*-collection-*.csv", searchDirs.Select(d => Path.Combine(d, "collection-csv")).ToArray());
        PrintSource("  discogs_collection", discogs);

        // YouTube takeout
        var takeoutDirs = searchDirs.Select(d => Path.Combine(d, "takeout", "extracted", "Takeout", "YouTube and YouTube Music", "history")).ToArray();
        var youtube = FindFile("watch-history.html", takeoutDirs);
        PrintSource("  youtube_takeout", youtube);

        // Known misses
        var misses = FindExact(Path.Combine(dataDir, "known-misses.md"));
        PrintSource("  known_misses", misses);

        // User-defined data
        var favorites = FindExact(Path.Combine(dataDir, "my-favorites.md"));
        PrintSource("  my_favorites", favorites);

        var userSimilar = FindExact(Path.Combine(dataDir, "my-similar-artists.md"));
        PrintSource("  my_similar_artists", userSimilar);

        Console.WriteLine();

        // Cache
        Console.WriteLine("cache:");
        var mbidCache = FindExact(Path.Combine(cacheDir, "mbid-cache.md"));
        PrintSource("  mbid_cache", mbidCache);

        var similarDir = Path.Combine(cacheDir, "similar-artists");
        if (Directory.Exists(similarDir))
        {
            var count = Directory.EnumerateFiles(similarDir, "*.md").Count();
            Console.WriteLine($"  similar_artists: {count} cached");
        }
        else
        {
            Console.WriteLine("  similar_artists: (none)");
        }

        Console.WriteLine();

        // Suggestions
        var suggestions = new List<string>();
        if (scrobbleCsv is null)
            suggestions.Add("Export scrobble history from lastfmstats.com and place in " + Path.Combine(dataDir, "lastfmstats") + "/");
        if (config.LastFmApiKey is null && Environment.GetEnvironmentVariable("LASTFM_API_KEY") is null)
            suggestions.Add($"Add lastfm_api_key to {configFile} for snapshot fetching and live feed");
        if (scrobbleCsv is not null && snapshot is null && (config.LastFmApiKey is not null || Environment.GetEnvironmentVariable("LASTFM_API_KEY") is not null))
            suggestions.Add("Run the Last.fm snapshot tool to enable full analysis with MBIDs");
        if (scrobbleCsv is not null)
        {
            var age = DateTime.Now - File.GetLastWriteTime(scrobbleCsv);
            if (age.TotalDays > 30)
                suggestions.Add($"Scrobble CSV is {(int)age.TotalDays} days old — consider re-exporting from lastfmstats.com");
        }
        if (snapshot is not null)
        {
            var age = DateTime.Now - File.GetLastWriteTime(snapshot);
            if (age.TotalDays > 14)
                suggestions.Add($"Snapshot is {(int)age.TotalDays} days old — consider re-fetching");
        }

        if (suggestions.Count > 0)
        {
            Console.WriteLine("suggestions:");
            foreach (var s in suggestions)
                Console.WriteLine($"  - {s}");
        }
        else
        {
            Console.WriteLine("all data sources present");
        }

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

    private static void PrintSource(string label, string? path)
    {
        if (path is null)
        {
            Console.WriteLine($"{label}: (not found)");
            return;
        }

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

        Console.WriteLine($"{label}: {Path.GetFileName(path)} ({sizeStr}, {ageStr})");
    }
}
