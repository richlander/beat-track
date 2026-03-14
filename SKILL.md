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

## Check What's Available

Before running any commands, check what data and API keys are present. Run these checks and note the results — they determine which commands will work.

The tool searches multiple directories for each data file (env var, then workspace, then project, then home). The checks below mirror that search order.

### Data files

```bash
# Scrobble CSV (required for all quick queries)
# Search: env var → workspace → home
ls ${BEAT_TRACK_LASTFM_STATS_CSV:-/dev/null} \
   ~/.openclaw/workspace/data/lastfmstats/lastfmstats-*.csv \
   ~/.beattrack/data/lastfmstats/lastfmstats-*.csv 2>/dev/null

# Last.fm API snapshot (needed for full analysis MBIDs)
# Search: env var → project data/ → workspace → home
ls ${BEAT_TRACK_SNAPSHOT_PATH:-/dev/null} \
   ~/.openclaw/workspace/git/beat-track/data/*-snapshot.json \
   ~/.openclaw/workspace/data/*-snapshot.json \
   ~/.beattrack/data/*-snapshot.json 2>/dev/null

# Discogs collection (needed for cross-source analysis)
# Search: env var → workspace → home
ls ${BEAT_TRACK_DISCOGS_CSV:-/dev/null} \
   ~/.openclaw/workspace/data/collection-csv/*-collection-*.csv \
   ~/.beattrack/data/collection-csv/*-collection-*.csv 2>/dev/null

# YouTube Takeout (needed for cross-source analysis)
# Search: env var → workspace → home
ls ${BEAT_TRACK_TAKEOUT_DIR:-/dev/null}/extracted/Takeout/YouTube\ and\ YouTube\ Music/history/watch-history.html \
   ~/.openclaw/workspace/data/takeout/extracted/Takeout/YouTube\ and\ YouTube\ Music/history/watch-history.html \
   ~/.beattrack/data/takeout/extracted/Takeout/YouTube\ and\ YouTube\ Music/history/watch-history.html 2>/dev/null

# MBID and similar artist caches (grow over time, not required)
ls ${BEAT_TRACK_CACHE_DIR:-~/.beattrack/cache}/mbid-cache.md 2>/dev/null
ls ${BEAT_TRACK_CACHE_DIR:-~/.beattrack/cache}/similar-artists/*.md 2>/dev/null | wc -l
```

### API keys

```bash
# Last.fm API key (needed for live scrobble feed and snapshot fetching)
# NOT needed for quick queries or full analysis if snapshot already exists
echo "${LASTFM_API_KEY:+set}"
```

### What works with what

| Available data | What you can run |
| --- | --- |
| Scrobble CSV only | All quick queries: `stats`, `top-artists`, `streaks`, `artist-velocity`, `new-discoveries`, `artist-depth`, `miss` |
| + Snapshot | Full analysis with gap analysis (MBIDs enable similarity lookups) |
| + Discogs and/or YouTube | Full cross-source analysis (slice comparison, strange absences, re-engagement) |
| `LASTFM_API_KEY` set | Live scrobble feed, snapshot fetching |

If nothing is found, help the user set up their first data source (see below).

## Setting Up Data

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
| "Any new discoveries this week?" | `dotnet run --project src/BeatTrack.App -- new-discoveries --window 7d` | Engagement gradient: new discoveries, first clicks, rediscoveries, longtime fans |
| "What have I discovered this month?" | `dotnet run --project src/BeatTrack.App -- new-discoveries --window 30d` | Same gradient over 30 days |
| "What artists finally clicked?" | `dotnet run --project src/BeatTrack.App -- new-discoveries --window 60d --prior-threshold 10` | Raise prior-play ceiling to catch more "first click" artists |
| "Which artists do I explore deeply?" | `dotnet run --project src/BeatTrack.App -- artist-depth --mode deep` | Catalog explorers: most unique tracks/albums |
| "Which artists am I a one-hit wonder for?" | `dotnet run --project src/BeatTrack.App -- artist-depth --mode shallow` | One-hit wonders: high plays concentrated on 1-2 tracks |
| "Show artist depth for this year" | `dotnet run --project src/BeatTrack.App -- artist-depth --mode all --window 365d` | All artists by plays with depth metrics |

### Cross-source analysis (all data, may call APIs on first run)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "Run the full analysis" | `dotnet run --project src/BeatTrack.App` | Everything: profile, gaps, slices, new interests, surging, re-engagement, strange absences |
| "What am I currently into?" | Run full, look at `new_interests` and `surging` sections | Artists appearing for the first time or accelerating recently |
| "What should I revisit?" | Run full, look at `Re-engage` and `dormant favorites` sections | Forgotten favorites similar to current listening |
| "What artists am I surprisingly not listening to?" | Run full, look at `Strange absences` section | Artists similar to many active favorites but completely absent |
| "What music am I missing?" | Run full, look at `Gap analysis` section | Artists in the similarity graph of your favorites that you've never heard |

### Known misses (artists to skip in recommendations)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "I tried Johnny Marr, not for me" | `dotnet run --project src/BeatTrack.App -- miss add "Johnny Marr" --reason "doesn't grab me"` | Adds to known misses, excluded from future recommendations |
| "Show my known misses" | `dotnet run --project src/BeatTrack.App -- miss` | Lists all artists marked as misses |
| "Actually, give Johnny Marr another chance" | `dotnet run --project src/BeatTrack.App -- miss remove "Johnny Marr"` | Removes from known misses |

Known misses are stored at `~/.beattrack/data/known-misses.md` and are automatically filtered from gap analysis, strange absences, re-engagement, and dormant favorites in the full analysis.

