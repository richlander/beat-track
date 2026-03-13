using System.Text.Json;
using BeatTrack.App;
using BeatTrack.Core;
using BeatTrack.Discogs;
using BeatTrack.YouTube;

// --- Load Last.fm snapshot ---
var lastFmPath = Environment.GetEnvironmentVariable("BEAT_TRACK_SNAPSHOT_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "runfaster2000-snapshot.json");

BeatTrackSnapshot? lastFmSnapshot = null;
if (File.Exists(lastFmPath))
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
var discogsCsvPath = Environment.GetEnvironmentVariable("BEAT_TRACK_DISCOGS_CSV")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "collection-csv", "runfaster2000-collection-20260310-2317.csv");

BeatTrackDiscogsSnapshot? discogsSnapshot = null;
if (File.Exists(discogsCsvPath))
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
var takeoutDir = Environment.GetEnvironmentVariable("BEAT_TRACK_TAKEOUT_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "takeout");

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

return 0;
