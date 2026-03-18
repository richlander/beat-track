namespace BeatTrack.Core.SpokenData;

/// <summary>
/// Pre-built schemas for the three music domain spoken-data files.
/// </summary>
public static class MusicSpokenSchemas
{
    public static SpokenDataSchema Favorites { get; } = new(
        ["artist", "notes"], SubjectColumn: 0, ReasonColumn: 1);

    public static SpokenDataSchema KnownMisses { get; } = new(
        ["artist", "reason", "date_added"], SubjectColumn: 0, ReasonColumn: 1, DateAddedColumn: 2);

    public static SpokenDataSchema SimilarArtists { get; } = new(
        ["artist", "similar_to"], SubjectColumn: 0, TargetColumn: 1);
}
