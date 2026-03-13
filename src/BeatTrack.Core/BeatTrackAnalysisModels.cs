namespace BeatTrack.Core;

public sealed record BeatTrackArtistCount(
    string ArtistName,
    int Count);

public sealed record BeatTrackArtistDelta(
    string ArtistName,
    int RecentCount,
    long? OverallPlayCount,
    double SurgeScore);

public sealed record BeatTrackAliasCandidate(
    string CanonicalName,
    IReadOnlyList<string> Variants,
    int VariantCount);

public sealed record BeatTrackSnapshotAnalysis(
    IReadOnlyList<BeatTrackArtistCount> RecentTopArtists,
    IReadOnlyList<BeatTrackArtistDelta> RecentVsOverallSurge,
    IReadOnlyList<BeatTrackArtistCount> StableCoreArtists,
    IReadOnlyList<BeatTrackAliasCandidate> AliasCandidates);
