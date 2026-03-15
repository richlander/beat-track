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
    private readonly List<(string CanonicalName, string DisplayName, string? Notes)> _favorites = [];

    public UserFavorites(string filePath)
    {
        var (_, rows) = MarkdownTableStore.Read(filePath);
        foreach (var row in rows)
        {
            if (row.Length >= 1 && !string.IsNullOrWhiteSpace(row[0]))
            {
                var canonical = BeatTrackAnalysis.CanonicalizeArtistName(row[0]);
                var notes = row.Length > 1 && !string.IsNullOrWhiteSpace(row[1]) ? row[1] : null;
                _favorites.Add((canonical, row[0], notes));
            }
        }
    }

    public int Count => _favorites.Count;

    public IReadOnlyList<(string CanonicalName, string DisplayName, string? Notes)> GetAll() => _favorites;

    /// <summary>
    /// Gets canonical names of all favorite artists.
    /// </summary>
    public IReadOnlyList<string> GetCanonicalNames() =>
        _favorites.Select(static f => f.CanonicalName).ToList();
}
