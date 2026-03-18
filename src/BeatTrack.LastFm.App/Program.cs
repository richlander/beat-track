using System.Text.Json;
using BeatTrack.Core;
using BeatTrack.LastFm;
using BeatTrack.LastFm.App;

var config = new BeatTrackConfig(BeatTrackPaths.ConfigFile);

var apiKey = config.LastFmApiKey
    ?? Environment.GetEnvironmentVariable("LASTFM_API_KEY");

if (string.IsNullOrWhiteSpace(apiKey))
{
    if (!Console.IsInputRedirected && Environment.GetEnvironmentVariable("TERM") is not null)
    {
        Console.Error.Write("Last.fm API key: ");
        apiKey = Console.ReadLine()?.Trim();
    }

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("No API key found. Set it in:");
        Console.Error.WriteLine($"  config:  {BeatTrackPaths.ConfigFile}  (lastfm_api_key=YOUR_KEY)");
        Console.Error.WriteLine("  env:     LASTFM_API_KEY");
        Console.Error.WriteLine("  stdin:   echo YOUR_KEY | beat-track-lastfm ...");
        return 1;
    }
}

// Check for "live" subcommand: beat-track-lastfm live [-f] [-n 10] [username]
if (args.Length > 0 && string.Equals(args[0], "live", StringComparison.OrdinalIgnoreCase))
{
    var liveArgs = args[1..];
    // Find username: skip flag values (args that follow -n, --interval, etc.)
    var flagsWithValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "-n", "--interval" };
    string? liveUser = null;
    for (var i = 0; i < liveArgs.Length; i++)
    {
        if (liveArgs[i].StartsWith('-'))
        {
            if (flagsWithValues.Contains(liveArgs[i])) i++; // skip the value
            continue;
        }
        liveUser = liveArgs[i];
        break;
    }
    liveUser ??= config.LastFmUser ?? Environment.GetEnvironmentVariable("LASTFM_USER");

    if (string.IsNullOrWhiteSpace(liveUser))
    {
        Console.Error.WriteLine("Pass a username or set LASTFM_USER.");
        return 1;
    }

    var sharedSecretLive = config.LastFmSharedSecret ?? Environment.GetEnvironmentVariable("LASTFM_SHARED_SECRET");
    using var httpClientLive = new HttpClient();
    var clientLive = new LastFmClient(httpClientLive, new LastFmClientOptions(apiKey, sharedSecretLive, "beat-track"));
    return await LiveCommand.RunAsync(clientLive, liveUser, liveArgs);
}

var userName = args.Length > 0 ? args[0] : (config.LastFmUser ?? Environment.GetEnvironmentVariable("LASTFM_USER"));
if (string.IsNullOrWhiteSpace(userName))
{
    Console.Error.WriteLine("Pass a username as the first argument or set LASTFM_USER.");
    return 1;
}

var recentPages = ParsePositiveInt(Environment.GetEnvironmentVariable("BEAT_TRACK_RECENT_PAGES"), fallback: 3);
var recentPageSize = ParsePositiveInt(Environment.GetEnvironmentVariable("BEAT_TRACK_RECENT_PAGE_SIZE"), fallback: 200, max: 200);
var lovedPages = ParsePositiveInt(Environment.GetEnvironmentVariable("BEAT_TRACK_LOVED_PAGES"), fallback: 2);
var lovedPageSize = ParsePositiveInt(Environment.GetEnvironmentVariable("BEAT_TRACK_LOVED_PAGE_SIZE"), fallback: 100, max: 200);
var outputPath = Environment.GetEnvironmentVariable("BEAT_TRACK_OUTPUT_PATH");
var jsonOnly = IsTrue(Environment.GetEnvironmentVariable("BEAT_TRACK_JSON_ONLY"));

var sharedSecret = config.LastFmSharedSecret ?? Environment.GetEnvironmentVariable("LASTFM_SHARED_SECRET");
using var httpClient = new HttpClient();
var client = new LastFmClient(httpClient, new LastFmClientOptions(apiKey, sharedSecret, "beat-track"));

