namespace BeatTrack.LastFm;

public sealed record LastFmClientOptions(
    string ApiKey,
    string? SharedSecret = null,
    string? ApplicationName = null,
    Uri? BaseAddress = null)
{
    public Uri EffectiveBaseAddress => BaseAddress ?? new("https://ws.audioscrobbler.com/2.0/");
}
