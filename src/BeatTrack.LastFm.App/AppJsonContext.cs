using System.Text.Json.Serialization;
using BeatTrack.Core;

namespace BeatTrack.LastFm.App;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(BeatTrackUserProfile))]
[JsonSerializable(typeof(BeatTrackPagedResult<BeatTrackListeningEvent>))]
[JsonSerializable(typeof(BeatTrackPagedResult<BeatTrackArtistSummary>))]
[JsonSerializable(typeof(BeatTrackSnapshot))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
