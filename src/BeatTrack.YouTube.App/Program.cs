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

var client = new YouTubeTakeoutClient();
using var musicReader = File.OpenText(musicLibraryPath);
var savedTracks = client.ParseMusicLibrarySongs(musicReader);
var watchEvents = client.ParseWatchHistoryHtml(await File.ReadAllTextAsync(watchHistoryPath));

Console.WriteLine($"saved_tracks: {savedTracks.Count}");
Console.WriteLine($"watch_events: {watchEvents.Count}");
Console.WriteLine($"music_candidate_watch_events: {watchEvents.Count(static x => x.IsMusicCandidate)}");
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
    Console.WriteLine($"- {item.EventKind}: {item.ChannelName} - {item.Title} (music_candidate={item.IsMusicCandidate})");
}

Console.WriteLine();
Console.WriteLine("top_music_candidate_channels:");
foreach (var item in watchEvents.Where(static x => x.IsMusicCandidate && !string.IsNullOrWhiteSpace(x.ChannelName))
                                .GroupBy(static x => x.ChannelName!, StringComparer.OrdinalIgnoreCase)
                                .Select(static g => new { Name = g.First().ChannelName!, Count = g.Count() })
                                .OrderByDescending(static x => x.Count)
                                .ThenBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
                                .Take(15))
{
    Console.WriteLine($"- {item.Name} ({item.Count})");
}

Console.WriteLine();
Console.WriteLine("false_positive_sanity_check:");
foreach (var item in watchEvents.Where(static x => x.IsMusicCandidate)
                                .Where(static x => !string.IsNullOrWhiteSpace(x.ChannelName))
                                .Where(static x => x.ChannelName!.Contains("news", StringComparison.OrdinalIgnoreCase)
                                                || x.ChannelName.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
                                                || x.ChannelName.Contains("meidastouch", StringComparison.OrdinalIgnoreCase))
                                .Take(15))
{
    Console.WriteLine($"- {item.ChannelName} - {item.Title}");
}

return 0;
