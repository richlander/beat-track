using BeatTrack.Core.Views;
using Markout;

namespace BeatTrack.Core.Queries;

public static class StatsQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles)
    {
        var timed = scrobbles.Where(static s => s.TimestampMs > 0).ToList();

        if (timed.Count == 0)
        {
            Console.WriteLine("(no scrobbles with timestamps found)");
            return 0;
        }

        var firstTs = timed.Min(static s => s.TimestampMs);
        var lastTs = timed.Max(static s => s.TimestampMs);
        var firstDate = DateTimeOffset.FromUnixTimeMilliseconds(firstTs).ToLocalTime();
        var lastDate = DateTimeOffset.FromUnixTimeMilliseconds(lastTs).ToLocalTime();

        var artistGroups = timed.GroupBy(
            static s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName),
            StringComparer.OrdinalIgnoreCase);

        var uniqueArtists = artistGroups.Count();
        var oneHitWonders = artistGroups.Count(static g => g.Count() == 1);

        var uniqueAlbums = timed
            .Where(static s => !string.IsNullOrWhiteSpace(s.Album))
            .Select(s => (Artist: BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), Album: s.Album!.ToLowerInvariant()))
            .Distinct()
            .Count();

        var uniqueTracks = timed
            .Where(static s => !string.IsNullOrWhiteSpace(s.Track))
            .Select(s => (Artist: BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), Track: s.Track!.ToLowerInvariant()))
            .Distinct()
            .Count();

        // Group by date for daily stats
        var dailyCounts = timed
            .GroupBy(static s => DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().Date)
            .Select(static g => (Date: g.Key, Count: g.Count()))
            .OrderBy(static x => x.Date)
            .ToList();

        var activeDays = dailyCounts.Count;
        var spanDays = (lastDate.Date - firstDate.Date).Days + 1;
        var avgPerActiveDay = activeDays > 0 ? (double)timed.Count / activeDays : 0;

        // Most popular month/year/day-of-week
        var byMonth = timed
            .GroupBy(static s => DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().ToString("yyyy-MM"))
            .OrderByDescending(static g => g.Count())
            .First();

        var byYear = timed
            .GroupBy(static s => DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().Year)
            .OrderByDescending(static g => g.Count())
            .First();

        var byDow = timed
            .GroupBy(static s => DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().DayOfWeek)
            .OrderByDescending(static g => g.Count())
            .First();

        // Eddington number: largest E where you scrobbled >= E tracks on >= E days
        var sortedDailyCounts = dailyCounts.Select(static x => x.Count).OrderByDescending(static x => x).ToList();
        var eddington = 0;
        for (var i = 0; i < sortedDailyCounts.Count; i++)
        {
            if (sortedDailyCounts[i] >= i + 1)
            {
                eddington = i + 1;
            }
            else
            {
                break;
            }
        }

        var daysToNext = 0;
        var nextE = eddington + 1;
        var daysWithEnough = sortedDailyCounts.Count(c => c >= nextE);
        daysToNext = nextE - daysWithEnough;

        // Listening span breakdown
        var spanYears = lastDate.Year - firstDate.Year;
        var spanMonths = lastDate.Month - firstDate.Month;
        var spanRemainderDays = lastDate.Day - firstDate.Day;
        if (spanRemainderDays < 0)
        {
            spanMonths--;
            spanRemainderDays += DateTime.DaysInMonth(firstDate.Year + spanYears + (firstDate.Month + spanMonths - 1) / 12,
                (firstDate.Month + spanMonths - 1) % 12 + 1);
        }
        if (spanMonths < 0)
        {
            spanYears--;
            spanMonths += 12;
        }

        var durationParts = new List<string>();
        if (spanYears > 0) durationParts.Add($"{spanYears}y");
        if (spanMonths > 0) durationParts.Add($"{spanMonths}m");
        if (spanRemainderDays > 0 || durationParts.Count == 0) durationParts.Add($"{spanRemainderDays}d");
        var duration = string.Join(" ", durationParts);

        var firstScrobble = timed.First(s => s.TimestampMs == firstTs);
        var lastScrobble = timed.First(s => s.TimestampMs == lastTs);

        var view = new StatsView
        {
            FirstScrobble = $"{firstDate:yyyy-MM-dd} ({firstScrobble.ArtistName} - {firstScrobble.Track})",
            LastScrobble = $"{lastDate:yyyy-MM-dd} ({lastScrobble.ArtistName} - {lastScrobble.Track})",
            TotalScrobbles = timed.Count,
            UniqueArtists = uniqueArtists,
            UniqueAlbums = uniqueAlbums,
            UniqueTracks = uniqueTracks,
            OneHitWonders = $"{oneHitWonders:N0} ({100.0 * oneHitWonders / uniqueArtists:F1}%)",
            ListeningSpan = $"{spanDays:N0} days ({duration})",
            ActiveDays = $"{activeDays:N0} ({100.0 * activeDays / spanDays:F1}%)",
            AvgPerActiveDay = avgPerActiveDay,
            MostPopularMonth = $"{byMonth.Key} ({byMonth.Count():N0} scrobbles)",
            MostPopularYear = $"{byYear.Key} ({byYear.Count():N0} scrobbles)",
            BusiestDayOfWeek = $"{byDow.Key} ({byDow.Count():N0} scrobbles)",
            EddingtonNumber = eddington,
            DaysToNextEddington = daysToNext,
        };

        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
    }
}
