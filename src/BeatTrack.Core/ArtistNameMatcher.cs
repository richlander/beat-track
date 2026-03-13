using System.Globalization;
using System.Text;

namespace BeatTrack.Core;

/// <summary>
/// Multi-strategy matcher that resolves YouTube channel names (and other noisy
/// artist name variants) to canonical known artist names.
///
/// Strategies (tried in order):
///   1. Exact canonical match
///   2. Extended suffix stripping (officialchannel, official, music, etc.)
///   3. Spaceless match — strip all spaces and compare
///   4. ASCII-folded spaceless match — strip diacritics then compare spaceless
///   5. Levenshtein similarity — fuzzy fallback for missing words
/// </summary>
public sealed class ArtistNameMatcher
{
    private readonly Dictionary<string, string> _exactIndex;       // canonical → canonical
    private readonly Dictionary<string, string> _spacelessIndex;   // spaceless canonical → canonical
    private readonly Dictionary<string, string> _asciiFoldedIndex; // ASCII-folded spaceless → canonical
    private readonly Dictionary<string, string> _reducedIndex;     // spaceless with filler words removed → canonical

    private static readonly string[] ChannelSuffixes =
    [
        "vevo",
        " - topic",
        " topic",       // canonical form after dash/punctuation stripped
        "officialchannel",
        "official",
        "music",
    ];

    // Minimum Levenshtein similarity threshold for a match.
    // Set high to avoid false positives — the spaceless match handles
    // the common case; Levenshtein is for missing filler words.
    private const double LevenshteinThreshold = 0.75;

    // Don't attempt Levenshtein for very short names (too many false positives)
    private const int MinLengthForLevenshtein = 8;

