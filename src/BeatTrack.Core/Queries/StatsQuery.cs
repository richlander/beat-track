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

        Console.WriteLine("listening_stats:");
        Console.WriteLine($"  first_scrobble: {firstDate:yyyy-MM-dd} ({timed.First(s => s.TimestampMs == firstTs).ArtistName} - {timed.First(s => s.TimestampMs == firstTs).Track})");
        Console.WriteLine($"  last_scrobble: {lastDate:yyyy-MM-dd} ({timed.First(s => s.TimestampMs == lastTs).ArtistName} - {timed.First(s => s.TimestampMs == lastTs).Track})");
        Console.WriteLine($"  total_scrobbles: {timed.Count:N0}");
        Console.WriteLine($"  unique_artists: {uniqueArtists:N0}");
        Console.WriteLine($"  unique_albums: {uniqueAlbums:N0}");
        Console.WriteLine($"  unique_tracks: {uniqueTracks:N0}");
        Console.WriteLine($"  one_hit_wonders: {oneHitWonders:N0} ({100.0 * oneHitWonders / uniqueArtists:F1}%)");
        Console.WriteLine($"  listening_span: {spanDays:N0} days");
        Console.WriteLine($"  active_days: {activeDays:N0} ({100.0 * activeDays / spanDays:F1}%)");
        Console.WriteLine($"  avg_per_active_day: {avgPerActiveDay:F1}");
        Console.WriteLine($"  most_popular_month: {byMonth.Key} ({byMonth.Count():N0} scrobbles)");
        Console.WriteLine($"  most_popular_year: {byYear.Key} ({byYear.Count():N0} scrobbles)");
        Console.WriteLine($"  busiest_day_of_week: {byDow.Key} ({byDow.Count():N0} scrobbles)");
        Console.WriteLine($"  eddington_number: {eddington}");
        Console.WriteLine($"  days_to_next_eddington: {daysToNext}");

        return 0;
    }
}
