namespace BeatTrack.Core;

public sealed record BeatTrackUserProfile(
    string UserName,
    string? RealName,
    string? Url,
    long? RegisteredAtUnixTime,
    long? PlayCount,
    IReadOnlyList<BeatTrackImage> Images);

public sealed record BeatTrackListeningEvent(
    string TrackName,
    string? ArtistName,
    string? AlbumName,
    string? Url,
    long? PlayedAtUnixTime,
    bool IsNowPlaying,
    bool IsLoved,
    IReadOnlyList<BeatTrackImage> Images);

public sealed record BeatTrackArtistSummary(
    string Name,
    string? Url,
    string? Mbid,
    long? PlayCount,
    string? Period,
    IReadOnlyList<BeatTrackImage> Images);

public sealed record BeatTrackAlbumSummary(
    string Name,
    string? ArtistName,
    string? Url,
    string? Mbid,
    long? PlayCount,
    string? Period,
    IReadOnlyList<BeatTrackImage> Images);

public sealed record BeatTrackTrackSummary(
    string Name,
    string? ArtistName,
    string? Url,
    string? Mbid,
    long? PlayCount,
    string? Period,
    IReadOnlyList<BeatTrackImage> Images);

public sealed record BeatTrackPagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int? Page,
    int? PerPage,
    int? TotalPages,
    int? Total);

public sealed record BeatTrackImage(
    string? Size,
    string? Url);
