using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BeatTrack.Core;

public sealed class MusicBrainzArtistLookup : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, MusicBrainzLookupResult> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastRequestTime = DateTime.MinValue;

    public MusicBrainzArtistLookup(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri("https://musicbrainz.org/ws/2/");

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BeatTrack/0.1 (https://github.com/beat-track)");
        }

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));
    }

    public async Task<MusicBrainzLookupResult> LookupArtistAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        // MusicBrainz rate limit: 1 request/second
        var elapsed = DateTime.UtcNow - _lastRequestTime;
        if (elapsed < TimeSpan.FromMilliseconds(1100))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1100) - elapsed, cancellationToken);
        }

        var encodedName = Uri.EscapeDataString(name);
        var url = $"artist/?query=artist:{encodedName}&fmt=json&limit=5";

        _lastRequestTime = DateTime.UtcNow;
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable || response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Back off and retry once
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            _lastRequestTime = DateTime.UtcNow;
            response = await _httpClient.GetAsync(url, cancellationToken);
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(MusicBrainzJsonContext.Default.MusicBrainzSearchResponse, cancellationToken);

        var lookupResult = EvaluateMatch(name, result);
        _cache[name] = lookupResult;
        return lookupResult;
    }

    /// <summary>
    /// Looks up multiple candidate names and returns only those confirmed as artists.
    /// </summary>
    public async Task<IReadOnlyList<MusicBrainzLookupResult>> LookupCandidatesAsync(
        IEnumerable<string> candidateNames,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var confirmed = new List<MusicBrainzLookupResult>();

        foreach (var name in candidateNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(name);

            try
            {
                var result = await LookupArtistAsync(name, cancellationToken);
                if (result.IsConfirmedArtist)
                {
                    confirmed.Add(result);
                }
            }
            catch (HttpRequestException)
            {
                // Skip on network errors, don't fail the whole batch
            }
        }

        return confirmed;
    }

    // Common words and phrases that match real MusicBrainz artists but are
    // almost never the actual artist when used as a YouTube channel name.
    private static readonly HashSet<string> AmbiguousNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "heart", "family", "live", "training", "spirit", "stories", "garage",
        "cycling", "linux", "mellow", "audiophile", "worship", "economist",
        "foundry", "rainmaker", "droid", "summit", "various artists", "[unknown]",
    };

    private static MusicBrainzLookupResult EvaluateMatch(string query, MusicBrainzSearchResponse? response)
    {
        if (response?.Artists is not { Count: > 0 })
        {
            return new MusicBrainzLookupResult(query, false, null, null, 0);
        }

        // Only accept strict exact-name matches (query == artist name).
        // MusicBrainz fuzzy search returns false matches too easily
        // (e.g., "Third Row Tesla" → Tesla, "Orlando Shakes" → Alabama Shakes).
        foreach (var artist in response.Artists)
        {
            var score = artist.Score;
            var matchedName = artist.Name;

            var isExactMatch = string.Equals(query, matchedName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(query, artist.SortName, StringComparison.OrdinalIgnoreCase);

            if (!isExactMatch || score < 95)
            {
                continue;
            }

            // Reject matches to generic/ambiguous names
            if (AmbiguousNames.Contains(matchedName))
            {
                continue;
            }

            // Reject "Various Artists" and "[unknown]" regardless of casing
            if (matchedName.Contains("Various Artists", StringComparison.OrdinalIgnoreCase)
                || matchedName.Contains("[unknown]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Reject very short names (1-2 words, each <= 4 chars) as too ambiguous
            var words = matchedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 2 && words.All(w => w.Length <= 4))
            {
                continue;
            }

            return new MusicBrainzLookupResult(query, true, matchedName, artist.Id, score);
        }

        var top = response.Artists[0];
        return new MusicBrainzLookupResult(query, false, top.Name, top.Id, top.Score);
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed record MusicBrainzLookupResult(
    string Query,
    bool IsConfirmedArtist,
    string? MatchedName,
    string? MusicBrainzId,
    int Score);

public sealed class MusicBrainzSearchResponse
{
    [JsonPropertyName("artists")]
    public IReadOnlyList<MusicBrainzArtist> Artists { get; init; } = [];
}

public sealed class MusicBrainzArtist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("sort-name")]
    public string? SortName { get; init; }

    [JsonPropertyName("score")]
    public int Score { get; init; }
}

[JsonSerializable(typeof(MusicBrainzSearchResponse))]
internal sealed partial class MusicBrainzJsonContext : JsonSerializerContext;
