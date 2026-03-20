using Shelf.Core.Items;
using Shelf.Core.Relationships;

namespace BeatTrack.Core;

/// <summary>
/// User-defined artist similarity graph.
/// Backed by shelf's knowledge graph — "similar-to" relationships in the music domain.
/// Supplements or overrides service-derived similarity data (ListenBrainz).
/// </summary>
public class UserSimilarArtists
{
    private readonly Dictionary<string, List<string>> _graph = new(StringComparer.OrdinalIgnoreCase);

    public UserSimilarArtists(ItemStore items, RelationshipStore relationships)
    {
        foreach (var rel in relationships.GetByVerb(Verbs.SimilarTo))
        {
            if (rel.TargetId is null) continue;

            // Only include music-domain items
            var subjectItem = items.Get(rel.SubjectId);
            if (subjectItem is not null && !string.Equals(subjectItem.Domain, "music", StringComparison.OrdinalIgnoreCase))
                continue;

            // Shelf stores canonical IDs; map back to canonical artist names
            // The ID format is the canonical name (slugified)
            AddEdge(rel.SubjectId, rel.TargetId);
            AddEdge(rel.TargetId, rel.SubjectId);
        }
    }

    public int Count => _graph.Count;

    public IReadOnlyList<string> GetSimilar(string canonicalArtistName)
    {
        // Try both the raw canonical name and the shelf-style ID
        if (_graph.TryGetValue(canonicalArtistName, out var list))
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var shelfId = ItemStore.Canonicalize(canonicalArtistName);
        if (_graph.TryGetValue(shelfId, out list))
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return [];
    }

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
