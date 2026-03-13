namespace BeatTrack.Core;

/// <summary>
/// Reads and writes simple markdown pipe tables for flat key-value caches.
/// Format is GitHub-compatible:
///
///   | column1 | column2 | column3 |
///   |---------|---------|---------|
///   | value1  | value2  | value3  |
///
/// Adapted from markout's TableParser.
/// </summary>
public static class MarkdownTableStore
{
    /// <summary>
    /// Reads a markdown table from a file. Returns headers and rows.
    /// Skips the separator row and trims whitespace from all cells.
    /// </summary>
    public static (string[] Headers, List<string[]> Rows) Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ([], []);
        }

        var lines = File.ReadAllLines(filePath);
        return Parse(lines);
    }

    /// <summary>
    /// Reads a markdown table from a TextReader.
    /// </summary>
    public static (string[] Headers, List<string[]> Rows) Read(TextReader reader)
    {
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        return Parse([.. lines]);
    }

    /// <summary>
    /// Parses markdown table lines into headers and rows.
    /// </summary>
    public static (string[] Headers, List<string[]> Rows) Parse(string[] lines)
    {
        if (lines.Length < 2)
        {
            return ([], []);
        }

        // Skip leading blank lines or non-table lines
        var start = 0;
        while (start < lines.Length && !lines[start].Contains('|'))
        {
            start++;
        }

        if (start + 1 >= lines.Length)
        {
            return ([], []);
        }

        var headers = ParseRow(lines[start]);
        if (headers.Length == 0)
        {
            return ([], []);
        }

        // Skip separator row (line after header)
        var dataStart = start + 2;

        var rows = new List<string[]>();
        for (var i = dataStart; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || !lines[i].Contains('|'))
            {
                continue;
            }

            var row = ParseRow(lines[i]);
            if (row.Length > 0)
            {
                rows.Add(row);
            }
        }

        return (headers, rows);
    }

    /// <summary>
    /// Writes a markdown table to a file. Creates or overwrites the file.
    /// </summary>
    public static void Write(string filePath, string[] headers, IEnumerable<string[]> rows)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var writer = new StreamWriter(filePath);
        WriteTable(writer, headers, rows);
    }

    /// <summary>
    /// Writes a markdown table to a TextWriter.
    /// </summary>
    public static void WriteTable(TextWriter writer, string[] headers, IEnumerable<string[]> rows)
    {
        // Header row
        writer.Write("| ");
        writer.Write(string.Join(" | ", headers));
        writer.WriteLine(" |");

        // Separator row
        writer.Write("| ");
        writer.Write(string.Join(" | ", headers.Select(static _ => "---")));
        writer.WriteLine(" |");

        // Data rows
        foreach (var row in rows)
        {
            writer.Write("| ");
            writer.Write(string.Join(" | ", row));
            writer.WriteLine(" |");
        }
    }

    /// <summary>
    /// Reads a table into a dictionary keyed by the first column.
    /// Useful for simple key-value caches.
    /// </summary>
    public static Dictionary<string, string[]> ReadAsDictionary(string filePath, StringComparer? comparer = null)
    {
        var (headers, rows) = Read(filePath);
        var dict = new Dictionary<string, string[]>(comparer ?? StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.Length > 0 && !string.IsNullOrWhiteSpace(row[0]))
            {
                dict.TryAdd(row[0], row);
            }
        }

        return dict;
    }

    private static string[] ParseRow(string line)
    {
        var trimmed = line.AsSpan().Trim();

        if (trimmed.Length == 0 || !trimmed.Contains('|'))
        {
            return [];
        }

        // Strip leading/trailing pipes
        if (trimmed.StartsWith("|"))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith("|"))
        {
            trimmed = trimmed[..^1];
        }

        var parts = trimmed.ToString().Split('|');
        var cells = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            cells[i] = parts[i].Trim();
        }

        return cells;
    }
}
