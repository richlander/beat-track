using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeatTrack.Core;

public sealed record LastFmScrobble(
    string ArtistName,
    string? Album,
    string? Track,
    long TimestampMs);

/// <summary>
/// Reads and writes scrobbles in JSONL format (one JSON object per line).
/// </summary>
public static class ScrobbleStore
{
    public static IReadOnlyList<LastFmScrobble> Load(string path)
    {
        if (!File.Exists(path))
            return [];

        using var reader = File.OpenText(path);
        return Load(reader);
    }

    public static IReadOnlyList<LastFmScrobble> Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var scrobbles = new List<LastFmScrobble>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var scrobble = JsonSerializer.Deserialize(line, ScrobbleJsonContext.Default.LastFmScrobble);
            if (scrobble is not null)
                scrobbles.Add(scrobble);
        }

        return scrobbles;
    }

    /// <summary>
    /// Returns the timestamp of the last line in the file (the most recent scrobble
    /// when the file is in chronological order). Returns null if the file is empty or missing.
    /// </summary>
    public static long? LatestTimestampMs(string path)
    {
        if (!File.Exists(path))
            return null;

        string? lastLine = null;
        foreach (var line in File.ReadLines(path))
        {
            if (!string.IsNullOrWhiteSpace(line))
                lastLine = line;
        }

        if (lastLine is null)
            return null;

        var scrobble = JsonSerializer.Deserialize(lastLine, ScrobbleJsonContext.Default.LastFmScrobble);
        return scrobble?.TimestampMs;
    }

    public static void Write(string path, IEnumerable<LastFmScrobble> scrobbles)
    {
        using var writer = new StreamWriter(path, append: false);
        foreach (var scrobble in scrobbles)
            writer.WriteLine(JsonSerializer.Serialize(scrobble, ScrobbleJsonContext.Default.LastFmScrobble));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(LastFmScrobble))]
internal sealed partial class ScrobbleJsonContext : JsonSerializerContext;