    public ArtistNameMatcher(IEnumerable<string> knownArtists)
    {
        ArgumentNullException.ThrowIfNull(knownArtists);

        _exactIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _spacelessIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _asciiFoldedIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _reducedIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artist in knownArtists)
        {
            if (string.IsNullOrWhiteSpace(artist))
            {
                continue;
            }

            var canonical = BeatTrackAnalysis.CanonicalizeArtistName(artist);
            _exactIndex.TryAdd(canonical, canonical);

            var spaceless = canonical.Replace(" ", "", StringComparison.Ordinal);
            _spacelessIndex.TryAdd(spaceless, canonical);

            var folded = FoldToAscii(spaceless);
            _asciiFoldedIndex.TryAdd(folded, canonical);

            var reduced = StripFillerWords(canonical);
            if (reduced != spaceless)
            {
                _reducedIndex.TryAdd(reduced, canonical);
            }
        }
    }

    /// <summary>
    /// Adds a new artist to the matcher's indices.
    /// </summary>
    public void AddArtist(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(name);
        _exactIndex.TryAdd(canonical, canonical);

        var spaceless = canonical.Replace(" ", "", StringComparison.Ordinal);
        _spacelessIndex.TryAdd(spaceless, canonical);

        var folded = FoldToAscii(spaceless);
        _asciiFoldedIndex.TryAdd(folded, canonical);

        var reduced = StripFillerWords(canonical);
        if (reduced != spaceless)
        {
            _reducedIndex.TryAdd(reduced, canonical);
        }
    }

    /// <summary>
    /// Strips known YouTube channel suffixes (VEVO, Topic, etc.) from a raw name.
    /// Useful for pre-cleaning scrobble data where YouTube scrobblers leave suffixes.
    /// </summary>
    public static string CleanChannelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        foreach (var (suffix, caseSensitive) in RawChannelSuffixes)
        {
            if (name.Length > suffix.Length
                && name.EndsWith(suffix, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length].TrimEnd();
            }
        }

        return name;
    }

    private static readonly (string Suffix, bool CaseSensitive)[] RawChannelSuffixes =
    [
        ("VEVO", false),
        (" - Topic", false),
    ];

    /// <summary>
    /// Merges weight dictionary entries that resolve to each other via the matcher.
    /// Entries with higher weight are kept as the canonical key; lower-weight duplicates are merged in.
    /// </summary>
    public static Dictionary<string, double> MergeWeights(Dictionary<string, double> weights)
    {
        // Process entries by weight descending — heavier entries establish the canonical name
        var sorted = weights.OrderByDescending(static kvp => kvp.Value).ToList();
        var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var matcher = new ArtistNameMatcher(Enumerable.Empty<string>());

        foreach (var (canonical, weight) in sorted)
        {
            // Try to resolve against already-added entries
            var resolved = matcher.TryResolve(canonical);
            if (resolved is not null && merged.ContainsKey(resolved))
            {
                // Merge into existing entry
                merged[resolved] += weight;
            }
            else
            {
                // New unique entry — add to both merged dict and matcher
                merged[canonical] = weight;
                matcher.AddArtist(canonical);
            }
        }

        return merged;
    }

    /// <summary>
    /// Tries to resolve a name (typically a YouTube channel name) to a known canonical artist name.
    /// Returns null if no match is found with sufficient confidence.
    /// </summary>
    public string? TryResolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(name);

        // Strategy 1: Exact canonical match
        if (_exactIndex.TryGetValue(canonical, out var exact))
        {
            return exact;
        }

        // Strategy 2: Strip known suffixes and retry exact + spaceless + folded
        var stripped = StripSuffixes(canonical);
        if (stripped != canonical)
        {
            if (_exactIndex.TryGetValue(stripped, out var strippedExact))
            {
                return strippedExact;
            }

            var strippedSpaceless = stripped.Replace(" ", "", StringComparison.Ordinal);
            if (_spacelessIndex.TryGetValue(strippedSpaceless, out var strippedSpacelessMatch))
            {
                return strippedSpacelessMatch;
            }

            var strippedFolded = FoldToAscii(strippedSpaceless);
            if (_asciiFoldedIndex.TryGetValue(strippedFolded, out var strippedFoldedMatch))
            {
                return strippedFoldedMatch;
            }

            // Also check reduced index (filler words stripped) with the suffix-stripped form
            if (_reducedIndex.TryGetValue(strippedSpaceless, out var strippedReducedMatch))
            {
                return strippedReducedMatch;
            }
        }

        // Strategy 3: Spaceless match
        var spaceless = canonical.Replace(" ", "", StringComparison.Ordinal);
        if (_spacelessIndex.TryGetValue(spaceless, out var spacelessMatch))
        {
            return spacelessMatch;
        }

        // Strategy 4: ASCII-folded spaceless match (strips diacritics)
        var folded = FoldToAscii(spaceless);
        if (_asciiFoldedIndex.TryGetValue(folded, out var foldedMatch))
        {
            return foldedMatch;
        }

        // Strategy 5: Reduced form match (strip filler words: and, the, of)
        // Catches: florencemachine → florence+the+machine, halloates → hall+and+oates
        var reducedInput = StripFillerWords(canonical);
        if (_reducedIndex.TryGetValue(reducedInput, out var reducedMatch))
        {
            return reducedMatch;
        }

        // Also check if input (spaceless) matches a known reduced form
        if (_reducedIndex.TryGetValue(spaceless, out var reducedMatch2))
        {
            return reducedMatch2;
        }

        // Strategy 6: Levenshtein similarity against ASCII-folded index
        if (spaceless.Length >= MinLengthForLevenshtein)
        {
            var bestMatch = FindBestLevenshteinMatch(spaceless);
            if (bestMatch is not null)
            {
                return bestMatch;
            }
        }

        return null;
    }

    private static string StripSuffixes(string canonical)
    {
        foreach (var suffix in ChannelSuffixes)
        {
            if (canonical.Length > suffix.Length
                && canonical.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return canonical[..^suffix.Length].TrimEnd();
            }
        }

        return canonical;
    }

    private string? FindBestLevenshteinMatch(string spacelessInput)
    {
        var foldedInput = FoldToAscii(spacelessInput);
        string? bestCanonical = null;
        var bestSimilarity = LevenshteinThreshold;

        foreach (var (foldedKey, canonical) in _asciiFoldedIndex)
        {
            // Quick length pre-filter: if lengths differ by more than 30%,
            // similarity can't reach the threshold
            var lengthRatio = (double)Math.Min(foldedInput.Length, foldedKey.Length)
                / Math.Max(foldedInput.Length, foldedKey.Length);
            if (lengthRatio < LevenshteinThreshold)
            {
                continue;
            }

            var similarity = LevenshteinDistance.Similarity(foldedInput, foldedKey);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestCanonical = canonical;
            }
        }

        return bestCanonical;
    }

    private static readonly string[] FillerWords = ["and", "the", "of"];

    /// <summary>
    /// Removes filler words (and, the, of) and spaces from a canonical name.
    /// "florence and the machine" → "florencemachine"
    /// </summary>
    private static string StripFillerWords(string canonical)
    {
        var words = canonical.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var word in words)
        {
            if (!Array.Exists(FillerWords, f => string.Equals(f, word, StringComparison.OrdinalIgnoreCase)))
            {
                sb.Append(word);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Strips diacritics/accents from a string: ö → o, é → e, etc.
    /// </summary>
    private static string FoldToAscii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
