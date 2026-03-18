using BeatTrack.Core;

namespace BeatTrack.LastFm;

public sealed record SnapshotFetchOptions(
    int RecentPages = 3,
    int RecentPageSize = 200,
    int LovedPages = 2,
    int LovedPageSize = 100);

public static class SnapshotFetcher
{
    public static async Task<BeatTrackSnapshot> FetchAsync(
        ILastFmClient client, string userName, SnapshotFetchOptions? options = null)
    {
        var opts = options ?? new SnapshotFetchOptions();

        var profile = (await client.GetUserInfoAsync(userName)).ToBeatTrackUserProfile();
        var recentTracks = await FetchRecentTracksAsync(client, userName, opts.RecentPages, Math.Min(opts.RecentPageSize, 200));
        var lovedTracks = await FetchLovedTracksAsync(client, userName, opts.LovedPages, Math.Min(opts.LovedPageSize, 200));
        var topArtistsByPeriod = await FetchTopArtistsByPeriodAsync(client, userName);

        return new BeatTrackSnapshot(
            UserName: userName,
            FetchedAt: DateTimeOffset.UtcNow,
            Profile: profile,
            RecentTracks: recentTracks,
            TopArtistsByPeriod: topArtistsByPeriod,
            LovedTracks: lovedTracks);
    }

    private static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchRecentTracksAsync(
        ILastFmClient client, string userName, int pages, int pageSize)
    {
        var items = new List<BeatTrackListeningEvent>();
        for (var page = 1; page <= pages; page++)
        {
            var result = (await client.GetRecentTracksAsync(
                new LastFmRecentTracksRequest(userName, pageSize, page))).ToBeatTrackListeningEvents();
            items.AddRange(result.Items);
            if (result.Page >= result.TotalPages || result.Items.Count == 0)
            {
                break;
            }
        }

        return items;
    }

    private static async Task<IReadOnlyList<BeatTrackListeningEvent>> FetchLovedTracksAsync(
        ILastFmClient client, string userName, int pages, int pageSize)
    {
        var items = new List<BeatTrackListeningEvent>();
        for (var page = 1; page <= pages; page++)
        {
            var result = (await client.GetLovedTracksAsync(
                new LastFmPagedUserRequest(userName, pageSize, page))).ToBeatTrackLovedListeningEvents();
            items.AddRange(result.Items);
            if (result.Page >= result.TotalPages || result.Items.Count == 0)
            {
                break;
            }
        }

        return items;
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<BeatTrackArtistSummary>>> FetchTopArtistsByPeriodAsync(
        ILastFmClient client, string userName)
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
}
