using BeatTrack.Core;

namespace BeatTrack.LastFm;

public static class HistoryFetcher
{
    /// <summary>
    /// Fetches scrobble history via the Last.fm API.
    /// When <paramref name="fromUnixTime"/> is set, only scrobbles after that timestamp are fetched.
    /// When <paramref name="maxPages"/> is set, stops after that many pages.
    /// Returns scrobbles in chronological order (oldest first).
    /// </summary>
    public static async Task<IReadOnlyList<LastFmScrobble>> FetchAsync(
        ILastFmClient client, string userName, long? fromUnixTime = null, int? maxPages = null, CancellationToken cancellationToken = default)
    {
        // First request: get total pages
        var firstResponse = await client.GetRecentTracksAsync(
            new LastFmRecentTracksRequest(userName, Limit: 200, Page: 1, FromUnixTime: fromUnixTime), cancellationToken);
        var firstPage = firstResponse.ToBeatTrackListeningEvents();

        var total = firstPage.Total ?? 0;
        const int pageSize = 200;
        var totalPages = firstPage.TotalPages ?? (total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 1);
        var pagesToFetch = maxPages is not null ? Math.Min(maxPages.Value, totalPages) : totalPages;
        Console.Error.WriteLine($"Fetching {(maxPages is not null ? $"up to {pagesToFetch}" : $"{totalPages:N0}")} pages ({total:N0} available)...");

        var allTracks = new List<LastFmScrobble>(pagesToFetch * pageSize);
        allTracks.AddRange(ToScrobbles(firstPage.Items));

        for (var page = 2; page <= pagesToFetch; page++)
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
                        new LastFmRecentTracksRequest(userName, Limit: 200, Page: page, FromUnixTime: fromUnixTime), cts.Token);
                    var result = response.ToBeatTrackListeningEvents();
                    allTracks.AddRange(ToScrobbles(result.Items));
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

        // API returns newest-first; reverse to chronological order (oldest first)
        allTracks.Reverse();
        return allTracks;
    }

    private static IEnumerable<LastFmScrobble> ToScrobbles(IEnumerable<BeatTrackListeningEvent> events) =>
        events
            .Where(static t => !t.IsNowPlaying && t.ArtistName is not null)
            .Select(static t => new LastFmScrobble(
                t.ArtistName!,
                t.AlbumName,
                t.TrackName,
                (t.PlayedAtUnixTime ?? 0) * 1000));
}
