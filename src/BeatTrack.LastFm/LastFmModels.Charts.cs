using System.Text.Json.Serialization;

namespace BeatTrack.LastFm;

public sealed record LastFmTopArtistsResponse(
    [property: JsonPropertyName("topartists")] LastFmTopArtists TopArtists);

public sealed record LastFmTopArtists(
    IReadOnlyList<LastFmChartArtist> Artist,
    [property: JsonPropertyName("@attr")] LastFmChartAttributes? Attributes);

public sealed record LastFmChartArtist(
    string Name,
    string? Url,
    string? Mbid,
    [property: JsonPropertyName("playcount")] string? PlayCount,
    IReadOnlyList<LastFmImage>? Image,
    [property: JsonPropertyName("streamable")] string? Streamable);

public sealed record LastFmTopAlbumsResponse(
    [property: JsonPropertyName("topalbums")] LastFmTopAlbums TopAlbums);

public sealed record LastFmTopAlbums(
    IReadOnlyList<LastFmChartAlbum> Album,
    [property: JsonPropertyName("@attr")] LastFmChartAttributes? Attributes);

public sealed record LastFmChartAlbum(
    string Name,
    string? Url,
    string? Mbid,
    LastFmTrackArtist? Artist,
    [property: JsonPropertyName("playcount")] string? PlayCount,
    IReadOnlyList<LastFmImage>? Image);

public sealed record LastFmTopTracksResponse(
    [property: JsonPropertyName("toptracks")] LastFmTopTracks TopTracks);

public sealed record LastFmTopTracks(
    IReadOnlyList<LastFmChartTrack> Track,
    [property: JsonPropertyName("@attr")] LastFmChartAttributes? Attributes);

public sealed record LastFmChartTrack(
    string Name,
    string? Url,
    string? Mbid,
    LastFmTrackArtist? Artist,
    [property: JsonPropertyName("playcount")] string? PlayCount,
    IReadOnlyList<LastFmImage>? Image,
    [property: JsonPropertyName("streamable")] string? Streamable);

public sealed record LastFmLovedTracksResponse(
    [property: JsonPropertyName("lovedtracks")] LastFmLovedTracks LovedTracks);

public sealed record LastFmLovedTracks(
    IReadOnlyList<LastFmLovedTrack> Track,
    [property: JsonPropertyName("@attr")] LastFmChartAttributes? Attributes);

public sealed record LastFmLovedTrack(
    string Name,
    string? Url,
    string? Mbid,
    LastFmTrackArtist? Artist,
    LastFmTrackDate? Date,
    IReadOnlyList<LastFmImage>? Image,
    [property: JsonPropertyName("streamable")] LastFmStreamable? Streamable);

public sealed record LastFmStreamable(
    [property: JsonPropertyName("#text")] string? Text,
    string? Fulltrack);

public sealed record LastFmChartAttributes(
    string? User,
    string? Page,
    string? PerPage,
    string? TotalPages,
    string? Total,
    string? Period);
