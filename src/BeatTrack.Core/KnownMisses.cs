using Shelf.Core;
using Shelf.Core.Items;
using Shelf.Core.Relationships;

namespace BeatTrack.Core;

/// <summary>
/// Manages a list of artists the user has tried and doesn't connect with.
/// Backed by shelf's knowledge graph — items with "dislikes" relationships.
/// These artists are excluded from recommendations (gaps, strange absences, re-engagement).
/// </summary>
public class KnownMisses
{
    private readonly ItemStore _items;
    private readonly RelationshipStore _relationships;

    public KnownMisses(ItemStore items, RelationshipStore relationships)
    {
        _items = items;
        _relationships = relationships;
    }

    public int Count => _relationships.GetByVerb(Verbs.Dislikes)
        .Count(r => IsMusicItem(r.SubjectId));

    public bool Contains(string artistName)
    {
        var id = ItemStore.Canonicalize(artistName);
        return _relationships.GetBySubjectAndVerb(id, Verbs.Dislikes).Count > 0;
    }

    public void Add(string artistName, string? reason = null)
    {
        var (item, _) = _items.GetOrCreate(artistName, "artist", "music");

        // Remove existing dislike if present, then add updated one
        _relationships.RemoveBySubjectAndVerb(item.Id, Verbs.Dislikes);
        _relationships.Add(item.Id, Verbs.Dislikes, reason: reason);
    }

    public bool Remove(string artistName)
    {
        var id = ItemStore.Canonicalize(artistName);
        return _relationships.RemoveBySubjectAndVerb(id, Verbs.Dislikes) > 0;
    }

    public IReadOnlyList<(string CanonicalName, string DisplayName, string? Reason, string DateAdded)> GetAll()
    {
        return _relationships.GetByVerb(Verbs.Dislikes)
            .Where(r => IsMusicItem(r.SubjectId))
            .Select(r =>
            {
                var item = _items.Get(r.SubjectId);
                var displayName = item?.Name ?? r.SubjectId;
                return (r.SubjectId, displayName, r.Reason, r.DateAdded);
            })
            .OrderBy(x => x.displayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Save()
    {
        _items.Save();
        _relationships.Save();
    }

    private bool IsMusicItem(string id)
    {
        var item = _items.Get(id);
        return item is null || string.Equals(item.Domain, "music", StringComparison.OrdinalIgnoreCase);
    }
}
