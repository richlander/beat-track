namespace BeatTrack.Core;

public sealed record BeatTrackSnapshot(
    string UserName,
    DateTimeOffset FetchedAt,
    BeatTrackUserProfile Profile,
    IReadOnlyList<BeatTrackListeningEvent> RecentTracks,
    IReadOnlyDictionary<string, IReadOnlyList<BeatTrackArtistSummary>> TopArtistsByPeriod,
    IReadOnlyList<BeatTrackListeningEvent> LovedTracks);
