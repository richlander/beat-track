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

// Load known artists from Last.fm snapshot if available
var snapshotPath = Environment.GetEnvironmentVariable("BEAT_TRACK_SNAPSHOT_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "runfaster2000-snapshot.json");

var knownArtists = LoadKnownArtists(snapshotPath);
Console.WriteLine($"known_artists_loaded: {knownArtists.Count} (from {(File.Exists(snapshotPath) ? snapshotPath : "none")})");

var classifier = new MusicClassifier(knownArtists);

var client = new YouTubeTakeoutClient();
using var musicReader = File.OpenText(musicLibraryPath);
var savedTracks = client.ParseMusicLibrarySongs(musicReader);
var watchEvents = client.ParseWatchHistoryHtml(await File.ReadAllTextAsync(watchHistoryPath), classifier);

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
Console.WriteLine("saved_track_sample:");
foreach (var item in savedTracks.Take(10))
{
    Console.WriteLine($"- {string.Join(", ", item.ArtistNames)} - {item.Title} [{item.AlbumTitle ?? ""}]");
}

Console.WriteLine();
Console.WriteLine("watch_event_sample:");
foreach (var item in watchEvents.Take(15))
{
    Console.WriteLine($"- {item.EventKind}: {item.ChannelName} - {item.Title} (music={item.IsMusicCandidate}, reason={item.MusicMatchReason ?? "none"})");
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

Console.WriteLine();
Console.WriteLine("rejected_sample (non-music with ' - ' in title):");
foreach (var item in watchEvents.Where(static x => !x.IsMusicCandidate && x.Title.Contains(" - ", StringComparison.Ordinal))
                                .Take(10))
{
    Console.WriteLine($"  - {item.ChannelName} - {item.Title} [reason={item.MusicMatchReason}]");
}

return 0;

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

        // Extract from recent_tracks
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

        // Extract from top_artists_by_period
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

        // Extract from loved_tracks
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
