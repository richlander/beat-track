using System.Text.Json;
using BeatTrack.App;
using BeatTrack.Core;
using BeatTrack.Discogs;
using BeatTrack.YouTube;

// --- Resolve data directories ---
// Search for data in: env var, project/data, workspace/data, ~/.beattrack/data
static string? FindFile(string? envOverride, params string[] searchPaths)
{
    if (envOverride is not null && File.Exists(envOverride)) return envOverride;
    foreach (var path in searchPaths)
    {
        if (File.Exists(path)) return path;
    }
    return null;
}

static string? FindDir(string? envOverride, params string[] searchPaths)
{
    if (envOverride is not null && Directory.Exists(envOverride)) return envOverride;
    foreach (var path in searchPaths)
    {
        if (Directory.Exists(path)) return path;
    }
    return null;
}

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var workspaceData = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "data"));
var homeData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".beattrack", "data");

// --- Load Last.fm snapshot ---
var lastFmPath = FindFile(
    Environment.GetEnvironmentVariable("BEAT_TRACK_SNAPSHOT_PATH"),
    Path.Combine(projectRoot, "data", "runfaster2000-snapshot.json"),
    Path.Combine(workspaceData, "runfaster2000-snapshot.json"),
    Path.Combine(homeData, "runfaster2000-snapshot.json"));

BeatTrackSnapshot? lastFmSnapshot = null;
if (lastFmPath is not null)
{
    await using var stream = File.OpenRead(lastFmPath);
    lastFmSnapshot = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.BeatTrackSnapshot);
    Console.WriteLine($"lastfm: {lastFmSnapshot?.RecentTracks.Count} recent tracks, {lastFmSnapshot?.TopArtistsByPeriod.Values.SelectMany(static x => x).Select(static x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()} unique top artists, {lastFmSnapshot?.LovedTracks.Count} loved tracks");
}
else
{
    Console.WriteLine("lastfm: (no snapshot found)");
}

// --- Load Discogs collection ---
var discogsCsvPath = FindFile(
    Environment.GetEnvironmentVariable("BEAT_TRACK_DISCOGS_CSV"),
    Path.Combine(workspaceData, "collection-csv", "runfaster2000-collection-20260310-2317.csv"),
    Path.Combine(homeData, "collection-csv", "runfaster2000-collection-20260310-2317.csv"));

BeatTrackDiscogsSnapshot? discogsSnapshot = null;
if (discogsCsvPath is not null)
{
    var discogsReader = new DiscogsCollectionCsvReader();
    using var csvReader = File.OpenText(discogsCsvPath);
    var releases = discogsReader.ParseCsv(csvReader);
    discogsSnapshot = new BeatTrackDiscogsSnapshot(DateTimeOffset.UtcNow, releases);
    Console.WriteLine($"discogs: {releases.Count} releases, {releases.Select(static r => r.ArtistName).Where(static n => n is not null).Distinct(StringComparer.OrdinalIgnoreCase).Count()} unique artists");
}
else
{
    Console.WriteLine("discogs: (no CSV found)");
}

// --- Load YouTube data (with classifier built from Last.fm + Discogs) ---
var takeoutDir = FindDir(
    Environment.GetEnvironmentVariable("BEAT_TRACK_TAKEOUT_DIR"),
    Path.Combine(workspaceData, "takeout"),
    Path.Combine(homeData, "takeout"))
    ?? Path.Combine(workspaceData, "takeout");

var musicLibraryPath = Path.Combine(takeoutDir, "extracted", "Takeout", "YouTube and YouTube Music", "music (library and uploads)", "music library songs.csv");
var watchHistoryPath = Path.Combine(takeoutDir, "extracted", "Takeout", "YouTube and YouTube Music", "history", "watch-history.html");

