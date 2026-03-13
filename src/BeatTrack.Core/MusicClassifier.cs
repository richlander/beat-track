using System.Buffers;

namespace BeatTrack.Core;

public sealed class MusicClassifier
{
    private readonly HashSet<string> _knownArtists;

    private static readonly string[] MusicPlatformChannels =
    [
        "kexp", "cercle", "cercle records", "boiler room", "npr music",
        "arte concert", "austin city limits", "tiny desk concerts",
        "audiotree live", "sofar sounds", "colors", "colors studios",
        "majestic casual", "the current", "sub pop", "nuclear blast records",
        "napalm records", "anjunadeep", "anjunachill", "lofi girl",
        "colemine records", "gondwana records", "rough trade records",
        "domino recording co.", "nonesuch records", "merge records on youtube",
        "because music", "atlantic records", "rhino", "3voor12",
        "pinkpop archive", "npo radio 2 muziek", "drumeo",
        "rock & roll hall of fame", "rock vault",
        "session spotlight", "brew sessions live", "brodie sessions",
    ];

    private static readonly string[] DenyChannels =
    [
        "cbc news", "abc news", "bbc news", "nbc news", "cnbc",
        "the ezra klein show", "meidastouch", "the daily show",
        "the late show with stephen colbert", "saturday night live",
        "monocle films", "devops toolbox", "dotnet", ".net foundation",
        "ndc conferences", "microsoft developer", "microsoft mechanics",
        "microsoft visual studio", "scott hanselman",
        "immo landwerth", "linus tech tips", "gamers nexus",
        "bps.space", "julian ilett", "scott manley", "smarterevery",
        "numberphile", "computerphile", "veritasium", "kurzgesagt",
        "3blue1brown", "the royal institution", "cool worlds",
        "our ludicrous future", "speculative future",
        "now you know", "the electric viking", "undecided with matt ferrell",
        "adafruit industries", "nascompares", "nasaspaceflight",
        "jeff geerling", "rctestflight", "tom stanton",
        "crosstalk solutions", "powerfuljre", "dwarkesh patel",
        "latent space", "out of spec reviews", "jayztwocents",
        "shortcircuit", "hardware canucks", "paul's hardware",
        "eevblog", "brian tyler cohen", "glenn kirschner",
        "raging moderates", "the majority report",
        "movieclips", "rotten tomatoes", "prime video", "miramax",
        "techworld with nana", "distrotube",
        "sabine hossenfelder", "astrum", "adam ondra", "dr. becky",
        "strange parts", "kitboga", "electrified",
    ];

    private static readonly SearchValues<string> DenyTitleTerms =
        SearchValues.Create([
            "cbc news", "abc news", "ezra klein", "meidastouch",
            "dotnet", ".net ", "hanselman", "veritasium",
            "doctor who", "sherlock", "anthropic", "pentagon",
            "trump", "iran", "devops",
        ], StringComparison.OrdinalIgnoreCase);

    private static readonly SearchValues<string> StrongPositiveTitleTerms =
        SearchValues.Create([
            " - topic", "vevo", "official video", "official audio",
            "visualizer", "visualiser", "remix", "album stream",
            "full album", "kexp", "cercle", "boiler room", "tiny desk",
            "concert", "acoustic session",
        ], StringComparison.OrdinalIgnoreCase);

    private static readonly SearchValues<string> MediumPositiveTitleTerms =
        SearchValues.Create([
            "live at", "lyrics", "lyric video", "session",
            "feat.", " ft. ", "featuring ",
        ], StringComparison.OrdinalIgnoreCase);

