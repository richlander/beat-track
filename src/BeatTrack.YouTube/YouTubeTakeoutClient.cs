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

            var columns = CsvLineParser.ParseLine(line);
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

    public IReadOnlyList<BeatTrackWatchEvent> ParseWatchHistoryHtml(string html, MusicClassifier? classifier = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        classifier ??= new MusicClassifier([]);

        var matches = WatchEventRegex.Matches(html);
        var items = new List<BeatTrackWatchEvent>(matches.Count);

        foreach (Match match in matches)
        {
            var title = Decode(match.Groups["title"].Value);
            var channel = NullIfWhiteSpace(Decode(match.Groups["channel"].Value));
            var url = Decode(match.Groups["url"].Value);
            var dateText = Decode(match.Groups["date"].Value);
            var kind = match.Groups[1].Value;

            var classification = classifier.Classify(title, channel);

            items.Add(new BeatTrackWatchEvent(
                Title: title,
                ChannelName: channel,
                Url: url,
                WatchedAtUnixTime: ParseDate(dateText),
                Product: "YouTube",
                IsMusicCandidate: classification.IsMusicCandidate,
                MusicMatchReason: classification.MatchReason,
                EventKind: kind));
        }

        return items;
    }

    private static readonly Dictionary<string, string> TimezoneAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PDT"] = "-07:00", ["PST"] = "-08:00",
        ["MDT"] = "-06:00", ["MST"] = "-07:00",
        ["CDT"] = "-05:00", ["CST"] = "-06:00",
        ["EDT"] = "-04:00", ["EST"] = "-05:00",
        ["UTC"] = "+00:00", ["GMT"] = "+00:00",
        ["BST"] = "+01:00", ["CET"] = "+01:00", ["CEST"] = "+02:00",
        ["IST"] = "+05:30", ["JST"] = "+09:00", ["AEST"] = "+10:00",
    };

    private static long? ParseDate(string value)
    {
        // Try direct parse first
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return parsed.ToUnixTimeSeconds();
        }

        // Replace common timezone abbreviations with UTC offsets
        // e.g. "Mar 10, 2026, 3:06:30 PM PDT" → "Mar 10, 2026, 3:06:30 PM -07:00"
        var lastSpace = value.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            var tzAbbrev = value[(lastSpace + 1)..].Trim();
            if (TimezoneAbbreviations.TryGetValue(tzAbbrev, out var offset))
            {
                var replaced = string.Concat(value.AsSpan(0, lastSpace + 1), offset);
                if (DateTimeOffset.TryParse(replaced, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                {
                    return parsed.ToUnixTimeSeconds();
                }
            }
        }

        return null;
    }

    private static string Decode(string value) => WebUtility.HtmlDecode(value).Trim();

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
