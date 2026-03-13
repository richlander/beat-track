using System.Text.Json.Serialization;

namespace BeatTrack.LastFm;

public sealed record LastFmAuthTokenResponse(
    string Token);

public sealed record LastFmSessionResponse(
    LastFmSession Session);

public sealed record LastFmMobileSessionResponse(
    LastFmSession Session);

public sealed record LastFmSession(
    string Name,
    string Key,
    string Subscriber);
