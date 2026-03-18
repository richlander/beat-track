using BeatTrack.Core.SpokenData;

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
        var store = new SpokenDataStore(filePath, MusicSpokenSchemas.SimilarArtists, BeatTrackAnalysis.CanonicalizeArtistName);

        foreach (var entry in store.GetAll())
        {
            if (entry.CanonicalTarget is null)
            {
                continue;
            }

            AddEdge(entry.CanonicalSubject, entry.CanonicalTarget);
            AddEdge(entry.CanonicalTarget, entry.CanonicalSubject);
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

    private void AddEdge(string from, string to)
    {
        if (!_graph.TryGetValue(from, out var list))
        {
            list = [];
            _graph[from] = list;
        }

        list.Add(to);
    }
}
