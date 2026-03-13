namespace BeatTrack.Core;

public enum BeatTrackSource
{
    LastFm,
    YouTube,
    Discogs,
}

public sealed record BeatTrackSourceReference(
    BeatTrackSource Source,
    string? SourceId,
    string? SourceUrl,
    string? OriginalName);

public sealed record BeatTrackCanonicalArtist(
    string CanonicalName,
    IReadOnlyList<BeatTrackSourceReference> Sources);

public sealed record BeatTrackCanonicalRelease(
    string Title,
    string? ArtistCanonicalName,
    int? Year,
    IReadOnlyList<BeatTrackSourceReference> Sources);

public sealed record BeatTrackUnifiedProfile(
    DateTimeOffset BuiltAt,
    IReadOnlyList<BeatTrackCanonicalArtist> Artists,
    IReadOnlyList<BeatTrackCanonicalRelease> Releases);
