using BeatTrack.Core.SpokenData;

namespace BeatTrack.Core;

/// <summary>
/// Manages a list of artists the user has tried and doesn't connect with.
/// Stored as a markdown table at ~/.local/share/beat-track/known-misses.md.
/// These artists are excluded from recommendations (gaps, strange absences, re-engagement).
/// </summary>
public class KnownMisses
{
    private readonly SpokenDataStore _store;

    public KnownMisses(string filePath)
    {
        _store = new SpokenDataStore(filePath, MusicSpokenSchemas.KnownMisses, BeatTrackAnalysis.CanonicalizeArtistName);
    }

    public int Count => _store.Count;

    public bool Contains(string artistName)
    {
        return _store.ContainsSubject(artistName);
    }

    public void Add(string artistName, string? reason = null)
    {
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artistName);
        // Remove existing entry if present, then add updated one
        _store.Remove(canonical);
        _store.Add(new SpokenEntry(canonical, artistName, null, null, reason, DateTime.Now.ToString("yyyy-MM-dd")));
    }

    public bool Remove(string artistName)
    {
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artistName);
        return _store.Remove(canonical);
    }

    public IReadOnlyList<(string CanonicalName, string DisplayName, string? Reason, string DateAdded)> GetAll()
    {
        return _store.GetAll()
            .OrderBy(static e => e.DisplaySubject, StringComparer.OrdinalIgnoreCase)
            .Select(static e => (e.CanonicalSubject, e.DisplaySubject, e.Reason, e.DateAdded ?? ""))
            .ToList();
    }

    public void Save()
    {
        _store.Save();
    }
}
