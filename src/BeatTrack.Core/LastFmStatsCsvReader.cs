using System.Globalization;

namespace BeatTrack.Core;

public sealed record LastFmScrobble(
    string ArtistName,
    string? Album,
    string? Track,
    long TimestampMs);

public static class LastFmStatsCsvReader
{
    /// <summary>
    /// Parses the semicolon-delimited CSV exported by lastfm-stats-web.
    /// Format: Artist;Album;AlbumId;Track;Date (with epoch milliseconds)
    /// </summary>
    public static IReadOnlyList<LastFmScrobble> ParseCsv(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var scrobbles = new List<LastFmScrobble>();
        _ = reader.ReadLine(); // skip header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseSemicolonLine(line);
            if (fields.Count < 5)
            {
                continue;
            }

            var artist = Unquote(fields[0]);
            var album = NullIfEmpty(Unquote(fields[1]));
            var track = NullIfEmpty(Unquote(fields[3]));
            var dateStr = Unquote(fields[4]);

            if (string.IsNullOrWhiteSpace(artist))
            {
                continue;
            }

            long timestampMs = 0;
            if (!string.IsNullOrEmpty(dateStr))
            {
                long.TryParse(dateStr, CultureInfo.InvariantCulture, out timestampMs);
            }

            scrobbles.Add(new LastFmScrobble(artist, album, track, timestampMs));
        }

        return scrobbles;
    }

    private static List<string> ParseSemicolonLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ';' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        return value;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