BeatTrackYouTubeSnapshot? youTubeSnapshot = null;
if (File.Exists(musicLibraryPath) && File.Exists(watchHistoryPath))
{
    // Build known artist set from Last.fm + Discogs + YouTube saved tracks
    var knownArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    if (lastFmSnapshot is not null)
    {
        foreach (var track in lastFmSnapshot.RecentTracks)
        {
            if (!string.IsNullOrWhiteSpace(track.ArtistName)) knownArtists.Add(track.ArtistName);
        }

        foreach (var (_, periodArtists) in lastFmSnapshot.TopArtistsByPeriod)
        {
            foreach (var artist in periodArtists)
            {
                if (!string.IsNullOrWhiteSpace(artist.Name)) knownArtists.Add(artist.Name);
            }
        }

        foreach (var track in lastFmSnapshot.LovedTracks)
        {
            if (!string.IsNullOrWhiteSpace(track.ArtistName)) knownArtists.Add(track.ArtistName);
        }
    }

    if (discogsSnapshot is not null)
    {
        foreach (var release in discogsSnapshot.Releases)
        {
            if (!string.IsNullOrWhiteSpace(release.ArtistName)) knownArtists.Add(release.ArtistName);
        }
    }

    var ytClient = new YouTubeTakeoutClient();
    using var musicReader = File.OpenText(musicLibraryPath);
    var savedTracks = ytClient.ParseMusicLibrarySongs(musicReader);

    foreach (var artist in savedTracks.SelectMany(static t => t.ArtistNames))
    {
        knownArtists.Add(artist);
    }

    var classifier = new MusicClassifier(knownArtists);
    var watchHtml = await File.ReadAllTextAsync(watchHistoryPath);
    var watchEvents = ytClient.ParseWatchHistoryHtml(watchHtml, classifier);

    youTubeSnapshot = new BeatTrackYouTubeSnapshot(DateTimeOffset.UtcNow, savedTracks, watchEvents);
    Console.WriteLine($"youtube: {savedTracks.Count} saved tracks, {watchEvents.Count} watch events ({watchEvents.Count(static e => e.IsMusicCandidate)} music candidates, {knownArtists.Count} known artists used)");
}
else
{
    Console.WriteLine("youtube: (no takeout data found)");
}

Console.WriteLine();

// --- Build unified profile ---
var profile = BeatTrackUnifiedProfileBuilder.Build(lastFmSnapshot, youTubeSnapshot, discogsSnapshot);

Console.WriteLine($"unified_artists: {profile.Artists.Count}");
Console.WriteLine($"unified_releases: {profile.Releases.Count}");
Console.WriteLine();

// --- Cross-source analysis ---
var multiSource = profile.Artists.Where(static a => a.Sources.Select(static s => s.Source).Distinct().Count() > 1).ToList();
var allThree = profile.Artists.Where(static a => a.Sources.Select(static s => s.Source).Distinct().Count() == 3).ToList();
var lastFmOnly = profile.Artists.Where(static a => a.Sources.All(static s => s.Source == BeatTrackSource.LastFm)).ToList();
var youTubeOnly = profile.Artists.Where(static a => a.Sources.All(static s => s.Source == BeatTrackSource.YouTube)).ToList();
var discogsOnly = profile.Artists.Where(static a => a.Sources.All(static s => s.Source == BeatTrackSource.Discogs)).ToList();

Console.WriteLine("artist_source_breakdown:");
Console.WriteLine($"  all_three_sources: {allThree.Count}");
Console.WriteLine($"  multi_source: {multiSource.Count}");
Console.WriteLine($"  lastfm_only: {lastFmOnly.Count}");
Console.WriteLine($"  youtube_only: {youTubeOnly.Count}");
Console.WriteLine($"  discogs_only: {discogsOnly.Count}");

Console.WriteLine();
Console.WriteLine("artists_in_all_three_sources:");
foreach (var artist in allThree.OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
{
    var names = artist.Sources.Select(static s => $"{s.Source}:{s.OriginalName}").Distinct();
    Console.WriteLine($"  {artist.CanonicalName} ({string.Join(", ", names)})");
}

// Discogs artists also in Last.fm (owned + listened to)
var discogsAndLastFm = profile.Artists
    .Where(static a => a.Sources.Any(static s => s.Source == BeatTrackSource.Discogs)
        && a.Sources.Any(static s => s.Source == BeatTrackSource.LastFm))
    .ToList();

Console.WriteLine();
Console.WriteLine($"discogs_and_lastfm ({discogsAndLastFm.Count} artists — owned + actively listened to):");
foreach (var artist in discogsAndLastFm.OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"  {artist.CanonicalName}");
}

// Discogs artists NOT in Last.fm or YouTube (owned but never listened to digitally)
Console.WriteLine();
Console.WriteLine($"discogs_only ({discogsOnly.Count} artists — owned but not in any listening data):");
foreach (var artist in discogsOnly.OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"  {artist.CanonicalName}");
}

