namespace BeatTrack.LastFm;

public sealed record LastFmPagedUserRequest(
    string UserName,
    int? Limit = null,
    int? Page = null);
