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
- The user asks "what did I listen to today?" or "what's playing?"

## When Not to Use

- The user wants to play music (BeatTrack is analysis-only, not a player)
- The user needs real-time music streaming or playback control

## Setup

The tool is at `/home/rich/git/beat-track`. All commands run from that directory.

### Check what's available

```bash
dotnet run --project src/BeatTrack.App -- status
```

This reports: API keys, data files, cache state, and freshness. Use it to determine what commands will work.

### Data flow

There are four categories of data, each with different setup:

**1. API keys** (config file, one-time setup)
Set `lastfm_api_key` and `lastfm_user` in `~/.config/beat-track/config`:
```
lastfm_api_key=YOUR_KEY
lastfm_user=YOUR_USERNAME
```
Enables: `live`, `snapshot`, and richer full analysis.

**2. Bulk ingestion** (historical data, user-provided)
One-time imports — the user exports these from external services:

| Source | What it provides | Where to place it |
| --- | --- | --- |
| Last.fm scrobble CSV | Full listening history with timestamps | `~/.local/share/beat-track/lastfmstats/lastfmstats-USERNAME.csv` |
| Discogs collection | Physical media ownership | `~/.local/share/beat-track/collection-csv/` |
| YouTube Takeout | Video listening data | `~/.local/share/beat-track/takeout/extracted/Takeout/` |