    public MusicClassifier(IEnumerable<string> knownArtists)
    {
        ArgumentNullException.ThrowIfNull(knownArtists);
        _knownArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artist in knownArtists)
        {
            if (!string.IsNullOrWhiteSpace(artist) && artist.Length >= 3)
            {
                _knownArtists.Add(artist.Trim());
            }
        }
    }

    public MusicClassification Classify(string title, string? channelName)
    {
        var channel = channelName ?? string.Empty;

        // Deny-list channels first (fast reject)
        if (!string.IsNullOrWhiteSpace(channel) && IsDenyListedChannel(channel))
        {
            return new MusicClassification(false, "DenyChannel");
        }

        // Deny-list title terms
        if (ContainsAny(title, DenyTitleTerms))
        {
            return new MusicClassification(false, "DenyTitle");
        }

        // Layer 1: Known artist match (word-boundary)
        var artistMatch = FindKnownArtist(title, channel);
        if (artistMatch is not null)
        {
            return new MusicClassification(true, $"KnownArtist:{artistMatch}");
        }

        // Layer 2: YouTube Music auto-channel patterns (VEVO, "- Topic")
        if (IsAutoMusicChannel(channel))
        {
            return new MusicClassification(true, "AutoMusicChannel");
        }

        // Layer 3: Known music platform channels
        if (IsMusicPlatformChannel(channel))
        {
            return new MusicClassification(true, "MusicPlatform");
        }

        // Layer 4: Title heuristics (fallback)
        return ClassifyByHeuristic(title, channel);
    }

    private string? FindKnownArtist(string title, string channel)
    {
        // Check channel name (exact match after stripping suffixes)
        var channelClean = StripChannelSuffix(channel);
        if (!string.IsNullOrWhiteSpace(channelClean) && _knownArtists.TryGetValue(channelClean, out var channelMatch))
        {
            return channelMatch;
        }

        // Check title parts around " - " or " – " delimiters
        var parts = SplitArtistTitle(title);
        if (parts is not null)
        {
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (_knownArtists.TryGetValue(trimmed, out var titleMatch))
                {
                    return titleMatch;
                }
            }
        }

        // Word-boundary search in title for longer artist names (5+ chars)
        foreach (var artist in _knownArtists)
        {
            if (artist.Length >= 5 && ContainsWordBoundary(title, artist))
            {
                return artist;
            }
        }

        return null;
    }

    private static readonly (string Suffix, StringComparison Comparison)[] ChannelSuffixes =
    [
        (" - Topic", StringComparison.OrdinalIgnoreCase),
        ("VEVO", StringComparison.OrdinalIgnoreCase),
        ("officialchannel", StringComparison.OrdinalIgnoreCase),
        ("Music", StringComparison.Ordinal),  // case-sensitive to avoid stripping "music" from band names
    ];

    private static string StripChannelSuffix(string channel)
    {
        foreach (var (suffix, comparison) in ChannelSuffixes)
        {
            if (channel.Length > suffix.Length
                && channel.EndsWith(suffix, comparison))
            {
                return channel[..^suffix.Length].TrimEnd();
            }
        }

        return channel;
    }

    private static string[]? SplitArtistTitle(string title)
    {
        var idx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (idx < 0)
        {
            idx = title.IndexOf(" – ", StringComparison.Ordinal);
        }

        if (idx < 0)
        {
            return null;
        }

        return [title[..idx], title[(idx + 3)..]];
    }

    private static bool ContainsWordBoundary(string text, string word)
    {
        var idx = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            var before = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            var afterIdx = idx + word.Length;
            var after = afterIdx >= text.Length || !char.IsLetterOrDigit(text[afterIdx]);

            if (before && after)
            {
                return true;
            }

            idx = text.IndexOf(word, idx + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsAutoMusicChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        return channel.EndsWith("VEVO", StringComparison.OrdinalIgnoreCase)
            || channel.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMusicPlatformChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        foreach (var platform in MusicPlatformChannels)
        {
            if (string.Equals(channel, platform, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly SearchValues<string> DenyChannelTerms =
        SearchValues.Create(DenyChannels, StringComparison.OrdinalIgnoreCase);

    private static bool IsDenyListedChannel(string channel) =>
        channel.AsSpan().ContainsAny(DenyChannelTerms);

    private static MusicClassification ClassifyByHeuristic(string title, string channel)
    {
        var haystack = $"{title} {channel}";

        var strongPositive = ContainsAny(haystack, StrongPositiveTitleTerms);
        var mediumPositive = ContainsAny(haystack, MediumPositiveTitleTerms);
        var structuralPositive = LooksLikeArtistTrackPattern(title);

        if (strongPositive)
        {
            return new MusicClassification(true, "StrongHeuristic");
        }

        if (mediumPositive && structuralPositive)
        {
            return new MusicClassification(true, "MediumHeuristic");
        }

        if (structuralPositive && LooksArtistish(channel))
        {
            return new MusicClassification(true, "StructuralHeuristic");
        }

        return new MusicClassification(false, null);
    }

    private static bool LooksLikeArtistTrackPattern(string value) =>
        value.Contains(" - ", StringComparison.Ordinal) || value.Contains(" – ", StringComparison.Ordinal);

    private static readonly SearchValues<string> NonArtistTerms =
        SearchValues.Create(["news", "show", "podcast", "toolbox", "films", "in-depth",
            "background", "clips", "official channel"], StringComparison.OrdinalIgnoreCase);

    private static bool LooksArtistish(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.AsSpan().ContainsAny(NonArtistTerms);
    }

    private static bool ContainsAny(ReadOnlySpan<char> value, SearchValues<string> terms) =>
        value.ContainsAny(terms);
}

public sealed record MusicClassification(bool IsMusicCandidate, string? MatchReason);

public sealed record DiscoveredArtist(string Name, int EventCount, string Source);

public static class MusicArtistDiscovery
{
    /// <summary>
    /// Discovers likely music artist names from watch events that were classified
    /// via heuristics. A channel qualifies only if it has enough strong/medium
    /// heuristic matches (title contains "official video", "live at", "remix", etc.)
    /// — pure structural matches (just " - " in title) are not sufficient on their own.
    /// </summary>
    public static IReadOnlyList<DiscoveredArtist> DiscoverArtists(
        IEnumerable<BeatTrackWatchEvent> classifiedEvents,
        int minimumStrongCount = 3,
        int minimumTotalCount = 5)
    {
        return classifiedEvents
            .Where(static e => e.IsMusicCandidate
                && !string.IsNullOrWhiteSpace(e.ChannelName)
                && e.MusicMatchReason is "StructuralHeuristic" or "StrongHeuristic" or "MediumHeuristic")
            .GroupBy(static e => e.ChannelName!, StringComparer.OrdinalIgnoreCase)
            .Where(g =>
            {
                var strongOrMedium = g.Count(static e => e.MusicMatchReason is "StrongHeuristic" or "MediumHeuristic");
                return strongOrMedium >= minimumStrongCount && g.Count() >= minimumTotalCount;
            })
            .Select(static g => new DiscoveredArtist(
                Name: g.First().ChannelName!,
                EventCount: g.Count(),
                Source: "YouTubeHeuristic"))
            .OrderByDescending(static x => x.EventCount)
            .ToList();
    }
}
