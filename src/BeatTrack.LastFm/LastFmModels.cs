using System.Text.Json.Serialization;

namespace BeatTrack.LastFm;

public sealed record LastFmErrorEnvelope(
    int? Error,
    string? Message);

public sealed record LastFmUserGetInfoResponse(
    LastFmUser User);

public sealed record LastFmUser(
    string Name,
    [property: JsonPropertyName("realname")] string? RealName,
    string? Url,
    LastFmRegisteredAt? Registered,
    [property: JsonPropertyName("playcount")] string? PlayCount,
    IReadOnlyList<LastFmImage>? Image);

public sealed record LastFmRegisteredAt(
    [property: JsonPropertyName("unixtime")] string? UnixTime);

public sealed record LastFmPlayCountContainer(
    [property: JsonPropertyName("#text")] string? Text);

public sealed record LastFmTextValue(
    [property: JsonPropertyName("#text")] string? Text);

public sealed record LastFmRecentTracksResponse(
    [property: JsonPropertyName("recenttracks")] LastFmRecentTracks RecentTracks);

public sealed record LastFmRecentTracks(
    IReadOnlyList<LastFmTrack> Track,
    [property: JsonPropertyName("@attr")] LastFmRecentTracksAttributes? Attributes);

public sealed record LastFmTrack(
    string Name,
    LastFmTrackArtist Artist,
    LastFmTrackAlbum? Album,
    LastFmTrackDate? Date,
    string? Url,
    IReadOnlyList<LastFmImage>? Image,
    [property: JsonPropertyName("@attr")] LastFmNowPlayingAttributes? Attributes);

public sealed record LastFmTrackArtist(
    [property: JsonPropertyName("#text")] string? Name,
    string? Mbid);

public sealed record LastFmTrackAlbum(
    [property: JsonPropertyName("#text")] string? Name,
    string? Mbid);

public sealed record LastFmTrackDate(
    [property: JsonPropertyName("uts")] string? UnixTime,
    [property: JsonPropertyName("#text")] string? Text);

public sealed record LastFmNowPlayingAttributes(
    [property: JsonPropertyName("nowplaying")] string? NowPlaying);

public sealed record LastFmRecentTracksAttributes(
    string? User,
    string? Page,
    string? PerPage,
    string? TotalPages,
    string? Total);

public sealed record LastFmImage(
    string? Size,
    [property: JsonPropertyName("#text")] string? Url);
