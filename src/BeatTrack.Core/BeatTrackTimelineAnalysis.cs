namespace BeatTrack.Core;

public static class BeatTrackTimelineAnalysis
{
    public static IReadOnlyList<BeatTrackTimeBucket> BuildArtistBuckets(
        IReadOnlyList<BeatTrackListeningEvent> events,
        int bucketSizeHours = 24,
        int topArtistCount = 10)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bucketSizeHours);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topArtistCount);

        var bucketSeconds = bucketSizeHours * 60L * 60L;

        return events
            .Where(static x => x.PlayedAtUnixTime is not null && !string.IsNullOrWhiteSpace(x.ArtistName))
            .GroupBy(x => AlignToBucket(x.PlayedAtUnixTime!.Value, bucketSeconds))
            .OrderBy(static g => g.Key)
            .Select(g => new BeatTrackTimeBucket(
                BucketStartUnixTime: g.Key,
                TotalEvents: g.Count(),
                TopArtists: g.GroupBy(x => x.ArtistName!, StringComparer.OrdinalIgnoreCase)
                    .Select(static artistGroup => new BeatTrackArtistCount(artistGroup.First().ArtistName!, artistGroup.Count()))
                    .OrderByDescending(static x => x.Count)
                    .ThenBy(static x => x.ArtistName, StringComparer.OrdinalIgnoreCase)
                    .Take(topArtistCount)
                    .ToArray()))
            .ToArray();
    }

    public static IReadOnlyList<BeatTrackArtistTimelinePoint> BuildArtistTimeline(
        IReadOnlyList<BeatTrackTimeBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        return buckets
            .SelectMany(bucket => bucket.TopArtists.Select((artist, index) => new BeatTrackArtistTimelinePoint(
                ArtistName: artist.ArtistName,
                BucketStartUnixTime: bucket.BucketStartUnixTime,
                Count: artist.Count,
                Rank: index + 1)))
            .OrderBy(static x => x.BucketStartUnixTime)
            .ThenBy(static x => x.Rank)
            .ToArray();
    }

    private static long AlignToBucket(long unixTime, long bucketSeconds) =>
        unixTime - (unixTime % bucketSeconds);
}
