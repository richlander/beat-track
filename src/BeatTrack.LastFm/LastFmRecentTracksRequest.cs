namespace BeatTrack.LastFm;

public sealed record LastFmRecentTracksRequest(
    string UserName,
    int? Limit = null,
    int? Page = null,
    long? FromUnixTime = null,
    long? ToUnixTime = null,
    bool IncludeExtendedData = false);
