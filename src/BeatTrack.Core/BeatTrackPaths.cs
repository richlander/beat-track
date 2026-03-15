namespace BeatTrack.Core;

/// <summary>
/// Resolves data and cache directories using XDG Base Directory conventions.
/// Falls back to legacy ~/.beattrack/ paths if the XDG locations don't exist yet.
/// </summary>
public static class BeatTrackPaths
{
    private const string AppName = "beat-track";
    private const string LegacyDotDir = ".beattrack";

    /// <summary>
    /// Persistent user data (scrobble CSVs, snapshots, known-misses, etc.).
    /// Resolution order: BEAT_TRACK_DATA_DIR → XDG_DATA_HOME/beat-track → ~/.local/share/beat-track → ~/.beattrack/data
    /// </summary>
    public static string DataDir { get; } = ResolveDataDir();

    /// <summary>
    /// Regenerable cache data (MBID cache, similar-artists).
    /// Resolution order: BEAT_TRACK_CACHE_DIR → XDG_CACHE_HOME/beat-track → ~/.cache/beat-track → ~/.beattrack/cache
    /// </summary>
    public static string CacheDir { get; } = ResolveCacheDir();

    private static string ResolveDataDir()
    {
        // Explicit env var override
        var envOverride = Environment.GetEnvironmentVariable("BEAT_TRACK_DATA_DIR");
        if (envOverride is not null)
            return envOverride;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // XDG path
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(home, ".local", "share");
        var xdgPath = Path.Combine(xdgDataHome, AppName);

        // Legacy path
        var legacyPath = Path.Combine(home, LegacyDotDir, "data");

        // Prefer XDG if it exists, otherwise fall back to legacy if it exists, otherwise default to XDG
        if (Directory.Exists(xdgPath))
            return xdgPath;
        if (Directory.Exists(legacyPath))
            return legacyPath;
        return xdgPath;
    }

    private static string ResolveCacheDir()
    {
        // Explicit env var override
        var envOverride = Environment.GetEnvironmentVariable("BEAT_TRACK_CACHE_DIR");
        if (envOverride is not null)
            return envOverride;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // XDG path
        var xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
            ?? Path.Combine(home, ".cache");
        var xdgPath = Path.Combine(xdgCacheHome, AppName);

        // Legacy path
        var legacyPath = Path.Combine(home, LegacyDotDir, "cache");

        // Prefer XDG if it exists, otherwise fall back to legacy if it exists, otherwise default to XDG
        if (Directory.Exists(xdgPath))
            return xdgPath;
        if (Directory.Exists(legacyPath))
            return legacyPath;
        return xdgPath;
    }
}
