using Shelf.Core.Items;
using Shelf.Core.Relationships;

namespace BeatTrack.Core;

/// <summary>
/// User-declared favorite artists.
/// Backed by shelf's knowledge graph — items with "likes" relationships in the music domain.
/// Acts as seed artists for gap analysis even without listening history.
/// </summary>
public class UserFavorites
{
    private readonly ShelfItems _items;
    private readonly ShelfRelationships _relationships;

    public UserFavorites(ShelfItems items, ShelfRelationships relationships)
    {
        _items = items;
        _relationships = relationships;
    }

    public int Count => _relationships.GetByVerb(Verbs.Likes)
        .Count(r => IsMusicItem(r.SubjectId));

    public IReadOnlyList<(string CanonicalName, string DisplayName, string? Notes)> GetAll() =>
        _relationships.GetByVerb(Verbs.Likes)
            .Where(r => IsMusicItem(r.SubjectId))
            .Select(r =>
            {
                var item = _items.Get(r.SubjectId);
                var displayName = item?.Name ?? r.SubjectId;
                return (r.SubjectId, displayName, r.Reason);
            })
            .OrderBy(x => x.displayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<string> GetCanonicalNames() =>
        _relationships.GetByVerb(Verbs.Likes)
            .Where(r => IsMusicItem(r.SubjectId))
            .Select(r => r.SubjectId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool IsMusicItem(string id)
    {
        var item = _items.Get(id);
        return item is null || string.Equals(item.Domain, "music", StringComparison.OrdinalIgnoreCase);
    }
}
