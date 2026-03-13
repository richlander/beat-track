using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BeatTrack.Core;

namespace BeatTrack.YouTube;

public sealed class YouTubeTakeoutClient
{
    private static readonly Regex WatchEventRegex = new(
        "(?s)(Watched|Viewed)\\s*<a href=\"(?<url>[^\"]+)\">(?<title>.*?)</a>(?:<br><a href=\"[^\"]+\">(?<channel>.*?)</a>)?<br>(?<date>[^<]+)<br>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<BeatTrackSavedTrack> ParseMusicLibrarySongs(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var items = new List<BeatTrackSavedTrack>();
        _ = reader.ReadLine(); // header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = ParseCsvLine(line);
            if (columns.Count < 5)
            {
                continue;
            }

            var videoId = NullIfWhiteSpace(columns[0]);
            var title = columns[1];
            var album = NullIfWhiteSpace(columns[2]);
            var artists = columns.Skip(3).Where(static x => !string.IsNullOrWhiteSpace(x)).ToArray();

            items.Add(new BeatTrackSavedTrack(
                Title: title,
                AlbumTitle: album,
                ArtistNames: artists,
                SourceId: videoId,
                SourceUrl: videoId is null ? null : $"https://www.youtube.com/watch?v={videoId}"));
        }

        return items;
    }

    public IReadOnlyList<BeatTrackWatchEvent> ParseWatchHistoryHtml(string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var matches = WatchEventRegex.Matches(html);
        var items = new List<BeatTrackWatchEvent>(matches.Count);

        foreach (Match match in matches)
        {
            var title = Decode(match.Groups["title"].Value);
            var channel = NullIfWhiteSpace(Decode(match.Groups["channel"].Value));
            var url = Decode(match.Groups["url"].Value);
            var dateText = Decode(match.Groups["date"].Value);
            var kind = match.Groups[1].Value;

            items.Add(new BeatTrackWatchEvent(
                Title: title,
                ChannelName: channel,
                Url: url,
                WatchedAtUnixTime: ParseDate(dateText),
                Product: "YouTube",
                IsMusicCandidate: IsLikelyMusic(title, channel),
                EventKind: kind));
        }

        return items;
    }

    private static bool IsLikelyMusic(string title, string? channel)
    {
        var titleText = title.AsSpan();
        var channelText = channel ?? string.Empty;
        var haystack = $"{title} {channelText}";

        if (ContainsAny(haystack, ["cbc news", "abc news", "ezra klein", "meidastouch", "dotnet", "hanselman", "veritasium", "doctor who", "sherlock", "anthropic", "pentagon", "trump", "iran", "devops"]))
        {
            return false;
        }

        var strongPositive = ContainsAny(haystack, [" - topic", "vevo", "official video", "official audio", "visualizer", "visualiser", "remix", "album stream", "full album", "kexp", "cercle", "boiler room", "tiny desk", "concert", "acoustic session"]);
        var mediumPositive = ContainsAny(haystack, ["live at", "lyrics", "lyric video", "session", "feat.", " ft. ", "featuring "]);
        var structuralPositive = LooksLikeArtistTrackPattern(title) && !ContainsAny(haystack, ["the ezra klein show", "abc news", "cbc news", "saturday night live", "monocle", "devops toolbox"]);

        if (strongPositive)
        {
            return true;
        }

        if (mediumPositive && structuralPositive)
        {
            return true;
        }

        if (channelText.Contains("topic", StringComparison.OrdinalIgnoreCase)
            || channelText.Contains("vevo", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return structuralPositive && LooksArtistish(channelText);
    }

    private static bool LooksLikeArtistTrackPattern(string value) =>
        value.Contains(" - ", StringComparison.Ordinal) || value.Contains(" – ", StringComparison.Ordinal);

    private static bool LooksArtistish(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !ContainsAny(value, ["news", "show", "podcast", "toolbox", "films", "in-depth", "background", "clips", "official channel"]);
    }

    private static bool ContainsAny(string value, string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static long? ParseDate(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return parsed.ToUnixTimeSeconds();
        }

        return null;
    }

    private static string Decode(string value) => WebUtility.HtmlDecode(value).Trim();

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> ParseCsvLine(string line)
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
            else if (ch == ',' && !inQuotes)
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
}
