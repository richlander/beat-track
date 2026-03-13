using System.Text.Json;
using BeatTrack.Core;
using BeatTrack.YouTube;

var takeoutDir = args.Length > 0 ? args[0] : "/home/rich/.openclaw/workspace/data/takeout";
var musicLibraryPath = Path.Combine(takeoutDir, "extracted", "Takeout", "YouTube and YouTube Music", "music (library and uploads)", "music library songs.csv");
var watchHistoryPath = Path.Combine(takeoutDir, "extracted", "Takeout", "YouTube and YouTube Music", "history", "watch-history.html");

if (!File.Exists(musicLibraryPath) || !File.Exists(watchHistoryPath))
{
    Console.Error.WriteLine("Expected extracted Takeout files are missing. Extract the YouTube Takeout zip(s) under data/takeout/extracted first.");
    return 1;
}

// Load known artists from Last.fm snapshot
var snapshotPath = Environment.GetEnvironmentVariable("BEAT_TRACK_SNAPSHOT_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "runfaster2000-snapshot.json");

var knownArtists = LoadKnownArtists(snapshotPath);
Console.WriteLine($"lastfm_artists: {knownArtists.Count}");

// Parse YouTube saved tracks and add their artists
var client = new YouTubeTakeoutClient();
using var musicReader = File.OpenText(musicLibraryPath);
var savedTracks = client.ParseMusicLibrarySongs(musicReader);

var savedTrackArtists = savedTracks.SelectMany(static t => t.ArtistNames).ToList();
foreach (var artist in savedTrackArtists)
{
    knownArtists.Add(artist);
}

Console.WriteLine($"youtube_saved_track_artists: {savedTrackArtists.Count} ({savedTrackArtists.Distinct(StringComparer.OrdinalIgnoreCase).Count()} unique)");
Console.WriteLine($"known_artists_after_merge: {knownArtists.Count}");

// Pass 1: classify with Last.fm + saved track artists
var pass1Classifier = new MusicClassifier(knownArtists);
var watchHistoryHtml = await File.ReadAllTextAsync(watchHistoryPath);
var pass1Events = client.ParseWatchHistoryHtml(watchHistoryHtml, pass1Classifier);

// Discover new artists from heuristic matches
var discoveredArtists = MusicArtistDiscovery.DiscoverArtists(pass1Events);
Console.WriteLine($"discovered_artists: {discoveredArtists.Count}");

foreach (var discovered in discoveredArtists)
{
    knownArtists.Add(discovered.Name);
}

// MusicBrainz validation: check remaining heuristic channel names
var skipMusicBrainz = IsTrue(Environment.GetEnvironmentVariable("BEAT_TRACK_SKIP_MUSICBRAINZ"));
if (!skipMusicBrainz)
{
    // Build intermediate classifier to find what's still in StructuralHeuristic
    var intermediateClassifier = new MusicClassifier(knownArtists);
    var intermediateEvents = client.ParseWatchHistoryHtml(watchHistoryHtml, intermediateClassifier);

    var heuristicChannels = intermediateEvents
        .Where(static e => e.IsMusicCandidate && e.MusicMatchReason == "StructuralHeuristic" && !string.IsNullOrWhiteSpace(e.ChannelName))
        .GroupBy(static e => e.ChannelName!, StringComparer.OrdinalIgnoreCase)
        .Where(static g => g.Count() >= 3)
        .Select(static g => g.First().ChannelName!)
        .Where(name => !knownArtists.Contains(name))
        .ToList();

    Console.WriteLine($"musicbrainz_candidates: {heuristicChannels.Count}");

    using var mbLookup = new MusicBrainzArtistLookup();
    var confirmed = await mbLookup.LookupCandidatesAsync(
        heuristicChannels,
        new Progress<string>(name => Console.Write($"\r  checking: {name,-50}")));
    Console.WriteLine();

    Console.WriteLine($"musicbrainz_confirmed: {confirmed.Count}");
    foreach (var match in confirmed)
    {
        knownArtists.Add(match.MatchedName ?? match.Query);
        Console.WriteLine($"  + {match.Query} => {match.MatchedName} (score={match.Score}, mbid={match.MusicBrainzId})");
    }
}

Console.WriteLine($"known_artists_final: {knownArtists.Count}");
Console.WriteLine();

// Final pass: reclassify with expanded artist set
var finalClassifier = new MusicClassifier(knownArtists);
var watchEvents = client.ParseWatchHistoryHtml(watchHistoryHtml, finalClassifier);

var musicCandidates = watchEvents.Where(static x => x.IsMusicCandidate).ToList();

Console.WriteLine($"saved_tracks: {savedTracks.Count}");
Console.WriteLine($"watch_events: {watchEvents.Count}");
Console.WriteLine($"music_candidate_watch_events: {musicCandidates.Count}");
Console.WriteLine();

// Breakdown by match reason
Console.WriteLine("match_reason_breakdown:");
foreach (var group in musicCandidates.GroupBy(static x => x.MusicMatchReason ?? "(none)")
                                     .OrderByDescending(static g => g.Count()))
{
    Console.WriteLine($"  {group.Count(),5}  {group.Key}");
}

Console.WriteLine();
Console.WriteLine("discovered_artists_detail:");
foreach (var artist in discoveredArtists)
{
    // Show whether it moved from heuristic to KnownArtist in pass 2
    var pass2Reason = watchEvents
        .FirstOrDefault(e => string.Equals(e.ChannelName, artist.Name, StringComparison.OrdinalIgnoreCase) && e.IsMusicCandidate)
        ?.MusicMatchReason;
    Console.WriteLine($"  {artist.EventCount,4}  {artist.Name} [pass2={pass2Reason}]");
}

Console.WriteLine();
Console.WriteLine("remaining_structural_heuristic_channels:");
foreach (var item in musicCandidates.Where(static x => x.MusicMatchReason == "StructuralHeuristic" && !string.IsNullOrWhiteSpace(x.ChannelName))
                                    .GroupBy(static x => x.ChannelName!, StringComparer.OrdinalIgnoreCase)
                                    .Select(static g => new { Name = g.First().ChannelName!, Count = g.Count() })
                                    .OrderByDescending(static x => x.Count)
                                    .Take(20))
{
    Console.WriteLine($"  {item.Count,4}  {item.Name}");
}

Console.WriteLine();
Console.WriteLine("top_music_candidate_channels:");
foreach (var item in musicCandidates.Where(static x => !string.IsNullOrWhiteSpace(x.ChannelName))
                                    .GroupBy(static x => x.ChannelName!, StringComparer.OrdinalIgnoreCase)
                                    .Select(static g => new { Name = g.First().ChannelName!, Count = g.Count(), TopReason = g.GroupBy(static x => x.MusicMatchReason).MaxBy(static r => r.Count())?.Key })
                                    .OrderByDescending(static x => x.Count)
                                    .Take(25))
{
    Console.WriteLine($"  {item.Count,4}  {item.Name} [{item.TopReason}]");
}

Console.WriteLine();
Console.WriteLine("false_positive_sanity_check (suspect channels still matching):");
string[] suspectChannels = ["BPS.space", "Julian Ilett", "Scott Manley", "Linus Tech Tips",
    "Numberphile", "Gamers Nexus", "SmarterEveryDay", "Computerphile",
    "Our Ludicrous Future", "The Royal Institution", "Now You Know",
    ".NET Foundation", "NDC Conferences", "Adafruit Industries",
    "Dwarkesh Patel", "NASCompares", "Speculative Future"];
foreach (var ch in suspectChannels)
{
    var matches = watchEvents.Where(x => x.IsMusicCandidate
        && string.Equals(x.ChannelName, ch, StringComparison.OrdinalIgnoreCase)).ToList();
    if (matches.Count > 0)
    {
        Console.WriteLine($"  WARNING: {ch} ({matches.Count}):");
        foreach (var m in matches.Take(3))
        {
            Console.WriteLine($"    - {m.Title} [reason={m.MusicMatchReason}]");
        }
    }
}

return 0;

static bool IsTrue(string? value) =>
    string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

static HashSet<string> LoadKnownArtists(string snapshotPath)
{
    var artists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    if (!File.Exists(snapshotPath))
    {
        return artists;
    }

    try
    {
        using var stream = File.OpenRead(snapshotPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (root.TryGetProperty("recent_tracks", out var recentTracks))
        {
            foreach (var track in recentTracks.EnumerateArray())
            {
                if (track.TryGetProperty("artist_name", out var artist) && artist.ValueKind == JsonValueKind.String)
                {
                    var name = artist.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        artists.Add(name);
                    }
                }
            }
        }

        if (root.TryGetProperty("top_artists_by_period", out var topArtists))
        {
            foreach (var period in topArtists.EnumerateObject())
            {
                foreach (var entry in period.Value.EnumerateArray())
                {
                    if (entry.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    {
                        var n = name.GetString();
                        if (!string.IsNullOrWhiteSpace(n))
                        {
                            artists.Add(n);
                        }
                    }
                }
            }
        }

        if (root.TryGetProperty("loved_tracks", out var lovedTracks))
        {
            foreach (var track in lovedTracks.EnumerateArray())
            {
                if (track.TryGetProperty("artist_name", out var artist) && artist.ValueKind == JsonValueKind.String)
                {
                    var name = artist.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        artists.Add(name);
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: could not load snapshot: {ex.Message}");
    }

    return artists;
}
