namespace BeatTrack.Core;

/// <summary>
/// Persistent cache mapping canonical artist names to MusicBrainz MBIDs.
/// Stored as a markdown table for human readability and GitHub rendering.
/// </summary>
public sealed class MbidCache
{
    private static readonly string[] Headers = ["canonical_name", "mbid", "matched_name", "source"];

    private readonly Dictionary<string, MbidCacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _filePath;

    public int Count => _entries.Count;

    public MbidCache(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    /// <summary>
    /// Gets the MBID for a canonical artist name, or null if not cached.
    /// </summary>
    public string? GetMbid(string canonicalName)
    {
        return _entries.TryGetValue(canonicalName, out var entry) ? entry.Mbid : null;
    }

    /// <summary>
    /// Gets a cache entry, or null if not cached.
    /// </summary>
    public MbidCacheEntry? GetEntry(string canonicalName)
    {
        return _entries.TryGetValue(canonicalName, out var entry) ? entry : null;
    }

    /// <summary>
    /// Adds or updates an MBID mapping.
    /// </summary>
    public void Set(string canonicalName, string mbid, string? matchedName = null, string source = "musicbrainz")
    {
        _entries[canonicalName] = new MbidCacheEntry(canonicalName, mbid, matchedName ?? canonicalName, source);
    }

    /// <summary>
    /// Adds entries from a Last.fm snapshot (artists with MBIDs).
    /// </summary>
    public int AddFromLastFmSnapshot(BeatTrackSnapshot snapshot)
    {
        var added = 0;
        foreach (var (_, periodArtists) in snapshot.TopArtistsByPeriod)
        {
            foreach (var artist in periodArtists)
            {
                if (string.IsNullOrWhiteSpace(artist.Mbid) || string.IsNullOrWhiteSpace(artist.Name))
                {
                    continue;
                }

                var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artist.Name);
                if (!_entries.ContainsKey(canonical))
                {
                    _entries[canonical] = new MbidCacheEntry(canonical, artist.Mbid, artist.Name, "lastfm");
                    added++;
                }
            }
        }

        return added;
    }

    /// <summary>
    /// Returns all entries as a read-only collection.
    /// </summary>
    public IReadOnlyCollection<MbidCacheEntry> GetAll() => _entries.Values;

    /// <summary>
    /// Saves the cache to disk as a markdown table.
    /// </summary>
    public void Save()
    {
        var rows = _entries.Values
            .OrderBy(static e => e.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .Select(static e => new[] { e.CanonicalName, e.Mbid, e.MatchedName, e.Source });

        MarkdownTableStore.Write(_filePath, Headers, rows);
    }

    private void Load()
    {
        var dict = MarkdownTableStore.ReadAsDictionary(_filePath);
        foreach (var (key, row) in dict)
        {
            if (row.Length >= 4)
            {
                _entries[key] = new MbidCacheEntry(row[0], row[1], row[2], row[3]);
            }
            else if (row.Length >= 2)
            {
                _entries[key] = new MbidCacheEntry(row[0], row[1], row.Length > 2 ? row[2] : key, "unknown");
            }
        }
    }
}

public sealed record MbidCacheEntry(
    string CanonicalName,
    string Mbid,
    string MatchedName,
    string Source);
