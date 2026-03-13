using System.Globalization;
using BeatTrack.Core;

namespace BeatTrack.LastFm;

public static class LastFmMappingExtensions
{
    public static BeatTrackUserProfile ToBeatTrackUserProfile(this LastFmUserGetInfoResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.User);

        return new BeatTrackUserProfile(
            UserName: response.User.Name,
            RealName: NullIfWhiteSpace(response.User.RealName),
            Url: NullIfWhiteSpace(response.User.Url),
            RegisteredAtUnixTime: ParseNullableInt64(response.User.Registered?.UnixTime),
            PlayCount: ParseNullableInt64(response.User.PlayCount),
            Images: MapImages(response.User.Image));
    }

    public static BeatTrackPagedResult<BeatTrackListeningEvent> ToBeatTrackListeningEvents(this LastFmRecentTracksResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new BeatTrackPagedResult<BeatTrackListeningEvent>(
            Items: response.RecentTracks.Track.Select(static track => track.ToBeatTrackListeningEvent(isLoved: false)).ToArray(),
            Page: ParseNullableInt32(response.RecentTracks.Attributes?.Page),
            PerPage: ParseNullableInt32(response.RecentTracks.Attributes?.PerPage),
            TotalPages: ParseNullableInt32(response.RecentTracks.Attributes?.TotalPages),
            Total: ParseNullableInt32(response.RecentTracks.Attributes?.Total));
    }

    public static BeatTrackPagedResult<BeatTrackListeningEvent> ToBeatTrackLovedListeningEvents(this LastFmLovedTracksResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new BeatTrackPagedResult<BeatTrackListeningEvent>(
            Items: response.LovedTracks.Track.Select(static track => track.ToBeatTrackListeningEvent()).ToArray(),
            Page: ParseNullableInt32(response.LovedTracks.Attributes?.Page),
            PerPage: ParseNullableInt32(response.LovedTracks.Attributes?.PerPage),
            TotalPages: ParseNullableInt32(response.LovedTracks.Attributes?.TotalPages),
            Total: ParseNullableInt32(response.LovedTracks.Attributes?.Total));
    }

    public static BeatTrackPagedResult<BeatTrackArtistSummary> ToBeatTrackArtistSummaries(this LastFmTopArtistsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new BeatTrackPagedResult<BeatTrackArtistSummary>(
            Items: response.TopArtists.Artist.Select(artist => new BeatTrackArtistSummary(
                Name: artist.Name,
                Url: NullIfWhiteSpace(artist.Url),
                Mbid: NullIfWhiteSpace(artist.Mbid),
                PlayCount: ParseNullableInt64(artist.PlayCount),
                Period: NullIfWhiteSpace(response.TopArtists.Attributes?.Period),
                Images: MapImages(artist.Image))).ToArray(),
            Page: ParseNullableInt32(response.TopArtists.Attributes?.Page),
            PerPage: ParseNullableInt32(response.TopArtists.Attributes?.PerPage),
            TotalPages: ParseNullableInt32(response.TopArtists.Attributes?.TotalPages),
            Total: ParseNullableInt32(response.TopArtists.Attributes?.Total));
    }

    public static BeatTrackPagedResult<BeatTrackAlbumSummary> ToBeatTrackAlbumSummaries(this LastFmTopAlbumsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new BeatTrackPagedResult<BeatTrackAlbumSummary>(
            Items: response.TopAlbums.Album.Select(album => new BeatTrackAlbumSummary(
                Name: album.Name,
                ArtistName: NullIfWhiteSpace(album.Artist?.Name),
                Url: NullIfWhiteSpace(album.Url),
                Mbid: NullIfWhiteSpace(album.Mbid),
                PlayCount: ParseNullableInt64(album.PlayCount),
                Period: NullIfWhiteSpace(response.TopAlbums.Attributes?.Period),
                Images: MapImages(album.Image))).ToArray(),
            Page: ParseNullableInt32(response.TopAlbums.Attributes?.Page),
            PerPage: ParseNullableInt32(response.TopAlbums.Attributes?.PerPage),
            TotalPages: ParseNullableInt32(response.TopAlbums.Attributes?.TotalPages),
            Total: ParseNullableInt32(response.TopAlbums.Attributes?.Total));
    }

    public static BeatTrackPagedResult<BeatTrackTrackSummary> ToBeatTrackTrackSummaries(this LastFmTopTracksResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new BeatTrackPagedResult<BeatTrackTrackSummary>(
            Items: response.TopTracks.Track.Select(track => new BeatTrackTrackSummary(
                Name: track.Name,
                ArtistName: NullIfWhiteSpace(track.Artist?.Name),
                Url: NullIfWhiteSpace(track.Url),
                Mbid: NullIfWhiteSpace(track.Mbid),
                PlayCount: ParseNullableInt64(track.PlayCount),
                Period: NullIfWhiteSpace(response.TopTracks.Attributes?.Period),
                Images: MapImages(track.Image))).ToArray(),
            Page: ParseNullableInt32(response.TopTracks.Attributes?.Page),
            PerPage: ParseNullableInt32(response.TopTracks.Attributes?.PerPage),
            TotalPages: ParseNullableInt32(response.TopTracks.Attributes?.TotalPages),
            Total: ParseNullableInt32(response.TopTracks.Attributes?.Total));
    }

    private static BeatTrackListeningEvent ToBeatTrackListeningEvent(this LastFmTrack track, bool isLoved) =>
        new(
            TrackName: track.Name,
            ArtistName: NullIfWhiteSpace(track.Artist?.Name),
            AlbumName: NullIfWhiteSpace(track.Album?.Name),
            Url: NullIfWhiteSpace(track.Url),
            PlayedAtUnixTime: ParseNullableInt64(track.Date?.UnixTime),
            IsNowPlaying: string.Equals(track.Attributes?.NowPlaying, "true", StringComparison.OrdinalIgnoreCase),
            IsLoved: isLoved,
            Images: MapImages(track.Image));

    private static BeatTrackListeningEvent ToBeatTrackListeningEvent(this LastFmLovedTrack track) =>
        new(
            TrackName: track.Name,
            ArtistName: NullIfWhiteSpace(track.Artist?.Name),
            AlbumName: null,
            Url: NullIfWhiteSpace(track.Url),
            PlayedAtUnixTime: ParseNullableInt64(track.Date?.UnixTime),
            IsNowPlaying: false,
            IsLoved: true,
            Images: MapImages(track.Image));

    private static IReadOnlyList<BeatTrackImage> MapImages(IReadOnlyList<LastFmImage>? images) =>
        images?.Select(static image => new BeatTrackImage(image.Size, NullIfWhiteSpace(image.Url))).ToArray()
        ?? [];

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static int? ParseNullableInt32(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static long? ParseNullableInt64(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
