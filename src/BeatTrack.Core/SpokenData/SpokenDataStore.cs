namespace BeatTrack.Core.SpokenData;

/// <summary>
/// Domain-agnostic store for user-voiced preference data backed by a markdown table.
/// Stores display names as written; computes canonical forms via a pluggable canonicalizer.
/// </summary>
public sealed class SpokenDataStore
{
    private readonly string _filePath;
    private readonly SpokenDataSchema _schema;
    private readonly Func<string, string> _canonicalize;
    private readonly Dictionary<string, List<SpokenEntry>> _entries = new(StringComparer.OrdinalIgnoreCase);

    public SpokenDataStore(string filePath, SpokenDataSchema schema, Func<string, string> canonicalize)
    {
        _filePath = filePath;
        _schema = schema;
        _canonicalize = canonicalize;
        Load();
    }

    public int Count => _entries.Values.Sum(static list => list.Count);

    public bool ContainsSubject(string name)
    {
        var canonical = _canonicalize(name);
        return _entries.ContainsKey(canonical);
    }

    public IReadOnlyList<SpokenEntry> GetBySubject(string name)
    {
        var canonical = _canonicalize(name);
        return _entries.TryGetValue(canonical, out var list) ? list : [];
    }

    public IReadOnlyList<SpokenEntry> GetAll()
    {
        return _entries.Values.SelectMany(static list => list).ToList();
    }

    public IReadOnlyList<string> GetAllCanonicalSubjects()
    {
        return [.. _entries.Keys];
    }

    public void Add(SpokenEntry entry)
    {
        if (!_entries.TryGetValue(entry.CanonicalSubject, out var list))
        {
            list = [];
            _entries[entry.CanonicalSubject] = list;
        }

        list.Add(entry);
    }

    public bool Remove(string canonicalSubject)
    {
        return _entries.Remove(canonicalSubject);
    }

    public bool RemoveEntry(string canonicalSubject, string? canonicalTarget)
    {
        if (!_entries.TryGetValue(canonicalSubject, out var list))
        {
            return false;
        }

        var removed = list.RemoveAll(e =>
            string.Equals(e.CanonicalTarget, canonicalTarget, StringComparison.OrdinalIgnoreCase)) > 0;

        if (list.Count == 0)
        {
            _entries.Remove(canonicalSubject);
        }

        return removed;
    }

    public void Save()
    {
        var rows = _entries.Values
            .SelectMany(static list => list)
            .OrderBy(e => e.DisplaySubject, StringComparer.OrdinalIgnoreCase)
            .Select(EntryToRow);

        MarkdownTableStore.Write(_filePath, _schema.Headers, rows);
    }

    private void Load()
    {
        var (_, rows) = MarkdownTableStore.Read(_filePath);
        foreach (var row in rows)
        {
            if (row.Length <= _schema.SubjectColumn)
            {
                continue;
            }

            var displaySubject = row[_schema.SubjectColumn];
            if (string.IsNullOrWhiteSpace(displaySubject))
            {
                continue;
            }

            var canonicalSubject = _canonicalize(displaySubject);

            string? displayTarget = null;
            string? canonicalTarget = null;
            if (_schema.TargetColumn >= 0 && row.Length > _schema.TargetColumn
                && !string.IsNullOrWhiteSpace(row[_schema.TargetColumn]))
            {
                displayTarget = row[_schema.TargetColumn];
                canonicalTarget = _canonicalize(displayTarget);
            }

            var reason = _schema.ReasonColumn >= 0 && row.Length > _schema.ReasonColumn
                && !string.IsNullOrWhiteSpace(row[_schema.ReasonColumn])
                ? row[_schema.ReasonColumn]
                : null;

            var dateAdded = _schema.DateAddedColumn >= 0 && row.Length > _schema.DateAddedColumn
                && !string.IsNullOrWhiteSpace(row[_schema.DateAddedColumn])
                ? row[_schema.DateAddedColumn]
                : null;

            var entry = new SpokenEntry(canonicalSubject, displaySubject, canonicalTarget, displayTarget, reason, dateAdded);

            if (!_entries.TryGetValue(canonicalSubject, out var list))
            {
                list = [];
                _entries[canonicalSubject] = list;
            }

            list.Add(entry);
        }
    }

    private string[] EntryToRow(SpokenEntry entry)
    {
        var row = new string[_schema.Headers.Length];
        Array.Fill(row, "");

        row[_schema.SubjectColumn] = entry.DisplaySubject;

        if (_schema.TargetColumn >= 0)
        {
            row[_schema.TargetColumn] = entry.DisplayTarget ?? "";
        }

        if (_schema.ReasonColumn >= 0)
        {
            row[_schema.ReasonColumn] = entry.Reason ?? "";
        }

        if (_schema.DateAddedColumn >= 0)
        {
            row[_schema.DateAddedColumn] = entry.DateAdded ?? "";
        }

        return row;
    }
}
