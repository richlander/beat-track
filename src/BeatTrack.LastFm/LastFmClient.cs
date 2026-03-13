using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BeatTrack.LastFm;

public sealed class LastFmClient : ILastFmClient
{
    private readonly HttpClient _httpClient;
    private readonly LastFmClientOptions _options;

    public LastFmClient(HttpClient httpClient, LastFmClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);

        _httpClient = httpClient;
        _options = options;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = options.EffectiveBaseAddress;
        }
    }

    public Task<LastFmUserGetInfoResponse> GetUserInfoAsync(string userName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        return GetAsync(
            method: "user.getInfo",
            parameters: new Dictionary<string, string?>
            {
                ["user"] = userName,
            },
            LastFmJsonContext.Default.LastFmUserGetInfoResponse,
            cancellationToken);
    }

    public Task<LastFmRecentTracksResponse> GetRecentTracksAsync(
        LastFmRecentTracksRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserName);

        ValidateLimit(request.Limit);

        return GetAsync(
            method: "user.getRecentTracks",
            parameters: new Dictionary<string, string?>
            {
                ["user"] = request.UserName,
                ["limit"] = request.Limit?.ToString(CultureInfo.InvariantCulture),
                ["page"] = request.Page?.ToString(CultureInfo.InvariantCulture),
                ["from"] = request.FromUnixTime?.ToString(CultureInfo.InvariantCulture),
                ["to"] = request.ToUnixTime?.ToString(CultureInfo.InvariantCulture),
                ["extended"] = request.IncludeExtendedData ? "1" : null,
            },
            LastFmJsonContext.Default.LastFmRecentTracksResponse,
            cancellationToken);
    }

    public Task<LastFmTopArtistsResponse> GetTopArtistsAsync(
        LastFmUserChartRequest request,
        CancellationToken cancellationToken = default) =>
        GetUserChartAsync(
            request,
            "user.getTopArtists",
            LastFmJsonContext.Default.LastFmTopArtistsResponse,
            cancellationToken);

    public Task<LastFmTopAlbumsResponse> GetTopAlbumsAsync(
        LastFmUserChartRequest request,
        CancellationToken cancellationToken = default) =>
        GetUserChartAsync(
            request,
            "user.getTopAlbums",
            LastFmJsonContext.Default.LastFmTopAlbumsResponse,
            cancellationToken);

    public Task<LastFmTopTracksResponse> GetTopTracksAsync(
        LastFmUserChartRequest request,
        CancellationToken cancellationToken = default) =>
        GetUserChartAsync(
            request,
            "user.getTopTracks",
            LastFmJsonContext.Default.LastFmTopTracksResponse,
            cancellationToken);

    public Task<LastFmLovedTracksResponse> GetLovedTracksAsync(
        LastFmPagedUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserName);
        ValidateLimit(request.Limit);

        return GetAsync(
            method: "user.getLovedTracks",
            parameters: new Dictionary<string, string?>
            {
                ["user"] = request.UserName,
                ["limit"] = request.Limit?.ToString(CultureInfo.InvariantCulture),
                ["page"] = request.Page?.ToString(CultureInfo.InvariantCulture),
            },
            LastFmJsonContext.Default.LastFmLovedTracksResponse,
            cancellationToken);
    }

    public Task<LastFmAuthTokenResponse> GetTokenAsync(CancellationToken cancellationToken = default) =>
        GetAsync(
            method: "auth.getToken",
            parameters: new Dictionary<string, string?>(),
            LastFmJsonContext.Default.LastFmAuthTokenResponse,
            cancellationToken,
            signed: true);

    public Uri BuildAuthorizeUri(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return new Uri($"{LastFmAuthenticationConstants.AuthorizeBaseAddress}?api_key={Uri.EscapeDataString(_options.ApiKey)}&token={Uri.EscapeDataString(token)}");
    }

    public Task<LastFmSessionResponse> GetSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return GetAsync(
            method: "auth.getSession",
            parameters: new Dictionary<string, string?>
            {
                ["token"] = token,
            },
            LastFmJsonContext.Default.LastFmSessionResponse,
            cancellationToken,
            signed: true);
    }

    public Task<LastFmMobileSessionResponse> GetMobileSessionAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return PostAsync(
            method: "auth.getMobileSession",
            parameters: new Dictionary<string, string?>
            {
                ["username"] = userName,
                ["password"] = password,
            },
            LastFmJsonContext.Default.LastFmMobileSessionResponse,
            cancellationToken,
            signed: true);
    }

    internal Uri BuildUri(string method, IReadOnlyDictionary<string, string?> parameters, bool signed = false)
    {
        var items = CreateParameters(method, parameters, signed);
        var query = string.Join(
            "&",
            items.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));

        return new Uri($"?{query}", UriKind.Relative);
    }

    internal string CreateApiSignature(IReadOnlyDictionary<string, string?> parameters)
    {
        if (string.IsNullOrWhiteSpace(_options.SharedSecret))
        {
            throw new InvalidOperationException("A shared secret is required for signed Last.fm requests.");
        }

        var builder = new StringBuilder();

        foreach (var pair in parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(pair.Value) || string.Equals(pair.Key, "format", StringComparison.Ordinal))
            {
                continue;
            }

            builder.Append(pair.Key);
            builder.Append(pair.Value);
        }

        builder.Append(_options.SharedSecret);

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = MD5.HashData(bytes);

        return Convert.ToHexStringLower(hash);
    }

    private Task<TResponse> GetUserChartAsync<TResponse>(
        LastFmUserChartRequest request,
        string method,
        JsonTypeInfo<TResponse> typeInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserName);
        ValidateLimit(request.Limit);

        return GetAsync(
            method,
            new Dictionary<string, string?>
            {
                ["user"] = request.UserName,
                ["period"] = request.Period.ToApiValue(),
                ["limit"] = request.Limit?.ToString(CultureInfo.InvariantCulture),
                ["page"] = request.Page?.ToString(CultureInfo.InvariantCulture),
            },
            typeInfo,
            cancellationToken);
    }

    private static void ValidateLimit(int? limit)
    {
        if (limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 200.");
        }
    }

    private SortedDictionary<string, string?> CreateParameters(
        string method,
        IReadOnlyDictionary<string, string?> parameters,
        bool signed)
    {
        var items = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["api_key"] = _options.ApiKey,
            ["format"] = "json",
            ["method"] = method,
        };

        foreach (var pair in parameters)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                items[pair.Key] = pair.Value;
            }
        }

        if (signed)
        {
            items["api_sig"] = CreateApiSignature(items);
        }

        return items;
    }

    private Task<TResponse> GetAsync<TResponse>(
        string method,
        IReadOnlyDictionary<string, string?> parameters,
        JsonTypeInfo<TResponse> typeInfo,
        CancellationToken cancellationToken,
        bool signed = false) =>
        SendAsync(HttpMethod.Get, method, parameters, typeInfo, cancellationToken, signed);

    private Task<TResponse> PostAsync<TResponse>(
        string method,
        IReadOnlyDictionary<string, string?> parameters,
        JsonTypeInfo<TResponse> typeInfo,
        CancellationToken cancellationToken,
        bool signed = false) =>
        SendAsync(HttpMethod.Post, method, parameters, typeInfo, cancellationToken, signed);

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod httpMethod,
        string method,
        IReadOnlyDictionary<string, string?> parameters,
        JsonTypeInfo<TResponse> typeInfo,
        CancellationToken cancellationToken,
        bool signed)
    {
        using var request = CreateRequest(httpMethod, method, parameters, signed);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var errorEnvelope = JsonSerializer.Deserialize(json, LastFmJsonContext.Default.LastFmErrorEnvelope);
        if (errorEnvelope is not null && errorEnvelope.Error is not null)
        {
            throw new LastFmApiException(errorEnvelope.Error.Value, errorEnvelope.Message ?? "Unknown Last.fm API error.");
        }

        var payload = JsonSerializer.Deserialize(json, typeInfo);
        return payload ?? throw new InvalidOperationException("The Last.fm API returned an empty response.");
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod httpMethod,
        string method,
        IReadOnlyDictionary<string, string?> parameters,
        bool signed)
    {
        var items = CreateParameters(method, parameters, signed);

        if (httpMethod == HttpMethod.Get)
        {
            var query = string.Join(
                "&",
                items.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));

            return new HttpRequestMessage(httpMethod, new Uri($"?{query}", UriKind.Relative));
        }

        var request = new HttpRequestMessage(httpMethod, string.Empty)
        {
            Content = new FormUrlEncodedContent(
                items.Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
                     .Select(static pair => new KeyValuePair<string, string>(pair.Key, pair.Value!))),
        };

        return request;
    }
}
