namespace BeatTrack.Core;

/// <summary>
/// Manages a list of artists the user has tried and doesn't connect with.
/// Stored as a markdown table at ~/.local/share/beat-track/known-misses.md.
/// These artists are excluded from recommendations (gaps, strange absences, re-engagement).
/// </summary>
public class KnownMisses
{
    private readonly string _filePath;
    private readonly Dictionary<string, (string DisplayName, string? Reason, string DateAdded)> _entries = new(StringComparer.OrdinalIgnoreCase);

    public KnownMisses(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public int Count => _entries.Count;

    public bool Contains(string artistName)
    {
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artistName);
        return _entries.ContainsKey(canonical);
    }

    public void Add(string artistName, string? reason = null)
    {
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artistName);
        _entries[canonical] = (artistName, reason, DateTime.Now.ToString("yyyy-MM-dd"));
    }

    public bool Remove(string artistName)
    {
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artistName);
        return _entries.Remove(canonical);
    }

    public IReadOnlyList<(string CanonicalName, string DisplayName, string? Reason, string DateAdded)> GetAll()
    {
        return _entries
            .Select(kvp => (kvp.Key, kvp.Value.DisplayName, kvp.Value.Reason, kvp.Value.DateAdded))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Save()
    {
        string[] headers = ["artist", "reason", "date_added"];
        var rows = _entries
            .OrderBy(kvp => kvp.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new[] { kvp.Value.DisplayName, kvp.Value.Reason ?? "", kvp.Value.DateAdded });
        MarkdownTableStore.Write(_filePath, headers, rows);
    }

    private void Load()
    {
        var (_, rows) = MarkdownTableStore.Read(_filePath);
        foreach (var row in rows)
        {
            if (row.Length >= 1 && !string.IsNullOrWhiteSpace(row[0]))
            {
                var canonical = BeatTrackAnalysis.CanonicalizeArtistName(row[0]);
                var reason = row.Length > 1 && !string.IsNullOrWhiteSpace(row[1]) ? row[1] : null;
                var dateAdded = row.Length > 2 ? row[2] : "";
                _entries[canonical] = (row[0], reason, dateAdded);
            }
        }
    }
}
