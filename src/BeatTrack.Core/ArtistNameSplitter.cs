namespace BeatTrack.Core;

public static class ArtistNameSplitter
{
    private static readonly string[] CollabSeparators = [" and ", " & ", " with "];
    private static readonly string[] FeaturedPatterns = [" feat. ", " ft. ", " featuring "];

    /// <summary>
    /// Splits a compound artist/channel name like "Khruangbin and Leon Bridges"
    /// into individual artist names, but only when the parts are recognized as
    /// known artists. Returns the original name if splitting would create unknowns.
    /// This prevents breaking band names like "Belle &amp; Sebastian".
    /// </summary>
    public static IReadOnlyList<string> SplitCollaboration(string name, IReadOnlySet<string>? knownArtists = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        // Strip Discogs disambiguation suffixes: "Tennis (6)" -> "Tennis"
        var cleaned = StripDiscogsSuffix(name);

        // If no known artists set, we can't validate splits — return as-is
        if (knownArtists is null or { Count: 0 })
        {
            return [cleaned];
        }

        // Try splitting on each separator
        foreach (var sep in CollabSeparators)
        {
            var idx = cleaned.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx <= 0 || idx + sep.Length >= cleaned.Length)
            {
                continue;
            }

            var left = cleaned[..idx].Trim();
            var right = cleaned[(idx + sep.Length)..].Trim();

            // Only split if at least one side is a known artist
            var leftKnown = left.Length >= 2 && knownArtists.Contains(left);
            var rightKnown = right.Length >= 2 && knownArtists.Contains(right);

            if (leftKnown || rightKnown)
            {
                var results = new List<string>();
                if (left.Length >= 2) results.Add(left);
                // Recursively split the right side
                results.AddRange(SplitCollaboration(right, knownArtists));
                return results;
            }
        }

        return [cleaned];
    }

    /// <summary>
    /// Extracts artist names from a music video title.
    /// Handles patterns like:
    ///   "Artist - Track feat. Artist2"
    ///   "Polyphia feat. Steve Vai - Track"
    ///   "Extrawelt x Jimi Jules - Track"
    /// Returns empty list if no artist-track pattern is detected.
    /// </summary>
    public static IReadOnlyList<string> ExtractArtistsFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        // Split on " - " or " – " to get the artist/track portions
        var dashIdx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx < 0)
        {
            dashIdx = title.IndexOf(" – ", StringComparison.Ordinal);
        }

        if (dashIdx <= 0)
        {
            return [];
        }

        var artistPart = title[..dashIdx].Trim();
        var trackPart = title[(dashIdx + 3)..].Trim();
        var artists = new List<string>();

        // Check if the artist part has a featured pattern first:
        // "Polyphia feat. Steve Vai - Track" or "Röyksopp feat. Robyn - Monument"
        var foundFeatInArtist = false;
        foreach (var pattern in FeaturedPatterns)
        {
            var featIdx = artistPart.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (featIdx >= 0)
            {
                var primary = artistPart[..featIdx].Trim();
                var featured = artistPart[(featIdx + pattern.Length)..].Trim();
                featured = StripTrailingParenthetical(featured);

                if (primary.Length >= 2) artists.Add(primary);
                if (featured.Length >= 2) artists.Add(featured);
                foundFeatInArtist = true;
                break;
            }
        }

        if (!foundFeatInArtist)
        {
            // Check for " x " collab in artist part: "Extrawelt x Jimi Jules"
            var xIdx = artistPart.IndexOf(" x ", StringComparison.Ordinal);
            if (xIdx > 0 && xIdx + 3 < artistPart.Length)
            {
                var left = artistPart[..xIdx].Trim();
                var right = artistPart[(xIdx + 3)..].Trim();
                if (left.Length >= 2) artists.Add(left);
                if (right.Length >= 2) artists.Add(right);
            }
            else
            {
                if (artistPart.Length >= 2) artists.Add(artistPart);
            }
        }

        // Extract featured artists from track part: "Track feat. Artist2 (Official Video)"
        foreach (var pattern in FeaturedPatterns)
        {
            var featIdx = trackPart.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (featIdx >= 0)
            {
                var featuredPart = trackPart[(featIdx + pattern.Length)..].Trim();
                featuredPart = StripTrailingParenthetical(featuredPart);
                if (featuredPart.Length >= 2
                    && !artists.Contains(featuredPart, StringComparer.OrdinalIgnoreCase))
                {
                    artists.Add(featuredPart);
                }

                break;
            }
        }

        return artists;
    }

    private static string StripTrailingParenthetical(string value)
    {
        var parenIdx = value.IndexOf('(');
        if (parenIdx > 1)
        {
            value = value[..parenIdx].Trim();
        }

        var bracketIdx = value.IndexOf('[');
        if (bracketIdx > 1)
        {
            value = value[..bracketIdx].Trim();
        }

        return value;
    }

    private static string StripDiscogsSuffix(string name)
    {
        // Discogs adds disambiguation like "Tennis (6)", "Tycho (3)"
        // Strip trailing " (N)" where N is a number
        if (name.Length > 4 && name[^1] == ')')
        {
            var parenStart = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (parenStart > 0)
            {
                var inner = name[(parenStart + 2)..^1];
                if (int.TryParse(inner, out _))
                {
                    return name[..parenStart].Trim();
                }
            }
        }

        return name;
    }
}