### Live scrobble feed (requires LASTFM_API_KEY)

| User prompt | Command |
| --- | --- |
| "What's playing right now?" | `LASTFM_API_KEY=KEY dotnet run --project src/BeatTrack.LastFm.App -- live -n 5 USERNAME` |
| "Follow my scrobbles" | `LASTFM_API_KEY=KEY dotnet run --project src/BeatTrack.LastFm.App -- live -f USERNAME` |

## Proactive Discovery Check

When greeting the user or starting a session, run `new-discoveries --window 7d` to check for recent activity. The query classifies active artists along an engagement gradient:

| Category | What it means | How to respond |
| --- | --- | --- |
| **New discovery** | Zero prior plays — completely new artist | "You've picked up X this week" — offer similar artists or playlist suggestions |
| **First click** | Had a few stray plays before, but now truly engaging | "X seems to have clicked" — this is often the most interesting signal; the artist was on the radar but never landed until now |
| **Rediscovery** | Real prior engagement, went dormant, now back | "Welcome back to X" — acknowledge the return, suggest what else they might revisit |
| **Longtime fan** | Continuously engaged, no gap | Don't comment unless asked — this is expected behavior, not news |

Only comment on artists that show real engagement:

1. **10+ plays** — genuine interest. Comment and offer to find similar artists or build a playlist around them.
2. **3-9 plays** — possible interest. Mention briefly, ask if they're enjoying it.
3. **1-2 plays** — ignore. A single play is not a signal. Do not mention one-off plays to the user — it feels noisy and presumptuous.

**First click** and **rediscovery** are the most conversation-worthy categories. A new discovery is obvious to the listener; a longtime fan needs no commentary. But "this artist finally clicked" or "you've come back to this one" surfaces something the listener may not have consciously noticed.

In general, never highlight an artist based on minimal listening. Statements like "I see you played X this week" when X has 1-2 plays come across as surveillance, not insight. Wait for the data to show a pattern before surfacing it.

## Validating Recommendations

**Never recommend an artist without checking the scrobble data first.** Every artist you suggest must be checked against the listening history so you can frame the recommendation correctly. Saying "try Alvvays" to someone with 600+ scrobbles is worse than no recommendation — it signals you aren't paying attention.

Before presenting any recommendation, grep the scrobble CSV for the artist:

```bash
grep -i "artist name" ~/.openclaw/workspace/data/lastfmstats/lastfmstats-*.csv 2>/dev/null | wc -l
```

Then frame the recommendation based on what you find:

| What the data shows | How to frame it |
| --- | --- |
| **Zero plays** | True discovery — "you haven't heard X, and they fit because..." |
| **A few plays, long ago** | First click opportunity — "X has been on your radar but never clicked — now might be the time because..." |
| **Significant plays, then a gap** | Revisit — "you used to be into X — worth coming back to because..." |
| **Hundreds of plays, concentrated on 1-2 albums** | Catalog dig — don't say "try X." Instead: "you've got 400 plays of X but they're all Antisocialites and the self-titled — have you heard Blue Rev?" Point to the albums or tracks they're missing, especially newer releases or deep cuts. |
| **Hundreds of plays, broad catalog** | Already a true fan — don't recommend the artist at all. Use them as a taste anchor ("since you love X, try Y"). |

### The catalog dig is the highest-value recommendation

When someone is deeply invested in an artist but concentrated on a subset of the catalog, that's the easiest win. They already love the artist — you're just pointing to the room they haven't walked into yet. Use `artist-depth` to identify these:

```bash
# Check specific artist depth — look at UniqueTracks, Albums, TopTrackShare
dotnet run --project src/BeatTrack.App -- artist-depth --mode all --window all
```

If `TopTrackShare` is high or `Albums` is low relative to the artist's actual discography, there's unexplored material. Prioritize:
1. **Newer releases** they may not know about
2. **Earlier/deeper albums** that match the sound of what they already play
3. **Tracks by the same artist** that are stylistically close to their most-played

### Depth-aware recommendation types

- **Deep mode** (`--mode deep`) surfaces `true_love_artists` — high track diversity across many months. These are the user's core taste and the best seeds for similar-artist recommendations.
- **Shallow mode** (`--mode shallow`) surfaces one-hit wonders with actionable recommendations:
  - **Covers** are detected automatically (e.g. "Get Lucky (Daft Punk cover)" by Daughter → recommend Daft Punk)
  - **Remixes** point to the original artist or remixer
  - **Track fixations** suggest exploring that artist's broader catalog

## Combining Queries for Playlists

To build a personalized playlist recommendation, run multiple queries and synthesize:

### Step 1: Assess current state

```bash
dotnet run --project src/BeatTrack.App -- top-artists --window 7d
dotnet run --project src/BeatTrack.App -- top-artists --window 30d
dotnet run --project src/BeatTrack.App -- artist-depth --mode deep --limit 10
```

Identify the user's current rotation, recent favorites, and true love artists.

### Step 2: Find what to add

Run `new-discoveries --window 30d` and the full analysis, then extract:
- **First clicks** — artists that just landed; lean into the momentum
- **Rediscoveries** — returning favorites; pair with their catalog depth
- **Surging artists** — double down on what's hot
- **Re-engagement suggestions** — dormant favorites that fit the current mood
- **Strange absences with low seed counts (3-5)** — targeted recommendations, not universal connectors
- **Shallow one-hit wonders** (`artist-depth --mode shallow`) — check if the user should explore the original artist (covers) or the artist's catalog

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
