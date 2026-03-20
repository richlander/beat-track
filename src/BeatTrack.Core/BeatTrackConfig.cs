namespace BeatTrack.Core;

/// <summary>
/// Reads configuration from ~/.config/beat-track/config (key=value format).
/// Lines starting with # are comments. Blank lines are ignored.
/// </summary>
public class BeatTrackConfig
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public BeatTrackConfig(string path)
    {
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
                continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = trimmed[..eqIndex].Trim();
            var value = trimmed[(eqIndex + 1)..].Trim();

            // Strip surrounding quotes if present
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];

            _values[key] = value;
        }
    }

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public string? LastFmApiKey => Get("lastfm_api_key");
    public string? LastFmSharedSecret => Get("lastfm_shared_secret");
    public string? LastFmUser => Get("lastfm_user");
}
