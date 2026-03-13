namespace BeatTrack.Core;

public sealed record BeatTrackSavedTrack(
    string Title,
    string? AlbumTitle,
    IReadOnlyList<string> ArtistNames,
    string? SourceId,
    string? SourceUrl);

public sealed record BeatTrackWatchEvent(
    string Title,
    string? ChannelName,
    string? Url,
    long? WatchedAtUnixTime,
    string Product,
    bool IsMusicCandidate,
    string? MusicMatchReason,
    string EventKind);

public sealed record BeatTrackYouTubeSnapshot(
    DateTimeOffset FetchedAt,
    IReadOnlyList<BeatTrackSavedTrack> SavedTracks,
    IReadOnlyList<BeatTrackWatchEvent> WatchEvents);
