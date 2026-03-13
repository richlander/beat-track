using System.Text.Json;

namespace BeatTrack.Core;

/// <summary>
/// Queries the ListenBrainz Labs API for artists similar to a given seed artist.
/// Uses session-based collaborative filtering from aggregate listening data.
/// </summary>
public sealed class ListenBrainzSimilarArtistsLookup : IDisposable
{
    private readonly HttpClient _httpClient;

    // Default: 7500-day window, 3-contribution threshold, top 100 results
    private const string DefaultAlgorithm =
        "session_based_days_7500_session_300_contribution_3_threshold_10_limit_100_filter_True_skip_30";

    public ListenBrainzSimilarArtistsLookup(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri("https://labs.api.listenbrainz.org/");

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BeatTrack/0.1 (https://github.com/beat-track)");
        }
    }

    /// <summary>
    /// Gets artists similar to the given seed artist MBID.
    /// </summary>
    public async Task<IReadOnlyList<SimilarArtist>> GetSimilarArtistsAsync(
        string artistMbid,
        string? algorithm = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artistMbid);

        var algo = algorithm ?? DefaultAlgorithm;
        var url = $"similar-artists/json?artist_mbids={Uri.EscapeDataString(artistMbid)}&algorithm={Uri.EscapeDataString(algo)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var results = new List<SimilarArtist>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var mbid = element.GetProperty("artist_mbid").GetString();
            var name = element.GetProperty("name").GetString();
            var score = element.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number
                ? s.GetInt32() : 0;
            var type = element.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() : null;
            var referenceMbid = element.TryGetProperty("reference_mbid", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() : null;

            if (mbid is not null && name is not null)
            {
                results.Add(new SimilarArtist(mbid, name, score, type, referenceMbid));
            }
        }

        return results;
    }

    /// <summary>
    /// Gets similar artists for multiple seed artists and aggregates the results.
    /// Artists appearing as similar to multiple seeds get higher combined scores.
    /// </summary>
    public async Task<IReadOnlyList<AggregatedSimilarArtist>> GetSimilarArtistsForMultipleAsync(
        IEnumerable<(string Mbid, string Name)> seedArtists,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var seedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aggregated = new Dictionary<string, AggregatedSimilarArtistBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var (mbid, name) in seedArtists)
        {
            cancellationToken.ThrowIfCancellationRequested();
            seedSet.Add(mbid);
            progress?.Report(name);

            try
            {
                var similar = await GetSimilarArtistsAsync(mbid, cancellationToken: cancellationToken);

                foreach (var artist in similar)
                {
                    if (!aggregated.TryGetValue(artist.ArtistMbid, out var builder))
                    {
                        builder = new AggregatedSimilarArtistBuilder(artist.ArtistMbid, artist.Name);
                        aggregated[artist.ArtistMbid] = builder;
                    }

                    builder.AddReference(name, artist.Score);
                }
            }
            catch (HttpRequestException)
            {
                // Skip on network errors
            }
        }

        // Exclude seed artists themselves from results
        return aggregated.Values
            .Where(b => !seedSet.Contains(b.Mbid))
            .Select(static b => b.Build())
            .OrderByDescending(static a => a.SeedCount)
            .ThenByDescending(static a => a.TotalScore)
            .ToList();
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class AggregatedSimilarArtistBuilder(string mbid, string name)
    {
        public string Mbid => mbid;
        private readonly List<(string SeedName, int Score)> _references = [];

        public void AddReference(string seedName, int score)
        {
            _references.Add((seedName, score));
        }

        public AggregatedSimilarArtist Build() => new(
            mbid, name,
            _references.Count,
            _references.Sum(r => r.Score),
            _references.Select(r => r.SeedName).ToList());
    }
}

public sealed record SimilarArtist(
    string ArtistMbid,
    string Name,
    int Score,
    string? Type,
    string? ReferenceMbid);

public sealed record AggregatedSimilarArtist(
    string ArtistMbid,
    string Name,
    int SeedCount,
    int TotalScore,
    IReadOnlyList<string> SimilarToSeeds);
