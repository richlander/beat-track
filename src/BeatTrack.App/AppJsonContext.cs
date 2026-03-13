using System.Text.Json.Serialization;
using BeatTrack.Core;

namespace BeatTrack.App;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(BeatTrackSnapshot))]
[JsonSerializable(typeof(BeatTrackUnifiedProfile))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
