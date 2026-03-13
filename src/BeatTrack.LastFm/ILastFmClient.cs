namespace BeatTrack.LastFm;

public interface ILastFmClient
{
    Task<LastFmUserGetInfoResponse> GetUserInfoAsync(string userName, CancellationToken cancellationToken = default);

    Task<LastFmRecentTracksResponse> GetRecentTracksAsync(
        LastFmRecentTracksRequest request,
        CancellationToken cancellationToken = default);

    Task<LastFmTopArtistsResponse> GetTopArtistsAsync(
        LastFmUserChartRequest request,
        CancellationToken cancellationToken = default);

    Task<LastFmTopAlbumsResponse> GetTopAlbumsAsync(
        LastFmUserChartRequest request,
        CancellationToken cancellationToken = default);

    Task<LastFmTopTracksResponse> GetTopTracksAsync(
        LastFmUserChartRequest request,
        CancellationToken cancellationToken = default);

    Task<LastFmLovedTracksResponse> GetLovedTracksAsync(
        LastFmPagedUserRequest request,
        CancellationToken cancellationToken = default);

    Task<LastFmAuthTokenResponse> GetTokenAsync(CancellationToken cancellationToken = default);

    Uri BuildAuthorizeUri(string token);

    Task<LastFmSessionResponse> GetSessionAsync(string token, CancellationToken cancellationToken = default);

    Task<LastFmMobileSessionResponse> GetMobileSessionAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}
