namespace BeatTrack.Core.Queries;

/// <summary>
/// Shows the total duration of listening history as years, months, and days.
/// Helps orient how much a given time window represents of the overall history.
/// </summary>
public static class DurationQuery
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
        var spanDays = (lastDate.Date - firstDate.Date).Days + 1;

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

        // Active days for coverage calculation
        var activeDays = timed
            .Select(static s => DateTimeOffset.FromUnixTimeMilliseconds(s.TimestampMs).ToLocalTime().Date)
            .Distinct()
            .Count();

        Console.WriteLine("listening_duration:");
        Console.WriteLine($"  from: {firstDate:yyyy-MM-dd}");
        Console.WriteLine($"  to: {lastDate:yyyy-MM-dd}");
        Console.WriteLine($"  span: {duration} ({spanDays:N0} days)");
        Console.WriteLine($"  active_days: {activeDays:N0} ({100.0 * activeDays / spanDays:F1}%)");
        Console.WriteLine();
        Console.WriteLine("  windows_as_percentage:");

        // Show what common windows represent
        int[] windowDays = [7, 30, 90, 365];
        foreach (var w in windowDays)
        {
            if (w >= spanDays) break;
            Console.WriteLine($"    {w}d: {100.0 * w / spanDays:F1}% of history");
        }

        return 0;
    }
}
