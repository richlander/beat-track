using System.Buffers;
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
///   5. Reduced form match — strip filler words (and, the, of) then compare
///   6. Filler word insertion — find embedded "and"/"the"/"of" in spaceless input,
///      insert spaces, and check against exact index
///   7. Levenshtein similarity — fuzzy fallback for remaining mismatches
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
        " topic",
        "officialchannel",
        "official",
        "music",
    ];

    private static readonly string[] FillerWords = ["and", "the", "of"];

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

            AddToIndices(artist);
        }
    }

    /// <summary>
    /// Adds a new artist to the matcher's indices.
    /// </summary>
    public void AddArtist(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            AddToIndices(name);
        }
    }

    private void AddToIndices(string name)
    {
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
        var sorted = weights.OrderByDescending(static kvp => kvp.Value).ToList();
        var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var matcher = new ArtistNameMatcher([]);

        foreach (var (canonical, weight) in sorted)
        {
            var resolved = matcher.TryResolve(canonical);
            if (resolved is not null && merged.ContainsKey(resolved))
            {
                merged[resolved] += weight;
            }
            else
            {
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

        // Strategy 2: Strip known suffixes and retry exact + spaceless + folded + reduced
        var stripped = StripSuffixes(canonical);
        if (stripped != canonical)
        {
            var result = TryMatchVariants(stripped);
            if (result is not null)
            {
                return result;
            }
        }

        // Strategy 3-5: Spaceless, ASCII-folded, reduced
        var result2 = TryMatchVariants(canonical);
        if (result2 is not null)
        {
            return result2;
        }

        // Strategy 6: Filler word insertion — find embedded "and"/"the"/"of" in
        // spaceless input, insert spaces around them, check exact index
        var spaceless = canonical.Replace(" ", "", StringComparison.Ordinal);
        var inserted = TryFillerWordInsertion(spaceless);
        if (inserted is not null)
        {
            return inserted;
        }

        // Also try on suffix-stripped form
        if (stripped != canonical)
        {
            var strippedSpaceless = stripped.Replace(" ", "", StringComparison.Ordinal);
            var insertedStripped = TryFillerWordInsertion(strippedSpaceless);
            if (insertedStripped is not null)
            {
                return insertedStripped;
            }
        }

        // Strategy 7: Levenshtein similarity against ASCII-folded index
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

    /// <summary>
    /// Tries spaceless, ASCII-folded, and reduced matching for a given canonical name.
    /// </summary>
    private string? TryMatchVariants(string canonical)
    {
        var spaceless = canonical.Replace(" ", "", StringComparison.Ordinal);
        if (_spacelessIndex.TryGetValue(spaceless, out var spacelessMatch))
        {
            return spacelessMatch;
        }

        var folded = FoldToAscii(spaceless);
        if (_asciiFoldedIndex.TryGetValue(folded, out var foldedMatch))
        {
            return foldedMatch;
        }

        // Check reduced index both ways
        var reducedInput = StripFillerWords(canonical);
        if (_reducedIndex.TryGetValue(reducedInput, out var reducedMatch))
        {
            return reducedMatch;
        }

        if (_reducedIndex.TryGetValue(spaceless, out var reducedMatch2))
        {
            return reducedMatch2;
        }

        return null;
    }

    /// <summary>
    /// Tries inserting spaces around filler words embedded in a spaceless string.
    /// e.g. "hallandoates" → finds "and" at pos 4 → tries "hall and oates" → checks exact index.
    /// </summary>
    private string? TryFillerWordInsertion(string spaceless)
    {
        foreach (var filler in FillerWords)
        {
            var idx = spaceless.IndexOf(filler, StringComparison.Ordinal);
            while (idx > 0 && idx + filler.Length < spaceless.Length)
            {
                var before = spaceless[..idx];
                var after = spaceless[(idx + filler.Length)..];
                var candidate = $"{before} {filler} {after}";

                if (_exactIndex.TryGetValue(candidate, out var match))
                {
                    return match;
                }

                // Try with additional filler insertions in the remainder
                // e.g. "florenceandthemachine" → "florence and themachine" → "florence and the machine"
                foreach (var filler2 in FillerWords)
                {
                    var idx2 = after.IndexOf(filler2, StringComparison.Ordinal);
                    if (idx2 > 0 && idx2 + filler2.Length < after.Length)
                    {
                        var before2 = after[..idx2];
                        var after2 = after[(idx2 + filler2.Length)..];
                        var candidate2 = $"{before} {filler} {before2} {filler2} {after2}";

                        if (_exactIndex.TryGetValue(candidate2, out var match2))
                        {
                            return match2;
                        }
                    }
                }

                idx = spaceless.IndexOf(filler, idx + 1, StringComparison.Ordinal);
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
