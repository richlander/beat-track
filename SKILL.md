---
name: beat-track
description: Analyze music listening data across Last.fm, YouTube, and Discogs. Run queries to find listening stats, discover new artists, identify strange absences, track surging interests, and build playlists from cross-source analysis.
---

# BeatTrack Music Analysis

## When to Use

- The user asks about their music listening habits, patterns, or history
- The user wants to discover new music based on what they already listen to
- The user wants to compare their listening across platforms (Last.fm, YouTube, Discogs)
- The user wants playlist suggestions based on surging interests, re-engagement, or gaps
- The user asks "what should I listen to?" or "what am I missing?"

## When Not to Use

- The user wants to play music (BeatTrack is analysis-only, not a player)
- The user needs real-time music streaming or playback control

## Prerequisites

The tool is at `/home/rich/.openclaw/workspace/git/beat-track`. All commands run from that directory.

Data lives at `~/.beattrack/data/`. At minimum, a Last.fm scrobble history CSV is needed.

## Using Without Data

If no data sources are configured yet, help the user set one up:

1. **Quickest path**: Export scrobble history from [lastfmstats.com](https://lastfmstats.com) → CSV → place at `~/.beattrack/data/lastfmstats/lastfmstats-USERNAME.csv`
2. **With API key**: Set `LASTFM_API_KEY` and run the snapshot tool for richer metadata
3. Even a few days of scrobbles is enough to run `stats` and `top-artists`

## Data Sources

| Source | What it provides | How to get it |
| --- | --- | --- |
| Last.fm scrobble CSV | Full listening history with timestamps | Export from lastfmstats.com, place in `~/.beattrack/data/lastfmstats/` |
| Last.fm API snapshot | Top artists with MBIDs, loved tracks | Set `LASTFM_API_KEY`, run `dotnet run --project src/BeatTrack.LastFm.App -- USERNAME` |
| Discogs collection | Physical media ownership | Export CSV from Discogs, place in `~/.beattrack/data/collection-csv/` |
| YouTube watch history | Video listening data | Google Takeout → YouTube, extract to `~/.beattrack/data/takeout/extracted/Takeout/` |
| Spotify (planned) | Streaming history | Not yet supported — extended streaming history from Spotify privacy export is on the roadmap |

## Available Queries

### Quick queries (scrobble CSV only, fast)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "What are my listening stats?" | `dotnet run --project src/BeatTrack.App -- stats` | Eddington number, total artists, span, one-hit-wonders, busiest periods |
| "What am I listening to this week?" | `dotnet run --project src/BeatTrack.App -- top-artists --window 7d` | Top artists by play count in last 7 days |
| "What are my top artists this year?" | `dotnet run --project src/BeatTrack.App -- top-artists --window 365d` | Top artists by play count in last year |
| "Show me my listening streaks" | `dotnet run --project src/BeatTrack.App -- streaks` | Longest consecutive-day streaks, overall and per-artist |
| "How long was my Massive Attack streak?" | `dotnet run --project src/BeatTrack.App -- streaks --artist "Massive Attack"` | All streaks for a specific artist |
| "Show me how my top artists grew over time" | `dotnet run --project src/BeatTrack.App -- artist-velocity --top 10 --bucket yearly` | Cumulative scrobble curves |
| "Compare Radiohead vs Caribou over time" | `dotnet run --project src/BeatTrack.App -- artist-velocity --artists "Radiohead,Caribou" --bucket monthly` | Side-by-side growth |

### Cross-source analysis (all data, may call APIs on first run)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "Run the full analysis" | `dotnet run --project src/BeatTrack.App` | Everything: profile, gaps, slices, new interests, surging, re-engagement, strange absences |
| "What am I currently into?" | Run full, look at `new_interests` and `surging` sections | Artists appearing for the first time or accelerating recently |
| "What should I revisit?" | Run full, look at `Re-engage` and `dormant favorites` sections | Forgotten favorites similar to current listening |
| "What artists am I surprisingly not listening to?" | Run full, look at `Strange absences` section | Artists similar to many active favorites but completely absent |
| "What music am I missing?" | Run full, look at `Gap analysis` section | Artists in the similarity graph of your favorites that you've never heard |

### Live scrobble feed (requires LASTFM_API_KEY)

| User prompt | Command |
| --- | --- |
| "What's playing right now?" | `LASTFM_API_KEY=KEY dotnet run --project src/BeatTrack.LastFm.App -- live -n 5 USERNAME` |
| "Follow my scrobbles" | `LASTFM_API_KEY=KEY dotnet run --project src/BeatTrack.LastFm.App -- live -f USERNAME` |

## Combining Queries for Playlists

To build a personalized playlist recommendation, run multiple queries and synthesize:

### Step 1: Assess current state

```bash
dotnet run --project src/BeatTrack.App -- top-artists --window 7d
dotnet run --project src/BeatTrack.App -- top-artists --window 30d
```

Identify the user's current rotation and recent favorites.

### Step 2: Find what to add

Run the full analysis and extract:
- **Surging artists** — double down on what's hot
- **Re-engagement suggestions** — dormant favorites that fit the current mood
- **Strange absences with low seed counts (3-5)** — targeted recommendations, not universal connectors

### Step 3: Build the playlist

Mix from three buckets:
- 40% current favorites (from top-artists 7d)
- 30% re-engagement picks (dormant favorites similar to current listening)
- 30% discovery (strange absences or gap artists with highest seed overlap to current rotation)

Present as a named list the user can transfer to their streaming platform.

## Updating Data

When the user wants fresh data:

1. Re-download scrobble history CSV from lastfmstats.com (replaces old file)
2. Re-export Discogs collection if collection changed
3. Re-download YouTube Takeout if needed
4. Delete `~/.beattrack/cache/` to force re-resolution of MBIDs and similar artists
5. Re-run: `dotnet run --project src/BeatTrack.App`

The MBID cache (`~/.beattrack/cache/mbid-cache.md`) and similar artist caches (`~/.beattrack/cache/similar-artists/`) persist across runs and grow incrementally. Only delete them to force a full refresh.

## Adding New Queries

Queries live in `src/BeatTrack.Core/Queries/` as static classes.

### Pattern

```csharp
namespace BeatTrack.Core.Queries;

public static class MyQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        // Parse args
        var window = ParseStringFlag(args, "--window") ?? "30d";

        // Filter scrobbles by time window
        var cutoffMs = TopArtistsQuery.ParseWindowCutoff(window);
        var filtered = scrobbles.Where(s => s.TimestampMs >= cutoffMs && s.TimestampMs > 0);

        // Group by canonical artist name
        var groups = filtered.GroupBy(
            s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName),
            StringComparer.OrdinalIgnoreCase);

        // Analyze and output
        Console.WriteLine("my_query:");
        foreach (var g in groups.OrderByDescending(g => g.Count()).Take(20))
        {
            Console.WriteLine($"  {g.First().ArtistName}  ({g.Count()})");
        }

        return 0;
    }
}
```

### Register the command

Add routing in `src/BeatTrack.App/Program.cs` at the top-level command dispatch:

```csharp
if (args.Length > 0 && args[0].ToLowerInvariant() is "stats" or "streaks" or "top-artists" or "artist-velocity" or "my-query")
```

And add the case:

```csharp
"my-query" => MyQuery.Run(allScrobbles, args[1..]),
```

### Key types

| Type | Use for |
| --- | --- |
| `LastFmScrobble` | Raw scrobble data: `ArtistName`, `Album`, `Track`, `TimestampMs` |
| `BeatTrackSlice` | Weighted artist bag — build with `new BeatTrackSlice(name, weightDict)` |
| `BeatTrackSliceComparer.Compare(a, b)` | Three-way diff: shared, only-A, only-B |
| `ArtistNameMatcher` | Fuzzy name resolution across sources |
| `MbidCache` | Load/save artist → MusicBrainz ID mappings |
| `MarkdownTableStore` | Persist tabular data as markdown tables |
| `TopArtistsQuery.ParseWindowCutoff("30d")` | Reusable time window parsing |
