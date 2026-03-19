using BeatTrack.Core.Views;
using Markout;

namespace BeatTrack.Core.Queries;

public static class TopArtistsQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var window = ParseStringFlag(args, "--window") ?? "30d";
        var limit = ParseIntFlag(args, "--limit") ?? 50;

        var cutoffMs = ParseWindowCutoff(window);
        var filtered = cutoffMs > 0
            ? scrobbles.Where(s => s.TimestampMs >= cutoffMs && s.TimestampMs > 0).ToList()
            : scrobbles.Where(static s => s.TimestampMs > 0).ToList();

        if (filtered.Count == 0)
        {
            Console.Error.WriteLine($"(no scrobbles found in {window} window)");
            return 0;
        }

        var ranked = filtered
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.First().ArtistName, Canonical: g.Key, Count: g.Count()))
            .OrderByDescending(static x => x.Count)
            .Take(limit)
            .ToList();

        var view = new TopArtistsView
        {
            Title = $"Top Artists ({window})",
            Summary = $"{filtered.Count:N0} scrobbles",
            Scrobbles = filtered.Count,
            Window = window,
            Artists = ranked
                .Select(x => new TopArtistRow
                {
                    Name = x.Name,
                    Plays = x.Count,
                })
                .ToList(),
        };

        MarkoutSerializer.Serialize(view, Console.Out, BeatTrackMarkoutContext.Default);

        return 0;
    }

    internal static long ParseWindowCutoff(string window)
    {
        var lower = window.ToLowerInvariant().Trim();
        if (lower is "all")
        {
            return 0;
        }

        if (lower.EndsWith('d') && int.TryParse(lower[..^1], out var days))
        {
            return DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();
        }

        if (lower.EndsWith('m') && int.TryParse(lower[..^1], out var months))
        {
            return DateTimeOffset.UtcNow.AddDays(-months * 30).ToUnixTimeMilliseconds();
        }

        if (lower.EndsWith('y') && int.TryParse(lower[..^1], out var years))
        {
            return DateTimeOffset.UtcNow.AddDays(-years * 365).ToUnixTimeMilliseconds();
        }

        return DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
    }

    private static string? ParseStringFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static int? ParseIntFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }
        return null;
    }
}
