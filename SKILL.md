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

## Tool Location

All commands: `beat-track COMMAND [OPTIONS]`

---

## Workflow 1: Establishing the Environment

On first use, find out what the user has available. Ask them:

1. **Do you have a Last.fm account?** If yes, what's your username?
2. **Do you have a Last.fm API key?** (Get one at https://www.last.fm/api/accounts — it's free and instant.)
3. **Do you have any other music data?** Discogs collection export, YouTube/Google Takeout, etc.

Then check what's already configured:

```bash
beat-trackstatus
```

### Setting up the API key

This is the fastest path to a working system. With just an API key and username, you can fetch the user's complete listening history — no manual exports needed.

Write `~/.config/beat-track/config`:
```
lastfm_api_key=YOUR_KEY
lastfm_user=YOUR_USERNAME
```

### Adding supplementary data sources

These are optional and enrich cross-source analysis:

| Source | What it provides | Where to place it |
| --- | --- | --- |
| Discogs collection | Physical media ownership | `~/.local/share/beat-track/collection-csv/` |
| YouTube Takeout | Video listening data | `~/.local/share/beat-track/takeout/extracted/Takeout/` |

### Data the app manages

These grow over time and don't require user action:
- **Spoken data**: `my-favorites.md`, `known-misses.md`, `my-similar-artists.md` in `~/.local/share/beat-track/`
- **Snapshots**: `{username}-snapshot.json` in DataDir (fetched via `snapshot` command)
- **Caches**: MBID mappings and similar artist data in `~/.cache/beat-track/` (regenerable — safe to delete)

---

## Workflow 2: Acquiring Listening Data

### From the Last.fm API (recommended)

With an API key configured, fetch the user's complete history:

```bash
# Download full scrobble history (~1 min for 60k scrobbles)
beat-trackhistory

# Fetch a snapshot (top artists by period, loved tracks, MBIDs)
beat-tracksnapshot
```

`history` produces a CSV at `~/.local/share/beat-track/lastfmstats/lastfmstats-USERNAME.csv` that all queries read from. `snapshot` produces a JSON file that enables gap analysis and similarity lookups.

### From spoken data

Users can declare preferences directly — these feed into recommendations even without listening history:

| User says | Action |
| --- | --- |
| "I love Slowdive" | Add to `~/.local/share/beat-track/my-favorites.md` |
| "I tried Johnny Marr, not for me" | `miss add "Johnny Marr" --reason "doesn't grab me"` |
| "Slowdive sounds like My Bloody Valentine" | Add to `~/.local/share/beat-track/my-similar-artists.md` |
| "Actually, give Johnny Marr another chance" | `miss remove "Johnny Marr"` |
| "Show my known misses" | `miss` |

Favorites and similarity entries are hand-edited markdown tables. Known misses have CLI commands for add/remove.

These are automatically used in analysis: favorites seed gap analysis, misses are excluded from all recommendations, and user-defined similarities supplement the ListenBrainz graph.

---

## Workflow 3: Baseline Reports

These require a scrobble CSV (from `history` or manual import). They're fast and don't call any APIs.

| User prompt | Command | What it shows |
| --- | --- | --- |
| "What are my listening stats?" | `stats` | Eddington number, total artists, span, one-hit-wonders, busiest periods |
| "What am I listening to this week?" | `top-artists --window 7d` | Top artists by play count in last 7 days |
| "What are my top artists this year?" | `top-artists --window 365d` | Top artists by play count in last year |
| "Show me my listening streaks" | `streaks` | Longest consecutive-day streaks, overall and per-artist |
| "How long was my Massive Attack streak?" | `streaks --artist "Massive Attack"` | All streaks for a specific artist |
| "Show me how my top artists grew over time" | `artist-velocity --top 10 --bucket yearly` | Cumulative scrobble curves |
| "Any new discoveries this week?" | `new-discoveries --window 7d` | Engagement gradient: new discoveries, first clicks, rediscoveries |
| "Which artists do I explore deeply?" | `artist-depth --mode deep` | Catalog explorers: most unique tracks/albums |
| "Which artists am I a one-hit wonder for?" | `artist-depth --mode shallow` | One-hit wonders: high plays on 1-2 tracks |

---

## Workflow 4: Advanced Reports and Queries

These require a scrobble CSV + snapshot. May call ListenBrainz/MusicBrainz APIs on first run (results are cached).

### Full analysis

```bash
beat-track
```

Runs everything (no args): profile, gaps, slices, new interests, surging, re-engagement, strange absences.

| User prompt | What to look at |
| --- | --- |
| "What am I currently into?" | `new_interests` and `surging` sections |
| "What should I revisit?" | `Re-engage` and `dormant favorites` sections |
| "What artists am I surprisingly not listening to?" | `Strange absences` section |
| "What music am I missing?" | `Gap analysis` section |

### Live data queries (API key required)

| User prompt | Command |
| --- | --- |
| "What's playing right now?" | `live -n 5` |
| "What did I listen to today?" | `live -n 50` |
| "Follow my scrobbles" | `live -f` |

### Proactive discovery check

When greeting the user or starting a session, run `new-discoveries --window 7d`. It classifies recent activity:

| Category | What it means | How to respond |
| --- | --- | --- |
| **New discovery** | Zero prior plays | "You've picked up X this week" — offer similar artists |
| **First click** | Had stray plays before, now truly engaging | "X seems to have clicked" — most interesting signal |
| **Rediscovery** | Was dormant, now back | "Welcome back to X" — suggest what else to revisit |
| **Longtime fan** | Continuously engaged | Don't comment unless asked |

Only comment on real engagement: **10+ plays** = genuine interest, **3-9** = mention briefly, **1-2** = ignore completely. Statements like "I see you played X" when X has 1-2 plays feel like surveillance, not insight.

### Validating recommendations

**Never recommend an artist without checking the scrobble data first.** Saying "try Alvvays" to someone with 600+ scrobbles signals you aren't paying attention.

```bash
grep -i "artist name" ~/.local/share/beat-track/lastfmstats/lastfmstats-*.csv 2>/dev/null | wc -l
```

| What the data shows | How to frame it |
| --- | --- |
| **Zero plays** | True discovery — "you haven't heard X, and they fit because..." |
| **A few plays, long ago** | First click — "X has been on your radar but never clicked..." |
| **Significant plays, then a gap** | Revisit — "you used to be into X..." |
| **Hundreds of plays, 1-2 albums** | Catalog dig — point to albums/tracks they're missing |
| **Hundreds of plays, broad catalog** | Already a fan — use as taste anchor, don't recommend |

The **catalog dig** is the highest-value recommendation. Use `artist-depth --mode all` to find artists where `TopTrackShare` is high or `Albums` is low relative to their discography.

---

## Workflow 5: Updating Listening Data

### Refreshing from the API

```bash
# Re-download full history (overwrites existing CSV)
beat-trackhistory

# Refresh snapshot (top artists, loved tracks, MBIDs)
beat-tracksnapshot
```

### Refreshing supplementary sources

- **Discogs**: Re-export CSV from Discogs if collection changed
- **YouTube**: Re-download Google Takeout if needed

### Cache management

Caches grow incrementally and are safe to delete:
```bash
rm -rf ~/.cache/beat-track/
```
They'll rebuild on the next full analysis run.

---

## Common Workflows

### "What should I listen to right now?"

1. `live -n 10` — see what's playing / recently played
2. `new-discoveries --window 7d` — what's new or clicking
3. `top-artists --window 7d` — current rotation
4. Use full analysis `Strange absences` for targeted recommendations

### "Build me a playlist"

1. `top-artists --window 7d` + `top-artists --window 30d` — current rotation
2. `artist-depth --mode deep --limit 10` — true love artists
3. `new-discoveries --window 30d` — first clicks, rediscoveries, surging
4. Run full analysis for re-engagement and strange absence picks
5. Mix: 40% current favorites, 30% re-engagement, 30% discovery

### "I want to discover new music"

1. Run full analysis → `Gap analysis` section (similar to favorites, never heard)
2. `Strange absences` (similar to many active artists, completely absent)
3. `artist-depth --mode shallow` — find covers/remixes that point to new artists
4. Validate every recommendation against scrobble data before presenting

### "Tell me about my listening habits"

1. `stats` — overall picture (span, Eddington number, one-hit-wonders)
2. `top-artists --window 365d` vs `top-artists --window 30d` — what's stable vs shifting
3. `streaks` — commitment patterns
4. `artist-velocity --top 10 --bucket yearly` — how taste evolved over time
5. `new-discoveries --window 60d` — engagement gradient

### Recording user preferences during conversation

When the user mentions artists they like or dislike during any conversation:

- **Likes**: Add to `my-favorites.md` if they express strong preference
- **Dislikes**: `miss add "Artist" --reason "reason"` if they reject a recommendation
- **Relationships**: Add to `my-similar-artists.md` if they say "X sounds like Y"

These accumulate over time and improve future recommendations.

---

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

Register in `src/BeatTrack.App/Program.cs` at the quick-path command dispatch.

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
