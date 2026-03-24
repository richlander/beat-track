using System.Text.Json.Serialization.Metadata;
using System.Text.Json;

namespace BeatTrack.Core;

/// <summary>
/// Reads and writes records in JSONL format (one JSON object per line),
/// ordered by the supplied comparer.
/// </summary>
public sealed class JsonlStore<T>(JsonTypeInfo<T> jsonTypeInfo, IComparer<T> comparer)
{
    public IReadOnlyList<T> Load(string path)
    {
        if (!File.Exists(path))
            return [];

        using var reader = File.OpenText(path);
        return Load(reader);
    }

    public IReadOnlyList<T> Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var items = new List<T>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var item = JsonSerializer.Deserialize(line, jsonTypeInfo);
            if (item is not null)
                items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Returns the last record in the file (the latest item when the file
    /// is sorted by the comparer). Returns default if the file is empty or missing.
    /// </summary>
    public T? Last(string path)
    {
        if (!File.Exists(path))
            return default;

        string? lastLine = null;
        foreach (var line in File.ReadLines(path))
        {
            if (!string.IsNullOrWhiteSpace(line))
                lastLine = line;
        }

        if (lastLine is null)
            return default;

        return JsonSerializer.Deserialize(lastLine, jsonTypeInfo);
    }

    public IComparer<T> Comparer => comparer;

    public void Write(string path, IEnumerable<T> items)
    {
        using var writer = new StreamWriter(path, append: false);
        foreach (var item in items)
            writer.WriteLine(JsonSerializer.Serialize(item, jsonTypeInfo));
    }
}
