using BeatTrack.Core;

namespace BeatTrack.LastFm;

/// <summary>
/// Live mode: polls Last.fm for recent scrobbles.
///   -n N    Show last N scrobbles (default: 10)
///   -f/-w   Follow/watch mode — poll continuously
///   Combined: -f -n 5 shows last 5 then follows
/// </summary>
public static class LiveCommand
{
    public static async Task<int> RunAsync(ILastFmClient client, string userName, string[] args)
    {
        var count = ParseIntFlag(args, "-n") ?? 10;
        var follow = HasFlag(args, "-f") || HasFlag(args, "-w");
        var pollSeconds = ParseIntFlag(args, "--interval") ?? 15;

        // Track what we've already printed to avoid duplicates in follow mode
        var seen = new HashSet<string>(StringComparer.Ordinal);
        long? lastTimestamp = null;

        // Initial fetch: show last N scrobbles
        var initial = await FetchRecentAsync(client, userName, count);

        if (initial.Count == 0)
        {
            Console.WriteLine("(no recent scrobbles)");
        }
        else
        {
            foreach (var track in initial)
            {
                PrintTrack(track);
                var key = TrackKey(track);
                seen.Add(key);

                if (track.PlayedAtUnixTime is not null
                    && (lastTimestamp is null || track.PlayedAtUnixTime > lastTimestamp))
                {
                    lastTimestamp = track.PlayedAtUnixTime;
                }
            }
        }

        if (!follow)
        {
            return 0;
        }

        // Follow mode: poll for new scrobbles
        Console.WriteLine();
        Console.WriteLine($"--- following {userName} (every {pollSeconds}s, ctrl-c to stop) ---");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                // Fetch since last known timestamp (or last 5 if no timestamp)
                var recent = lastTimestamp is not null
                    ? await FetchRecentSinceAsync(client, userName, lastTimestamp.Value)
                    : await FetchRecentAsync(client, userName, 5);

                // Print new tracks (reverse to show oldest first)
                var newTracks = recent
                    .Where(t => !seen.Contains(TrackKey(t)))
                    .Reverse()
                    .ToList();

                foreach (var track in newTracks)
                {
                    PrintTrack(track);
                    seen.Add(TrackKey(track));

                    if (track.PlayedAtUnixTime is not null
                        && (lastTimestamp is null || track.PlayedAtUnixTime > lastTimestamp))
                    {
                        lastTimestamp = track.PlayedAtUnixTime;
                    }
                }

                // Keep seen set from growing unbounded
                if (seen.Count > 500)
                {
                    seen.Clear();
                    foreach (var track in recent)
                    {
                        seen.Add(TrackKey(track));
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"  (poll error: {ex.Message})");
            }
        }

        Console.WriteLine("--- stopped ---");
        return 0;
    }

    private static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchRecentAsync(
        ILastFmClient client, string userName, int limit)
    {
        var response = await client.GetRecentTracksAsync(
            new LastFmRecentTracksRequest(userName, Limit: Math.Min(limit, 200)));
        return response.ToBeatTrackListeningEvents().Items;
    }

    private static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchRecentSinceAsync(
        ILastFmClient client, string userName, long fromUnixTime)
    {
        var response = await client.GetRecentTracksAsync(
            new LastFmRecentTracksRequest(userName, Limit: 50, FromUnixTime: fromUnixTime));
        return response.ToBeatTrackListeningEvents().Items;
    }

    private static void PrintTrack(BeatTrackListeningEvent track)
    {
        var time = track.PlayedAtUnixTime is not null
            ? DateTimeOffset.FromUnixTimeSeconds(track.PlayedAtUnixTime.Value).ToLocalTime().ToString("HH:mm")
            : "     ";

        var prefix = track.IsNowPlaying ? "▶" : " ";
        var album = !string.IsNullOrWhiteSpace(track.AlbumName) ? $" [{track.AlbumName}]" : "";

        Console.WriteLine($"  {prefix} {time}  {track.ArtistName} — {track.TrackName}{album}");
    }

    private static string TrackKey(BeatTrackListeningEvent track) =>
        $"{track.PlayedAtUnixTime}|{track.ArtistName}|{track.TrackName}|{track.IsNowPlaying}";

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static int? ParseIntFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }

        return null;
    }
}
