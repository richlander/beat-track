namespace BeatTrack.Core;

public sealed record BeatTrackCollectionRelease(
    string Title,
    string? ArtistName,
    string? Label,
    string? CatalogNumber,
    IReadOnlyList<string> Formats,
    int? ReleasedYear,
    int? DiscogsReleaseId,
    string? CollectionFolder,
    long? DateAddedUnixTime,
    string? MediaCondition,
    string? SleeveCondition,
    string? Notes);

public sealed record BeatTrackDiscogsSnapshot(
    DateTimeOffset FetchedAt,
    IReadOnlyList<BeatTrackCollectionRelease> Releases);
