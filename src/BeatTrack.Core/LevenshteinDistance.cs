namespace BeatTrack.Core;

/// <summary>
/// Computes the Levenshtein edit distance between two strings.
/// Uses an optimized single-row algorithm with O(min(m,n)) memory.
/// Adapted from dotnet-inspect.
/// </summary>
public static class LevenshteinDistance
{
    public static int Compute(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        // Ensure target is the shorter string for optimal memory usage
        if (source.Length < target.Length)
            (source, target) = (target, source);

        var costs = new int[target.Length + 1];
        for (int i = 0; i <= target.Length; i++) costs[i] = i;

        for (int i = 1; i <= source.Length; i++)
        {
            int prev = costs[0];
            costs[0] = i;
            for (int j = 1; j <= target.Length; j++)
            {
                int current = costs[j];
                costs[j] = Math.Min(
                    Math.Min(costs[j - 1] + 1, costs[j] + 1),
                    prev + (source[i - 1] == target[j - 1] ? 0 : 1));
                prev = current;
            }
        }

        return costs[target.Length];
    }

    public static double Similarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target)) return 1.0;
        int maxLen = Math.Max(source?.Length ?? 0, target?.Length ?? 0);
        return 1.0 - (double)Compute(source ?? "", target ?? "") / maxLen;
    }
}
