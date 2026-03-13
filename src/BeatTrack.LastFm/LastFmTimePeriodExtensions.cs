namespace BeatTrack.LastFm;

internal static class LastFmTimePeriodExtensions
{
    public static string ToApiValue(this LastFmTimePeriod period) =>
        period switch
        {
            LastFmTimePeriod.Overall => "overall",
            LastFmTimePeriod.SevenDays => "7day",
            LastFmTimePeriod.OneMonth => "1month",
            LastFmTimePeriod.ThreeMonths => "3month",
            LastFmTimePeriod.SixMonths => "6month",
            LastFmTimePeriod.TwelveMonths => "12month",
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
        };
}
