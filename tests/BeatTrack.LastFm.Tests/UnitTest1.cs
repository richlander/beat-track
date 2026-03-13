using System.Net;
using System.Net.Http;
using System.Text;

namespace BeatTrack.LastFm.Tests;

public sealed class LastFmClientTests
{
    [Fact]
    public void BuildUri_uses_expected_query_parameters()
    {
        var client = CreateClient();

        var uri = client.BuildUri(
            "user.getRecentTracks",
            new Dictionary<string, string?>
            {
                ["user"] = "runfaster2000",
                ["limit"] = "5",
            });

        var query = uri.ToString();

        Assert.Contains("method=user.getRecentTracks", query);
        Assert.Contains("user=runfaster2000", query);
        Assert.Contains("limit=5", query);
        Assert.Contains("api_key=test-key", query);
        Assert.Contains("format=json", query);
    }

    [Fact]
    public void CreateApiSignature_returns_lower_hex_md5()
    {
        var client = CreateClient(sharedSecret: "secret");
        var parameters = new Dictionary<string, string?>
        {
            ["method"] = "user.getInfo",
            ["api_key"] = "test-key",
            ["user"] = "runfaster2000",
            ["format"] = "json",
        };

        var signature = client.CreateApiSignature(parameters);

        Assert.Matches("^[0-9a-f]{32}$", signature);
    }

    [Fact]
    public void BuildAuthorizeUri_includes_api_key_and_token()
    {
        var client = CreateClient();

        var uri = client.BuildAuthorizeUri("abc123");

        Assert.Equal("https://www.last.fm/api/auth/?api_key=test-key&token=abc123", uri.ToString());
    }

    [Fact]
    public async Task GetUserInfoAsync_deserializes_payload()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "user": {
                "name": "runfaster2000",
                "realname": "",
                "url": "https://www.last.fm/user/runfaster2000",
                "registered": {
                  "unixtime": "123"
                },
                "playcount": "42",
                "image": []
              }
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/"),
        };

        var client = new BeatTrack.LastFm.LastFmClient(
            httpClient,
            new BeatTrack.LastFm.LastFmClientOptions("test-key"));

        var response = await client.GetUserInfoAsync("runfaster2000");

        Assert.Equal("runfaster2000", response.User.Name);
        Assert.Equal("42", response.User.PlayCount);
    }

    [Fact]
    public async Task GetTopArtistsAsync_uses_period_and_deserializes_payload()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "topartists": {
                "artist": [
                  {
                    "name": "Boards of Canada",
                    "url": "https://www.last.fm/music/Boards+of+Canada",
                    "mbid": "",
                    "playcount": "99",
                    "image": [],
                    "streamable": "0"
                  }
                ],
                "@attr": {
                  "user": "runfaster2000",
                  "page": "1",
                  "perPage": "50",
                  "totalPages": "1",
                  "total": "1",
                  "period": "7day"
                }
              }
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/"),
        };

        var client = new BeatTrack.LastFm.LastFmClient(
            httpClient,
            new BeatTrack.LastFm.LastFmClientOptions("test-key"));

        var response = await client.GetTopArtistsAsync(new BeatTrack.LastFm.LastFmUserChartRequest(
            UserName: "runfaster2000",
            Period: BeatTrack.LastFm.LastFmTimePeriod.SevenDays,
            Limit: 10));

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Contains("period=7day", handler.LastRequestUri);
        Assert.Equal("Boards of Canada", response.TopArtists.Artist[0].Name);
        Assert.Equal("99", response.TopArtists.Artist[0].PlayCount);
    }

    [Fact]
    public async Task GetMobileSessionAsync_uses_post_and_deserializes_payload()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "session": {
                "name": "runfaster2000",
                "key": "session-key",
                "subscriber": "0"
              }
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/"),
        };

        var client = new BeatTrack.LastFm.LastFmClient(
            httpClient,
            new BeatTrack.LastFm.LastFmClientOptions("test-key", "secret"));

        var response = await client.GetMobileSessionAsync("runfaster2000", "password");

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("runfaster2000", response.Session.Name);
        Assert.Equal("session-key", response.Session.Key);
    }

    private static BeatTrack.LastFm.LastFmClient CreateClient(string? sharedSecret = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler("{\"user\":{\"name\":\"x\"}}"))
        {
            BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/"),
        };

        return new BeatTrack.LastFm.LastFmClient(
            httpClient,
            new BeatTrack.LastFm.LastFmClientOptions("test-key", sharedSecret));
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri?.ToString();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}