var snapshot = await SnapshotFetcher.FetchAsync(client, userName,
    new SnapshotFetchOptions(recentPages, recentPageSize, lovedPages, lovedPageSize));

var analysis = BeatTrackAnalysis.Analyze(snapshot);

if (!string.IsNullOrWhiteSpace(outputPath))
{
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(snapshot, AppJsonContext.Default.BeatTrackSnapshot));
}

if (!jsonOnly)
{
    Console.WriteLine($"user: {snapshot.Profile.UserName}");
    Console.WriteLine($"real_name: {snapshot.Profile.RealName ?? string.Empty}");
    Console.WriteLine($"play_count: {snapshot.Profile.PlayCount?.ToString() ?? string.Empty}");
    Console.WriteLine($"recent_tracks_fetched: {snapshot.RecentTracks.Count}");
    Console.WriteLine($"loved_tracks_fetched: {snapshot.LovedTracks.Count}");
    Console.WriteLine();
    Console.WriteLine("recent_track_sample:");
    foreach (var item in snapshot.RecentTracks.Take(10))
    {
        Console.WriteLine($"- {item.ArtistName} - {item.TrackName} @ {item.PlayedAtUnixTime?.ToString() ?? ""}");
    }

    Console.WriteLine();
    Console.WriteLine("top_artists_by_period:");
    foreach (var pair in snapshot.TopArtistsByPeriod)
    {
        var top = string.Join(", ", pair.Value.Take(5).Select(static x => $"{x.Name} ({x.PlayCount?.ToString() ?? ""})"));
        Console.WriteLine($"- {pair.Key}: {top}");
    }

    Console.WriteLine();
    Console.WriteLine("stable_core_artists:");
    foreach (var item in analysis.StableCoreArtists.Take(10))
    {
        Console.WriteLine($"- {item.ArtistName} (recent_count={item.Count})");
    }

    Console.WriteLine();
    Console.WriteLine("recent_vs_overall_surge:");
    foreach (var item in analysis.RecentVsOverallSurge.Take(10))
    {
        Console.WriteLine($"- {item.ArtistName} (recent={item.RecentCount}, overall={item.OverallPlayCount?.ToString() ?? ""}, surge={item.SurgeScore:F2})");
    }

    Console.WriteLine();
    Console.WriteLine("alias_candidates:");
    foreach (var item in analysis.AliasCandidates.Take(10))
    {
        Console.WriteLine($"- {item.CanonicalName}: {string.Join(" | ", item.Variants)}");
    }

    var buckets = BeatTrackTimelineAnalysis.BuildArtistBuckets(snapshot.RecentTracks, bucketSizeHours: 24, topArtistCount: 5);
    Console.WriteLine();
    Console.WriteLine("daily_activity_histogram:");
    foreach (var bucket in buckets.TakeLast(14))
    {
        var date = DateTimeOffset.FromUnixTimeSeconds(bucket.BucketStartUnixTime).ToLocalTime().ToString("yyyy-MM-dd");
        var bar = new string('#', Math.Max(1, Math.Min(50, bucket.TotalEvents / 2)));
        var leaders = string.Join(", ", bucket.TopArtists.Take(3).Select(static x => $"{x.ArtistName} ({x.Count})"));
        Console.WriteLine($"- {date} | {bucket.TotalEvents,3} | {bar} | {leaders}");
    }

    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine();
        Console.WriteLine($"snapshot_written_to: {outputPath}");
    }
}
else
{
    Console.WriteLine(JsonSerializer.Serialize(snapshot, AppJsonContext.Default.BeatTrackSnapshot));
}

return 0;

static int ParsePositiveInt(string? value, int fallback, int? max = null)
{
    if (!int.TryParse(value, out var parsed) || parsed <= 0)
    {
        return fallback;
    }

    if (max is not null && parsed > max.Value)
    {
        return max.Value;
    }

    return parsed;
}

static bool IsTrue(string? value) =>
    string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
