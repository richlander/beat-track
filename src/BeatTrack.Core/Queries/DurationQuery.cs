using BeatTrack.Core.Views;
using Markout;

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

        // Show what common windows represent
        int[] windowDays = [7, 30, 90, 365];
        var windows = windowDays
            .TakeWhile(w => w < spanDays)
            .Select(w => new WindowPercentageRow
            {
                Window = $"{w}d",
                Percentage = $"{100.0 * w / spanDays:F1}%",
            })
            .ToList();

        var view = new DurationView
        {
            From = $"{firstDate:yyyy-MM-dd}",
            To = $"{lastDate:yyyy-MM-dd}",
            Span = $"{duration} ({spanDays:N0} days)",
            ActiveDays = $"{activeDays:N0} ({100.0 * activeDays / spanDays:F1}%)",
            Windows = windows.Count > 0 ? windows : null,
        };

        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
    }
}
