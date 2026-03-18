using BeatTrack.Core.SpokenData;

namespace BeatTrack.Core;

/// <summary>
/// User-declared favorite artists stored as a markdown table.
/// Acts as seed artists for gap analysis even without listening history.
///
/// Format:
///   | artist | notes |
///   | --- | --- |
///   | Slowdive | |
///   | Boards of Canada | especially older albums |
/// </summary>
public class UserFavorites
{
    private readonly SpokenDataStore _store;

    public UserFavorites(string filePath)
    {
        _store = new SpokenDataStore(filePath, MusicSpokenSchemas.Favorites, BeatTrackAnalysis.CanonicalizeArtistName);
    }

    public int Count => _store.Count;

    public IReadOnlyList<(string CanonicalName, string DisplayName, string? Notes)> GetAll() =>
        _store.GetAll()
            .Select(static e => (e.CanonicalSubject, e.DisplaySubject, e.Reason))
            .ToList();

    /// <summary>
    /// Gets canonical names of all favorite artists.
    /// </summary>
    public IReadOnlyList<string> GetCanonicalNames() =>
        _store.GetAllCanonicalSubjects();
}
