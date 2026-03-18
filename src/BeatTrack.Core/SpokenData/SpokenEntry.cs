namespace BeatTrack.Core.SpokenData;

/// <summary>
/// A single user-voiced preference entry. Domain-agnostic: works for music artists,
/// HN topics, GitHub repos, or any entity a user expresses opinions about.
/// </summary>
public sealed record SpokenEntry(
    string CanonicalSubject,
    string DisplaySubject,
    string? CanonicalTarget,
    string? DisplayTarget,
    string? Reason,
    string? DateAdded);
