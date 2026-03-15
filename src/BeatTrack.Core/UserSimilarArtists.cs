namespace BeatTrack.Core;

/// <summary>
/// User-defined artist similarity graph stored as a markdown table.
/// Supplements or overrides service-derived similarity data (ListenBrainz).
/// Especially valuable for users without long listening histories.
///
/// Format:
///   | artist | similar_to |
///   | --- | --- |
///   | Slowdive | My Bloody Valentine |
///   | Slowdive | Ride |
///   | Boards of Canada | Aphex Twin |
/// </summary>
public class UserSimilarArtists
{
    private readonly Dictionary<string, List<string>> _graph = new(StringComparer.OrdinalIgnoreCase);

    public UserSimilarArtists(string filePath)
    {
        var (_, rows) = MarkdownTableStore.Read(filePath);
        foreach (var row in rows)
        {
            if (row.Length >= 2 && !string.IsNullOrWhiteSpace(row[0]) && !string.IsNullOrWhiteSpace(row[1]))
            {
                var artist = BeatTrackAnalysis.CanonicalizeArtistName(row[0]);
                var similarTo = BeatTrackAnalysis.CanonicalizeArtistName(row[1]);

                if (!_graph.TryGetValue(artist, out var list))
                {
                    list = [];
                    _graph[artist] = list;
                }
                list.Add(similarTo);

                // Bidirectional: if A is similar to B, B is similar to A
                if (!_graph.TryGetValue(similarTo, out var reverseList))
                {
                    reverseList = [];
                    _graph[similarTo] = reverseList;
                }
                reverseList.Add(artist);
            }
        }
    }

    public int Count => _graph.Count;

    /// <summary>
    /// Gets all artists similar to the given artist (canonical name).
    /// </summary>
    public IReadOnlyList<string> GetSimilar(string canonicalArtistName)
    {
        return _graph.TryGetValue(canonicalArtistName, out var list)
            ? list.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : [];
    }

    /// <summary>
    /// Gets all artists that have user-defined similarities.
    /// </summary>
    public IReadOnlyList<string> GetAllArtists() => [.. _graph.Keys];
}
