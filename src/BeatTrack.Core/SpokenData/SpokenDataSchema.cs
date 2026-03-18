namespace BeatTrack.Core.SpokenData;

/// <summary>
/// Maps markdown table columns to <see cref="SpokenEntry"/> fields.
/// Column indices are zero-based. Use -1 to indicate a field has no column.
/// </summary>
public sealed record SpokenDataSchema(
    string[] Headers,
    int SubjectColumn,
    int TargetColumn = -1,
    int ReasonColumn = -1,
    int DateAddedColumn = -1);