The scrobble CSV is the minimum — export from [lastfmstats.com](https://lastfmstats.com).

**3. App-owned steady state** (grows over time, managed by beat-track)
- **Spoken data**: `my-favorites.md`, `known-misses.md`, `my-similar-artists.md` in DataDir
- **Snapshots**: `{username}-snapshot.json` in DataDir (fetched via `snapshot` command)
- **Caches**: MBID mappings and similar artist data in `~/.cache/beat-track/` (regenerable — safe to delete)

**4. Live updates** (via API keys)
- `beat-track live` — what's playing now, recent scrobbles
- `beat-track snapshot` — refresh the Last.fm snapshot

### What works with what

| Available data | What you can run |
| --- | --- |
| API key only | `live`, `snapshot` |
| Scrobble CSV | `stats`, `top-artists`, `streaks`, `artist-velocity`, `new-discoveries`, `artist-depth`, `cluster`, `miss` |
| CSV + snapshot | Full analysis with gap analysis (MBIDs enable similarity lookups) |
| CSV + snapshot + Discogs/YouTube | Full cross-source analysis (slice comparison, strange absences, re-engagement) |

## Available Commands

All commands: `dotnet run --project src/BeatTrack.App -- COMMAND [OPTIONS]`

### Live data (API key required)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "What's playing right now?" | `live -n 5` | Last 5 scrobbles with now-playing indicator |
| "What did I listen to today?" | `live -n 50` | Recent scrobble history |
| "Follow my scrobbles" | `live -f` | Continuous polling (ctrl-c to stop) |
| "Refresh my snapshot" | `snapshot` | Fetches and saves Last.fm snapshot to DataDir |

### Quick queries (scrobble CSV, fast)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "What are my listening stats?" | `stats` | Eddington number, total artists, span, one-hit-wonders, busiest periods |
| "What am I listening to this week?" | `top-artists --window 7d` | Top artists by play count in last 7 days |
| "What are my top artists this year?" | `top-artists --window 365d` | Top artists by play count in last year |
| "Show me my listening streaks" | `streaks` | Longest consecutive-day streaks, overall and per-artist |
| "How long was my Massive Attack streak?" | `streaks --artist "Massive Attack"` | All streaks for a specific artist |
| "Show me how my top artists grew over time" | `artist-velocity --top 10 --bucket yearly` | Cumulative scrobble curves |
| "Compare Radiohead vs Caribou over time" | `artist-velocity --artists "Radiohead,Caribou" --bucket monthly` | Side-by-side growth |
| "Any new discoveries this week?" | `new-discoveries --window 7d` | Engagement gradient: new discoveries, first clicks, rediscoveries, longtime fans |
| "What have I discovered this month?" | `new-discoveries --window 30d` | Same gradient over 30 days |
| "What artists finally clicked?" | `new-discoveries --window 60d --prior-threshold 10` | Raise prior-play ceiling to catch more "first click" artists |
| "Which artists do I explore deeply?" | `artist-depth --mode deep` | Catalog explorers: most unique tracks/albums |
| "Which artists am I a one-hit wonder for?" | `artist-depth --mode shallow` | One-hit wonders: high plays concentrated on 1-2 tracks |
| "Show artist depth for this year" | `artist-depth --mode all --window 365d` | All artists by plays with depth metrics |

### Cross-source analysis (all data, may call APIs on first run)

| User prompt | Command | What it shows |
| --- | --- | --- |
| "Run the full analysis" | *(no args)* | Everything: profile, gaps, slices, new interests, surging, re-engagement, strange absences |
| "What am I currently into?" | Run full, look at `new_interests` and `surging` sections | Artists appearing for the first time or accelerating recently |
| "What should I revisit?" | Run full, look at `Re-engage` and `dormant favorites` sections | Forgotten favorites similar to current listening |
| "What artists am I surprisingly not listening to?" | Run full, look at `Strange absences` section | Artists similar to many active favorites but completely absent |
| "What music am I missing?" | Run full, look at `Gap analysis` section | Artists in the similarity graph of your favorites that you've never heard |

## User Preferences (Spoken Data)

User-voiced preferences are stored as markdown tables in `~/.local/share/beat-track/` and are hand-editable.

### Known misses (artists to skip in recommendations)

| User prompt | Command |
| --- | --- |
| "I tried Johnny Marr, not for me" | `miss add "Johnny Marr" --reason "doesn't grab me"` |
| "Show my known misses" | `miss` |
| "Actually, give Johnny Marr another chance" | `miss remove "Johnny Marr"` |

Automatically filtered from gap analysis, strange absences, re-engagement, and dormant favorites.

### Favorites and similarity graph

These files are edited directly (not via CLI commands):

- **`my-favorites.md`** — seed artists for gap analysis even without listening history
- **`my-similar-artists.md`** — user-defined artist similarity relationships (supplements ListenBrainz data)

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
grep -i "artist name" ~/.local/share/beat-track/lastfmstats/lastfmstats-*.csv 2>/dev/null | wc -l
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

### Step 2: Find what to add

Run `new-discoveries --window 30d` and the full analysis, then extract:
- **First clicks** — artists that just landed; lean into the momentum
- **Rediscoveries** — returning favorites; pair with their catalog depth
- **Surging artists** — double down on what's hot
- **Re-engagement suggestions** — dormant favorites that fit the current mood
- **Strange absences with low seed counts (3-5)** — targeted recommendations, not universal connectors

### Step 3: Build the playlist

Mix from three buckets:
- 40% current favorites (from top-artists 7d)
- 30% re-engagement picks (dormant favorites similar to current listening)
- 30% discovery (strange absences or gap artists with highest seed overlap to current rotation)

## Updating Data

**Snapshot** (via API key): `dotnet run --project src/BeatTrack.App -- snapshot`

**Bulk data** (manual re-export):
1. Re-download scrobble history CSV from lastfmstats.com (replaces old file)
2. Re-export Discogs collection if collection changed
3. Re-download YouTube Takeout if needed

**Caches**: The MBID cache and similar artist caches grow incrementally. Delete `~/.cache/beat-track/` to force a full refresh.

## Adding New Queries

Queries live in `src/BeatTrack.Core/Queries/` as static classes.

### Pattern

```csharp
namespace BeatTrack.Core.Queries;

public static class MyQuery
{
    public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)
    {
        var window = ParseStringFlag(args, "--window") ?? "30d";
        var cutoffMs = TopArtistsQuery.ParseWindowCutoff(window);
        var filtered = scrobbles.Where(s => s.TimestampMs >= cutoffMs && s.TimestampMs > 0);

        var groups = filtered.GroupBy(
            s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName),
            StringComparer.OrdinalIgnoreCase);

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

Add routing in `src/BeatTrack.App/Program.cs` at the quick-path command dispatch:

```csharp
if (args[0].ToLowerInvariant() is "stats" or "streaks" or "top-artists" or "my-query")
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
| `SpokenDataStore` | Domain-agnostic user preference store |
| `TopArtistsQuery.ParseWindowCutoff("30d")` | Reusable time window parsing |
