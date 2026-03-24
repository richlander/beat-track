using System.Text.Json.Serialization;

namespace BeatTrack.Core;

public sealed record LastFmScrobble(
    string ArtistName,
    string? Album,
    string? Track,
    long TimestampMs);

public static class ScrobbleStore
{
    public static readonly JsonlStore<LastFmScrobble> Default = new(
        ScrobbleJsonContext.Default.LastFmScrobble,
        Comparer<LastFmScrobble>.Create((a, b) => a.TimestampMs.CompareTo(b.TimestampMs)));
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(LastFmScrobble))]
internal sealed partial class ScrobbleJsonContext : JsonSerializerContext;
