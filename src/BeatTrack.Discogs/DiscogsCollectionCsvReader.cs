using System.Globalization;
using BeatTrack.Core;

namespace BeatTrack.Discogs;

public sealed class DiscogsCollectionCsvReader
{
    public IReadOnlyList<BeatTrackCollectionRelease> ParseCsv(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var releases = new List<BeatTrackCollectionRelease>();
        _ = reader.ReadLine(); // skip header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = CsvLineParser.ParseLine(line);
            if (columns.Count < 13)
            {
                continue;
            }

            var catalogNumber = NullIfWhiteSpace(columns[0]);
            var artist = NullIfWhiteSpace(columns[1]);
            var title = columns[2].Trim();
            var label = NullIfWhiteSpace(columns[3]);
            var formats = ParseFormats(columns[4]);
            var releasedYear = ParseYear(columns[6]);
            var releaseId = ParseInt(columns[7]);
            var folder = NullIfWhiteSpace(columns[8]);
            var dateAdded = ParseDateToUnix(columns[9]);
            var mediaCondition = NullIfWhiteSpace(columns[10]);
            var sleeveCondition = NullIfWhiteSpace(columns[11]);
            var notes = NullIfWhiteSpace(columns[12]);

            releases.Add(new BeatTrackCollectionRelease(
                Title: title,
                ArtistName: artist,
                Label: label,
                CatalogNumber: catalogNumber,
                Formats: formats,
                ReleasedYear: releasedYear,
                DiscogsReleaseId: releaseId,
                CollectionFolder: folder,
                DateAddedUnixTime: dateAdded,
                MediaCondition: mediaCondition,
                SleeveCondition: sleeveCondition,
                Notes: notes));
        }

        return releases;
    }

    private static IReadOnlyList<string> ParseFormats(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static int? ParseYear(string value)
    {
        if (int.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var year) && year > 0)
        {
            return year;
        }

        return null;
    }

    private static int? ParseInt(string value)
    {
        if (int.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }

    private static long? ParseDateToUnix(string value)
    {
        if (DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            return dto.ToUnixTimeSeconds();
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
