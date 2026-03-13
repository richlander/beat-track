using System.Text;
using System.Text.Json;

namespace BeatTrack.Core;

public sealed class ListenBrainzPopularityLookup : IDisposable
{
    private readonly HttpClient _httpClient;

    public ListenBrainzPopularityLookup(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri("https://api.listenbrainz.org/");

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BeatTrack/0.1 (https://github.com/beat-track)");
        }
    }

    /// <summary>
    /// Checks artist popularity on ListenBrainz for a batch of MusicBrainz artist MBIDs.
    /// Returns popularity data (total listen count and user count) for each.
    /// </summary>
    public async Task<IReadOnlyList<ArtistPopularity>> GetArtistPopularityAsync(
        IEnumerable<string> artistMbids,
        CancellationToken cancellationToken = default)
    {
        var mbidList = artistMbids.ToList();
        if (mbidList.Count == 0)
        {
            return [];
        }

        var requestJson = BuildRequestJson(mbidList);

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("1/popularity/artist", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"ListenBrainz API returned {response.StatusCode}: {errorBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var popularities = new List<ArtistPopularity>(mbidList.Count);
        var i = 0;
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (i >= mbidList.Count)
            {
                break;
            }

            long listenCount = 0;
            long userCount = 0;

            if (element.ValueKind != JsonValueKind.Null)
            {
                if (element.TryGetProperty("total_listen_count", out var lc) && lc.ValueKind == JsonValueKind.Number)
                {
                    listenCount = lc.GetInt64();
                }

                if (element.TryGetProperty("total_user_count", out var uc) && uc.ValueKind == JsonValueKind.Number)
                {
                    userCount = uc.GetInt64();
                }
            }

            popularities.Add(new ArtistPopularity(
                ArtistMbid: mbidList[i],
                TotalListenCount: listenCount,
                TotalUserCount: userCount));
            i++;
        }

        return popularities;
    }

    private static string BuildRequestJson(List<string> mbids)
    {
        var sb = new StringBuilder();
        sb.Append("{\"artist_mbids\":[");
        for (var i = 0; i < mbids.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"');
            sb.Append(mbids[i]);
            sb.Append('"');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed record ArtistPopularity(
    string ArtistMbid,
    long TotalListenCount,
    long TotalUserCount);
