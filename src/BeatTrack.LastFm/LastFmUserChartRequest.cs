namespace BeatTrack.LastFm;

public sealed record LastFmUserChartRequest(
    string UserName,
    LastFmTimePeriod Period = LastFmTimePeriod.Overall,
    int? Limit = null,
    int? Page = null);
