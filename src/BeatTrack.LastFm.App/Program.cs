using System.Text.Json;
using BeatTrack.Core;
using BeatTrack.LastFm;
using BeatTrack.LastFm.App;

var apiKey = Environment.GetEnvironmentVariable("LASTFM_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set LASTFM_API_KEY first.");
    return 1;
}

var userName = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("LASTFM_USER");
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

var sharedSecret = Environment.GetEnvironmentVariable("LASTFM_SHARED_SECRET");
using var httpClient = new HttpClient();
var client = new LastFmClient(httpClient, new LastFmClientOptions(apiKey, sharedSecret, "beat-track"));

var profile = (await client.GetUserInfoAsync(userName)).ToBeatTrackUserProfile();
var recentTracks = await FetchRecentTracksAsync(client, userName, recentPages, recentPageSize);
var lovedTracks = await FetchLovedTracksAsync(client, userName, lovedPages, lovedPageSize);
var topArtistsByPeriod = await FetchTopArtistsByPeriodAsync(client, userName);

var snapshot = new BeatTrackSnapshot(
    UserName: userName,
    FetchedAt: DateTimeOffset.UtcNow,
    Profile: profile,
    RecentTracks: recentTracks,
    TopArtistsByPeriod: topArtistsByPeriod,
    LovedTracks: lovedTracks);

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
    Console.WriteLine($"user: {profile.UserName}");
    Console.WriteLine($"real_name: {profile.RealName ?? string.Empty}");
    Console.WriteLine($"play_count: {profile.PlayCount?.ToString() ?? string.Empty}");
    Console.WriteLine($"recent_tracks_fetched: {recentTracks.Count}");
    Console.WriteLine($"loved_tracks_fetched: {lovedTracks.Count}");
    Console.WriteLine();
    Console.WriteLine("recent_track_sample:");
    foreach (var item in recentTracks.Take(10))
    {
        Console.WriteLine($"- {item.ArtistName} - {item.TrackName} @ {item.PlayedAtUnixTime?.ToString() ?? ""}");
    }

    Console.WriteLine();
    Console.WriteLine("top_artists_by_period:");
    foreach (var pair in topArtistsByPeriod)
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

    var buckets = BeatTrackTimelineAnalysis.BuildArtistBuckets(recentTracks, bucketSizeHours: 24, topArtistCount: 5);
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

static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchRecentTracksAsync(ILastFmClient client, string userName, int pages, int pageSize)
{
    var items = new List<BeatTrackListeningEvent>();
    for (var page = 1; page <= pages; page++)
    {
        var result = (await client.GetRecentTracksAsync(new LastFmRecentTracksRequest(userName, pageSize, page))).ToBeatTrackListeningEvents();
        items.AddRange(result.Items);
        if (result.Page >= result.TotalPages || result.Items.Count == 0)
        {
            break;
        }
    }

    return items;
}

static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchLovedTracksAsync(ILastFmClient client, string userName, int pages, int pageSize)
{
    var items = new List<BeatTrackListeningEvent>();
    for (var page = 1; page <= pages; page++)
    {
        var result = (await client.GetLovedTracksAsync(new LastFmPagedUserRequest(userName, pageSize, page))).ToBeatTrackLovedListeningEvents();
        items.AddRange(result.Items);
        if (result.Page >= result.TotalPages || result.Items.Count == 0)
        {
            break;
        }
    }

    return items;
}

static async Task<IReadOnlyDictionary<string, IReadOnlyList<BeatTrackArtistSummary>>> FetchTopArtistsByPeriodAsync(ILastFmClient client, string userName)
{
    var periods = new[]
    {
        LastFmTimePeriod.SevenDays,
        LastFmTimePeriod.OneMonth,
        LastFmTimePeriod.ThreeMonths,
        LastFmTimePeriod.SixMonths,
        LastFmTimePeriod.TwelveMonths,
        LastFmTimePeriod.Overall,
    };

    var results = new Dictionary<string, IReadOnlyList<BeatTrackArtistSummary>>(StringComparer.Ordinal);
    foreach (var period in periods)
    {
        var response = await client.GetTopArtistsAsync(new LastFmUserChartRequest(userName, period, Limit: 50));
        var mapped = response.ToBeatTrackArtistSummaries();
        results[period.ToString()] = mapped.Items;
    }

    return results;
}

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
