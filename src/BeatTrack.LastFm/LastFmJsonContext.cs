using System.Text.Json.Serialization;

namespace BeatTrack.LastFm;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(LastFmErrorEnvelope))]
[JsonSerializable(typeof(LastFmUserGetInfoResponse))]
[JsonSerializable(typeof(LastFmRecentTracksResponse))]
[JsonSerializable(typeof(LastFmTopArtistsResponse))]
[JsonSerializable(typeof(LastFmTopAlbumsResponse))]
[JsonSerializable(typeof(LastFmTopTracksResponse))]
[JsonSerializable(typeof(LastFmLovedTracksResponse))]
[JsonSerializable(typeof(LastFmAuthTokenResponse))]
[JsonSerializable(typeof(LastFmSessionResponse))]
[JsonSerializable(typeof(LastFmMobileSessionResponse))]
[JsonSerializable(typeof(LastFmStreamable))]
internal sealed partial class LastFmJsonContext : JsonSerializerContext
{
}