// YouTube music artists NOT in Discogs (listen to but don't own)
var youTubeNotDiscogs = profile.Artists
    .Where(static a => a.Sources.Any(static s => s.Source == BeatTrackSource.YouTube)
        && !a.Sources.Any(static s => s.Source == BeatTrackSource.Discogs))
    .ToList();

Console.WriteLine();
Console.WriteLine($"youtube_not_discogs ({youTubeNotDiscogs.Count} artists — listen on YouTube but don't own):");
foreach (var artist in youTubeNotDiscogs.Take(30).OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"  {artist.CanonicalName}");
}

if (youTubeNotDiscogs.Count > 30)
{
    Console.WriteLine($"  ... and {youTubeNotDiscogs.Count - 30} more");
}

// Cross-source releases
var multiSourceReleases = profile.Releases.Where(static r => r.Sources.Select(static s => s.Source).Distinct().Count() > 1).ToList();
Console.WriteLine();
Console.WriteLine($"cross_source_releases ({multiSourceReleases.Count}):");
foreach (var release in multiSourceReleases.OrderBy(static r => r.ArtistCanonicalName).ThenBy(static r => r.Title))
{
    var sources = string.Join(", ", release.Sources.Select(static s => s.Source).Distinct());
    Console.WriteLine($"  {release.ArtistCanonicalName} - {release.Title} [{sources}]");
}

// === MBID cache + Gap analysis ===

