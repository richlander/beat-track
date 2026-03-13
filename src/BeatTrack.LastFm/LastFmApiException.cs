namespace BeatTrack.LastFm;

public sealed class LastFmApiException : Exception
{
    public LastFmApiException(int errorCode, string message)
        : base($"Last.fm API error {errorCode}: {message}")
    {
        ErrorCode = errorCode;
    }

    public int ErrorCode { get; }
}
