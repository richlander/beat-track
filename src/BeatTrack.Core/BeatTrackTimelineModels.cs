namespace BeatTrack.Core;

public sealed record BeatTrackTimeBucket(
    long BucketStartUnixTime,
    int TotalEvents,
    IReadOnlyList<BeatTrackArtistCount> TopArtists);

public sealed record BeatTrackArtistTimelinePoint(
    string ArtistName,
    long BucketStartUnixTime,
    int Count,
    int Rank);