var cacheDir = Environment.GetEnvironmentVariable("BEAT_TRACK_CACHE_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".beattrack", "cache");

var mbidCachePath = Path.Combine(cacheDir, "mbid-cache.md");
var mbidCache = new MbidCache(mbidCachePath);
Console.WriteLine($"mbid_cache: {mbidCache.Count} cached entries from {mbidCachePath}");

// Seed cache from Last.fm snapshot MBIDs
if (lastFmSnapshot is not null)
{
    var added = mbidCache.AddFromLastFmSnapshot(lastFmSnapshot);
    if (added > 0)
    {
        Console.WriteLine($"mbid_cache: added {added} from Last.fm snapshot");
    }
}

// Look up MBIDs for top unified profile artists that aren't cached yet
var uncachedArtists = profile.Artists
    .Where(a => mbidCache.GetMbid(a.CanonicalName) is null)
    .Select(static a => a.CanonicalName)
    .Take(50)
    .ToList();

if (uncachedArtists.Count > 0)
{
    Console.Write($"mbid_lookup: resolving {uncachedArtists.Count} artists via MusicBrainz... ");
    using var mbLookup = new MusicBrainzArtistLookup();
    var confirmed = await mbLookup.LookupCandidatesAsync(uncachedArtists, cancellationToken: CancellationToken.None);

    foreach (var result in confirmed)
    {
        if (result.MusicBrainzId is not null)
        {
            mbidCache.Set(
                BeatTrackAnalysis.CanonicalizeArtistName(result.Query),
                result.MusicBrainzId,
                result.MatchedName,
                "musicbrainz");
        }
    }

    Console.WriteLine($"{confirmed.Count} confirmed");
}

mbidCache.Save();
Console.WriteLine($"mbid_cache: {mbidCache.Count} total entries saved");

// Collect seed artists with MBIDs for gap analysis
var seedArtists = mbidCache.GetAll()
    .Select(static e => (Mbid: e.Mbid, Name: e.MatchedName ?? e.CanonicalName))
    .DistinctBy(static s => s.Mbid, StringComparer.OrdinalIgnoreCase)
    .ToList();

// Shared state for gap + re-engagement + strange absence analysis
var allSimilar = new Dictionary<string, List<SimilarArtist>>(StringComparer.OrdinalIgnoreCase);
var seedNameByMbid = seedArtists.ToDictionary(s => s.Mbid, s => s.Name, StringComparer.OrdinalIgnoreCase);
var knownMbids = new HashSet<string>(
    mbidCache.GetAll().Select(static e => e.Mbid),
    StringComparer.OrdinalIgnoreCase);

if (seedArtists.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("=== Gap analysis: similar artists you might be missing ===");
    Console.WriteLine($"seed_artists: {seedArtists.Count} (with MBIDs)");

    // Build a set of all known artist names for comparison
    var knownCanonical = new HashSet<string>(
        profile.Artists.Select(static a => a.CanonicalName),
        StringComparer.OrdinalIgnoreCase);

    // Load similar artists cache — one markdown table per seed artist
    var similarCacheDir = Path.Combine(cacheDir, "similar-artists");
    Directory.CreateDirectory(similarCacheDir);

    using var similarLookup = new ListenBrainzSimilarArtistsLookup();
    var queriedCount = 0;
    var cachedCount = 0;

    Console.Write("  loading similar artists: ");
    foreach (var (mbid, name) in seedArtists)
    {
        var cacheFile = Path.Combine(similarCacheDir, $"{mbid}.md");
        if (File.Exists(cacheFile))
        {
            // Load from cache
            var cached = LoadSimilarArtistsCache(cacheFile);
            if (cached.Count > 0)
            {
                allSimilar[mbid] = cached;
                cachedCount++;
                continue;
            }
        }

        // Query API and cache
        try
        {
            var similar = await similarLookup.GetSimilarArtistsAsync(mbid);
            if (similar.Count > 0)
            {
                allSimilar[mbid] = [.. similar];
                SaveSimilarArtistsCache(cacheFile, name, similar);
            }
            queriedCount++;
            Console.Write(".");
        }
        catch (HttpRequestException)
        {
            Console.Write("x");
        }
    }

    Console.WriteLine($" ({cachedCount} cached, {queriedCount} queried)");

    // Aggregate across all seeds
    var aggregatedMap = new Dictionary<string, (string Name, int TotalScore, List<string> Seeds)>(StringComparer.OrdinalIgnoreCase);

    foreach (var (seedMbid, similarList) in allSimilar)
    {
        var seedName = seedNameByMbid.GetValueOrDefault(seedMbid, seedMbid);
        foreach (var artist in similarList)
        {
            if (!aggregatedMap.TryGetValue(artist.ArtistMbid, out var entry))
            {
                entry = (artist.Name, 0, []);
                aggregatedMap[artist.ArtistMbid] = entry;
            }

            entry.Seeds.Add(seedName);
            aggregatedMap[artist.ArtistMbid] = (entry.Name, entry.TotalScore + artist.Score, entry.Seeds);
        }
    }

    var aggregated = aggregatedMap
        .Where(kvp => !knownMbids.Contains(kvp.Key))
        .Select(kvp => new AggregatedSimilarArtist(kvp.Key, kvp.Value.Name, kvp.Value.Seeds.Count, kvp.Value.TotalScore, kvp.Value.Seeds))
        .OrderByDescending(static a => a.SeedCount)
        .ThenByDescending(static a => a.TotalScore)
        .ToList();

    Console.WriteLine($"total_similar: {aggregatedMap.Count} artists found across {allSimilar.Count} seeds");

    // Filter to artists NOT in any of our sources (check both name and MBID)
    var gaps = aggregated
        .Where(a => !knownCanonical.Contains(BeatTrackAnalysis.CanonicalizeArtistName(a.Name)))
        .ToList();

    Console.WriteLine($"gaps: {gaps.Count} artists similar to your favorites but not in your data");
    Console.WriteLine();

    // Show top gaps — artists similar to the most seeds
    Console.WriteLine("top_gaps (similar to multiple favorites but never listened to):");
    foreach (var gap in gaps.Take(30))
    {
        var seeds = string.Join(", ", gap.SimilarToSeeds.Take(5));
        Console.WriteLine($"  {gap.Name}  (similar to {gap.SeedCount} seeds: {seeds})");
    }

    if (gaps.Count > 30)
    {
        Console.WriteLine($"  ... and {gaps.Count - 30} more");
    }
}

// === Slice comparison: Last.fm vs YouTube ===

var lastFmStatsPath = FindFile(
    Environment.GetEnvironmentVariable("BEAT_TRACK_LASTFM_STATS_CSV"),
    Path.Combine(workspaceData, "lastfmstats", "lastfmstats-runfaster2000.csv"),
    Path.Combine(homeData, "lastfmstats", "lastfmstats-runfaster2000.csv"));

if (lastFmStatsPath is not null && youTubeSnapshot is not null)
{
    Console.WriteLine();
    Console.WriteLine("=== Slice comparison: Last.fm vs YouTube ===");
    Console.WriteLine();

    // Build Last.fm slice from full scrobble history
    using var scrobbleReader = File.OpenText(lastFmStatsPath);
    var scrobbles = LastFmStatsCsvReader.ParseCsv(scrobbleReader);
    Console.WriteLine($"lastfm_full_history: {scrobbles.Count} scrobbles");

    var lastFmWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    foreach (var scrobble in scrobbles)
    {
        // Pre-clean scrobble artist names: strip channel suffixes that YouTube
        // scrobblers sometimes leave (e.g. "BelleSebastianVEVO" → "BelleSebastian")
        var cleaned = ArtistNameMatcher.CleanChannelName(scrobble.ArtistName);
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
        lastFmWeights.TryGetValue(canonical, out var current);
        lastFmWeights[canonical] = current + 1;
    }

    // Merge Last.fm weight variants (e.g. "bellesebastian" from cleaned VEVO scrobbles
    // should merge into "belle and sebastian" from normal scrobbles)
    lastFmWeights = ArtistNameMatcher.MergeWeights(lastFmWeights);

    var lastFmSlice = new BeatTrackSlice("Last.fm", lastFmWeights);

    // Build matcher from cleaned Last.fm artist names for resolving YouTube channel names
    var matcher = new ArtistNameMatcher(lastFmWeights.Keys);

    // Build YouTube slice from music-candidate watch events
    var ytWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    var matcherResolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var watch in youTubeSnapshot.WatchEvents)
    {
        if (!watch.IsMusicCandidate || watch.MusicMatchReason is null)
        {
            continue;
        }

        string? artistName = null;
        if (watch.MusicMatchReason.StartsWith("KnownArtist:", StringComparison.Ordinal))
        {
            artistName = watch.MusicMatchReason["KnownArtist:".Length..];
        }
        else if (watch.MusicMatchReason is "AutoMusicChannel" && !string.IsNullOrWhiteSpace(watch.ChannelName))
        {
            artistName = watch.ChannelName;
        }
        else if (watch.MusicMatchReason is "MusicPlatform" or "StrongHeuristic" or "MediumHeuristic")
        {
            // Extract artist from "Artist - Track" title pattern on platform channels
            var titleParts = watch.Title.Split([" - ", " – "], 2, StringSplitOptions.TrimEntries);
            if (titleParts.Length == 2 && titleParts[0].Length >= 2)
            {
                // Validate against known artists via matcher
                var candidate = matcher.TryResolve(titleParts[0]);
                if (candidate is not null)
                {
                    artistName = titleParts[0];
                }
            }
        }

        if (artistName is not null)
        {
            // Try to resolve to a known Last.fm artist name
            var resolved = matcher.TryResolve(artistName);
            if (resolved is not null)
            {
                if (!matcherResolutions.ContainsKey(artistName))
                {
                    matcherResolutions[artistName] = resolved;
                }

                ytWeights.TryGetValue(resolved, out var current);
                ytWeights[resolved] = current + 1;
            }
            else
            {
                var cleaned = ArtistNameMatcher.CleanChannelName(artistName);
                var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
                ytWeights.TryGetValue(canonical, out var current);
                ytWeights[canonical] = current + 1;
            }
        }
    }

    var ytSlice = new BeatTrackSlice("YouTube", ytWeights);

    Console.WriteLine($"lastfm_slice: {lastFmSlice.ArtistWeights.Count} artists");
    Console.WriteLine($"youtube_slice: {ytSlice.ArtistWeights.Count} artists");
    Console.WriteLine($"matcher_resolutions: {matcherResolutions.Count}");
    foreach (var (original, resolved) in matcherResolutions.OrderBy(static kvp => kvp.Value, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {original} -> {resolved}");
    }
    Console.WriteLine();

    var comparison = BeatTrackSliceComparer.Compare(lastFmSlice, ytSlice);

    Console.WriteLine($"shared ({comparison.Shared.Count} artists):");
    foreach (var artist in comparison.Shared.Take(40))
    {
        Console.WriteLine($"  {artist.CanonicalName}  (lastfm={artist.WeightA:N0}, youtube={artist.WeightB:N0})");
    }

    if (comparison.Shared.Count > 40)
    {
        Console.WriteLine($"  ... and {comparison.Shared.Count - 40} more");
    }

    Console.WriteLine();
    Console.WriteLine($"lastfm_only ({comparison.OnlyA.Count} artists):");
    foreach (var artist in comparison.OnlyA.Take(30))
    {
        Console.WriteLine($"  {artist.CanonicalName}  ({artist.Weight:N0} scrobbles)");
    }

    if (comparison.OnlyA.Count > 30)
    {
        Console.WriteLine($"  ... and {comparison.OnlyA.Count - 30} more");
    }

    Console.WriteLine();
    Console.WriteLine($"youtube_only ({comparison.OnlyB.Count} artists):");
    foreach (var artist in comparison.OnlyB.Take(30))
    {
        Console.WriteLine($"  {artist.CanonicalName}  ({artist.Weight:N0} watches)");
    }

    if (comparison.OnlyB.Count > 30)
    {
        Console.WriteLine($"  ... and {comparison.OnlyB.Count - 30} more");
    }

    // === Time-windowed slices ===
    // Build a combined (Last.fm + YouTube) slice for a given time window
    BeatTrackSlice BuildCombinedSlice(int days, string label)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var cutoffMs = cutoff.ToUnixTimeMilliseconds();
        var cutoffSec = cutoff.ToUnixTimeSeconds();

        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var scrobble in scrobbles.Where(s => s.TimestampMs >= cutoffMs))
        {
            var cleaned = ArtistNameMatcher.CleanChannelName(scrobble.ArtistName);
            var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
            var resolved = matcher.TryResolve(canonical) ?? canonical;
            weights.TryGetValue(resolved, out var current);
            weights[resolved] = current + 1;
        }

        foreach (var watch in youTubeSnapshot.WatchEvents)
        {
            if (!watch.IsMusicCandidate || watch.MusicMatchReason is null)
                continue;
            if (watch.WatchedAtUnixTime is null || watch.WatchedAtUnixTime < cutoffSec)
                continue;

            string? artistName = null;
            if (watch.MusicMatchReason.StartsWith("KnownArtist:", StringComparison.Ordinal))
                artistName = watch.MusicMatchReason["KnownArtist:".Length..];
            else if (watch.MusicMatchReason is "AutoMusicChannel" && !string.IsNullOrWhiteSpace(watch.ChannelName))
                artistName = watch.ChannelName;
            else if (watch.MusicMatchReason is "MusicPlatform" or "StrongHeuristic" or "MediumHeuristic")
            {
                var titleParts = watch.Title.Split([" - ", " – "], 2, StringSplitOptions.TrimEntries);
                if (titleParts.Length == 2 && titleParts[0].Length >= 2)
                {
                    var candidate = matcher.TryResolve(titleParts[0]);
                    if (candidate is not null)
                        artistName = titleParts[0];
                }
            }

            if (artistName is not null)
            {
                var resolved = matcher.TryResolve(artistName);
                if (resolved is not null)
                {
                    weights.TryGetValue(resolved, out var current);
                    weights[resolved] = current + 1;
                }
                else
                {
                    var cleaned = ArtistNameMatcher.CleanChannelName(artistName);
                    var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
                    weights.TryGetValue(canonical, out var current);
                    weights[canonical] = current + 1;
                }
            }
        }

        return new BeatTrackSlice(label, weights);
    }

    // 12-month bubble: Last.fm vs YouTube
    var slice365 = BuildCombinedSlice(365, "All (365d)");
    var slice60 = BuildCombinedSlice(60, "All (60d)");

    Console.WriteLine();
    Console.WriteLine($"=== Last 365 days: {slice365.ArtistWeights.Count} artists, {slice365.ArtistWeights.Values.Sum():N0} events ===");
    Console.WriteLine($"=== Last 60 days: {slice60.ArtistWeights.Count} artists, {slice60.ArtistWeights.Values.Sum():N0} events ===");

    // New interests: in 60d but not in the prior 305d (365d minus 60d)
    Console.WriteLine();
    Console.WriteLine("=== New interests (in last 60d but not in prior 305d) ===");
    Console.WriteLine();

    var newInterests = slice60.ArtistWeights
        .Where(kvp => !slice365.ArtistWeights.ContainsKey(kvp.Key)
            || slice365.ArtistWeights[kvp.Key] == kvp.Value) // all activity is in the 60d window
        .Where(kvp =>
        {
            // Check if the artist had ANY activity before the 60d window
            if (!slice365.ArtistWeights.TryGetValue(kvp.Key, out var total365))
                return true; // not in 365d at all — truly new
            return total365 <= kvp.Value; // all plays are within 60d
        })
        .OrderByDescending(static kvp => kvp.Value)
        .ToList();

    Console.WriteLine($"new_interests: {newInterests.Count} artists");
    foreach (var (name, weight) in newInterests)
    {
        Console.WriteLine($"  {name}  ({weight:N0} plays in last 60d)");
    }

    // Surging: in both windows but disproportionately active recently
    Console.WriteLine();
    Console.WriteLine("=== Surging (much more active in last 60d vs prior 305d) ===");
    Console.WriteLine();

    var surging = slice60.ArtistWeights
        .Where(kvp => slice365.ArtistWeights.TryGetValue(kvp.Key, out var total365)
            && total365 > kvp.Value) // has prior activity
        .Select(kvp =>
        {
            var recent = kvp.Value;
            var total = slice365.ArtistWeights[kvp.Key];
            var prior = total - recent;
            // Ratio: recent plays per day vs prior plays per day
            var recentRate = recent / 60.0;
            var priorRate = prior / 305.0;
            var surgeRatio = priorRate > 0 ? recentRate / priorRate : recent;
            return (Name: kvp.Key, Recent: recent, Prior: prior, Total: total, SurgeRatio: surgeRatio);
        })
        .Where(static x => x.SurgeRatio > 2.0 && x.Recent >= 3) // at least 2x the rate and 3+ recent plays
        .OrderByDescending(static x => x.SurgeRatio)
        .ToList();

    Console.WriteLine($"surging: {surging.Count} artists");
    foreach (var s in surging.Take(30))
    {
        Console.WriteLine($"  {s.Name}  (60d={s.Recent:N0}, prior={s.Prior:N0}, surge={s.SurgeRatio:N1}x)");
    }

    // === Re-engagement: forgotten favorites in the same cluster as new interests ===
    Console.WriteLine();
    Console.WriteLine("=== Re-engage: forgotten favorites similar to your new interests ===");
    Console.WriteLine();

    // Build inverted index: similar artist MBID → list of seed names that consider it similar
    var invertedSimilar = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    foreach (var (seedMbid, similarList) in allSimilar)
    {
        var seedName2 = seedNameByMbid.GetValueOrDefault(seedMbid, seedMbid);
        foreach (var sim in similarList)
        {
            if (!invertedSimilar.TryGetValue(sim.ArtistMbid, out var seeds))
            {
                seeds = [];
                invertedSimilar[sim.ArtistMbid] = seeds;
            }
            seeds.Add(seedName2);
        }
    }

    // Also build: MBID → canonical name → list of seeds similar to that MBID
    // This lets us look up by name when the new interest doesn't have an MBID
    var mbidByCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in mbidCache.GetAll())
    {
        mbidByCanonical.TryAdd(entry.CanonicalName, entry.Mbid);
    }

    // Combine new interests + surging artists
    var recentArtists = newInterests.Select(static kvp => (Name: kvp.Key, Weight: kvp.Value, Tag: "new"))
        .Concat(surging.Select(static s => (Name: s.Name, Weight: s.Recent, Tag: "surging")))
        .DistinctBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
        .Where(static x => x.Weight >= 3)
        .OrderByDescending(static x => x.Weight)
        .ToList();

    // Identify "dormant" favorites: in 365d but barely in 60d
    var dormantFavorites = slice365.ArtistWeights
        .Where(kvp => kvp.Value >= 10)
        .Where(kvp =>
        {
            slice60.ArtistWeights.TryGetValue(kvp.Key, out var recent);
            return recent < 2; // barely active recently
        })
        .ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    foreach (var (name, weight, tag) in recentArtists)
    {
        // Try to find this artist's MBID
        if (!mbidByCanonical.TryGetValue(name, out var artistMbid))
        {
            continue;
        }

        // Check inverted index: which seeds consider this artist similar?
        if (!invertedSimilar.TryGetValue(artistMbid, out var clusterSeeds))
        {
            continue;
        }

        // Filter to dormant favorites
        var dormantInCluster = clusterSeeds
            .Where(seedName2 =>
            {
                var seedCanonical = BeatTrackAnalysis.CanonicalizeArtistName(seedName2);
                return dormantFavorites.ContainsKey(seedCanonical);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        if (dormantInCluster.Count == 0)
        {
            continue;
        }

        Console.WriteLine($"  {name} ({tag}, {weight:N0} plays) → revisit: {string.Join(", ", dormantInCluster)}");
    }

    // === Strange absences: artists similar to many active artists but completely missing ===
    Console.WriteLine();
    Console.WriteLine("=== Strange absences (similar to many of your active artists, but never listened to) ===");
    Console.WriteLine();

    // Get MBIDs for currently active artists (60d)
    var active60dMbids = slice60.ArtistWeights.Keys
        .Select(name => (Name: name, Mbid: mbidByCanonical.GetValueOrDefault(name)))
        .Where(static x => x.Mbid is not null)
        .ToList();

    // For each active artist's MBID, gather their similar artists
    var absenceCandidates = new Dictionary<string, (string Name, double Score, List<string> ActiveNeighbors)>(StringComparer.OrdinalIgnoreCase);

    foreach (var (activeName, activeMbid) in active60dMbids)
    {
        if (activeMbid is null || !allSimilar.TryGetValue(activeMbid, out var similarList))
        {
            continue;
        }

        foreach (var sim in similarList)
        {
            // Skip if the artist is in ANY of our data
            var simCanonical = BeatTrackAnalysis.CanonicalizeArtistName(sim.Name);
            if (slice365.ArtistWeights.ContainsKey(simCanonical))
            {
                continue;
            }

            if (knownMbids.Contains(sim.ArtistMbid))
            {
                continue;
            }

            if (!absenceCandidates.TryGetValue(sim.ArtistMbid, out var entry))
            {
                entry = (sim.Name, 0, []);
                absenceCandidates[sim.ArtistMbid] = entry;
            }

            entry.ActiveNeighbors.Add(activeName);
            absenceCandidates[sim.ArtistMbid] = (entry.Name, entry.Score + sim.Score, entry.ActiveNeighbors);
        }
    }

    var strangeAbsences = absenceCandidates.Values
        .Where(static x => x.ActiveNeighbors.Count >= 3) // similar to at least 3 currently-active artists
        .OrderByDescending(static x => x.ActiveNeighbors.Count)
        .ThenByDescending(static x => x.Score)
        .ToList();

    Console.WriteLine($"strange_absences: {strangeAbsences.Count} artists");
    foreach (var absence in strangeAbsences.Take(30))
    {
        var neighbors = string.Join(", ", absence.ActiveNeighbors.Distinct(StringComparer.OrdinalIgnoreCase).Take(5));
        Console.WriteLine($"  {absence.Name}  (neighbors: {absence.ActiveNeighbors.Distinct(StringComparer.OrdinalIgnoreCase).Count()} active artists — {neighbors})");
    }
    if (strangeAbsences.Count > 30)
        Console.WriteLine($"  ... and {strangeAbsences.Count - 30} more");

    // Also check: for each seed, which of its similar artists are in our new interests?
    // This catches new interests that don't have MBIDs yet.
    Console.WriteLine();
    Console.WriteLine("dormant favorites you might revisit:");
    var suggestedDormant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (seedMbid, similarList) in allSimilar)
    {
        var seedName2 = seedNameByMbid.GetValueOrDefault(seedMbid, seedMbid);
        var seedCanonical = BeatTrackAnalysis.CanonicalizeArtistName(seedName2);

        if (!dormantFavorites.ContainsKey(seedCanonical))
        {
            continue;
        }

        // How many of this seed's similar artists overlap with recent interests?
        var overlapCount = similarList.Count(sim =>
        {
            var simCanonical = BeatTrackAnalysis.CanonicalizeArtistName(sim.Name);
            return slice60.ArtistWeights.ContainsKey(simCanonical);
        });

        if (overlapCount >= 3 && suggestedDormant.Add(seedCanonical))
        {
            var overlapping = similarList
                .Where(sim => slice60.ArtistWeights.ContainsKey(BeatTrackAnalysis.CanonicalizeArtistName(sim.Name)))
                .Select(static sim => sim.Name)
                .Take(5);
            Console.WriteLine($"  {seedName2} ({dormantFavorites[seedCanonical]:N0} plays in 365d) — you're currently into: {string.Join(", ", overlapping)}");
        }
    }
}

return 0;

// --- Helper methods for similar artist caching ---

static List<SimilarArtist> LoadSimilarArtistsCache(string filePath)
{
    var (_, rows) = MarkdownTableStore.Read(filePath);
    var results = new List<SimilarArtist>(rows.Count);
    foreach (var row in rows)
    {
        if (row.Length >= 3)
        {
            int.TryParse(row.Length > 2 ? row[2] : "0", out var score);
            results.Add(new SimilarArtist(row[0], row[1], score, row.Length > 3 ? row[3] : null, null));
        }
    }
    return results;
}

static void SaveSimilarArtistsCache(string filePath, string seedName, IReadOnlyList<SimilarArtist> artists)
{
    string[] headers = ["mbid", "name", "score", "type"];
    var rows = artists.Select(a => new[] { a.ArtistMbid, a.Name, a.Score.ToString(), a.Type ?? "" });
    MarkdownTableStore.Write(filePath, headers, rows);
}
