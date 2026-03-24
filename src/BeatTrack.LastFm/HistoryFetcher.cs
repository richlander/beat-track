using BeatTrack.Core;

namespace BeatTrack.LastFm;

public static class HistoryFetcher
{
    /// <summary>
    /// Fetches the user's complete scrobble history via the Last.fm API.
    /// Pages through all results at 200 per page with rate limiting.
    /// Reports progress to stderr.
    /// </summary>
    public static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchAllAsync(
        ILastFmClient client, string userName, CancellationToken cancellationToken = default)
    {
        // First request: get total pages
        var firstResponse = await client.GetRecentTracksAsync(
            new LastFmRecentTracksRequest(userName, Limit: 200, Page: 1), cancellationToken);
        var firstPage = firstResponse.ToBeatTrackListeningEvents();

        var total = firstPage.Total ?? 0;
        const int pageSize = 200;
        var totalPages = firstPage.TotalPages ?? (total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 1);
        Console.Error.WriteLine($"Fetching {total:N0} scrobbles ({totalPages:N0} pages)...");

        var allTracks = new List<BeatTrackListeningEvent>(total);
        allTracks.AddRange(firstPage.Items.Where(static t => !t.IsNowPlaying));

        for (var page = 2; page <= totalPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Rate limit: ~5 requests/second
            await Task.Delay(200, cancellationToken);

            const int maxRetries = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    var response = await client.GetRecentTracksAsync(
                        new LastFmRecentTracksRequest(userName, Limit: 200, Page: page), cts.Token);
                    var result = response.ToBeatTrackListeningEvents();
                    allTracks.AddRange(result.Items.Where(static t => !t.IsNowPlaying));
                    break;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    if (attempt > maxRetries)
                    {
                        Console.Error.WriteLine(
                            $"  page {page} failed after {maxRetries} retries. " +
                            "Last.fm may be temporarily unavailable — try again later.");
                        return allTracks;
                    }

                    var reason = ex is TaskCanceledException ? "timed out" : ex.Message;
                    var backoffSeconds = (int)Math.Pow(2, attempt);
                    Console.Error.WriteLine(
                        $"  page {page} failed: {reason} — retry {attempt}/{maxRetries} in {backoffSeconds}s...");
                    await Task.Delay(backoffSeconds * 1000, cancellationToken);
                }
            }

            if (page % 50 == 0 || page == totalPages)
            {
                Console.Error.WriteLine($"  page {page}/{totalPages} ({allTracks.Count:N0} scrobbles)");
            }
        }

        return allTracks;
    }

    /// <summary>
    /// Writes scrobbles in lastfmstats CSV format (compatible with LastFmStatsCsvReader).
    /// </summary>
    public static void WriteCsv(TextWriter writer, IEnumerable<BeatTrackListeningEvent> tracks, string userName)
    {
        writer.WriteLine($"Artist;Album;AlbumId;Track;Date#{userName}");

        foreach (var track in tracks)
        {
            if (track.ArtistName is null)
            {
                continue;
            }

            var artist = CsvQuote(track.ArtistName);
            var album = CsvQuote(track.AlbumName ?? "");
            var albumId = CsvQuote("");
            var trackName = CsvQuote(track.TrackName ?? "");

            // API returns seconds, CSV format uses milliseconds
            var dateMs = track.PlayedAtUnixTime is not null
                ? (track.PlayedAtUnixTime.Value * 1000).ToString()
                : "";
            var date = CsvQuote(dateMs);

            writer.WriteLine($"{artist};{album};{albumId};{trackName};{date}");
        }
    }

    private static string CsvQuote(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return $"\"{value}\"";
    }
}
