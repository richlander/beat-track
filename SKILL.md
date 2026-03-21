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

Run `beat-track` with no arguments to see all available commands.

## Output Format

All commands produce structured markdown: `#` title, inline summary fields, and `##` sections with pipe tables. This format is both human-readable and machine-parseable. No imperative/ad-hoc formatting — every command renders through a view model.

## Command Architecture

Commands fall into three categories:

| Category | Commands | Speed | Network |
| --- | --- | --- | --- |
| **Data acquisition** | `pull`, `snapshot` | Minutes (downloads full history) | Last.fm API |
| **Enrichment** | `learn`, `learn "Artist"` | ~3s per artist (rate-limited) | MusicBrainz + ListenBrainz |
| **Analysis** | `momentum`, `top-artists`, `stats`, `analyze`, all others | Seconds (local only) | None |

**Key principle**: analysis commands never call external services. All API work happens in `pull`, `snapshot`, and `learn`. If similarity or metadata is missing from `analyze`, run `learn` first — don't expect `analyze` to fetch it.

`learn` accepts specific artists (`beat-track learn "MUNYA" "Mr Twin Sister"`) or top-N by play count (`beat-track learn --top 100`). Each artist takes ~3 seconds due to MusicBrainz rate limits. For each artist it fetches genre tags, origin city/country, band membership (MusicBrainz) and similar-to relationships (ListenBrainz), writing everything to shelf.

---

## Workflow 1: First-Run Setup

### Step 1: Check what's already configured

```bash
beat-track status
```

This shows config, data sources (with file ages), and actionable suggestions. Start here every time — it tells you exactly what's missing.

### Step 2: Set up Last.fm API access

This is the only required step. With an API key and username, beat-track can fetch your complete listening history automatically.

1. Get a free API key at https://www.last.fm/api/accounts (instant, no approval)
2. Write `~/.config/beat-track/config`:

```
lastfm_api_key=YOUR_KEY
lastfm_user=YOUR_USERNAME
```

### Step 3: Download your data

```bash
beat-track pull     # full scrobble history (~1 min for 60k scrobbles)
beat-track snapshot    # top artists by period, loved tracks, MBIDs
```

After this, all baseline queries work: `momentum`, `top-artists`, `stats`, `new-discoveries`, `streaks`, `artist-depth`.

The `analyze` command additionally needs the snapshot for cross-source analysis. It is fully local — no API calls.

### Optional: supplementary sources

These enrich cross-source analysis but are not required:

| Source | What it adds | Where to place it |
| --- | --- | --- |
| Discogs collection CSV | Physical media ownership | `~/.local/share/beat-track/collection-csv/` |
| YouTube/Google Takeout | Video listening data | `~/.local/share/beat-track/takeout/extracted/Takeout/` |

---

## Workflow 2: Data Freshness

### What "recent enough" means

| Data source | What reads it | Freshness target | How to refresh |
| --- | --- | --- | --- |
| **Scrobble CSV** | All baseline queries | Re-download before any "this week" analysis | `beat-track pull` |
| **Snapshot JSON** | `analyze` (gap analysis, similarity) | Weekly or before deep analysis runs | `beat-track snapshot` |
| **Discogs CSV** | `analyze` (cross-source) | When collection changes | Re-export from Discogs |
| **YouTube Takeout** | `analyze` (cross-source) | Rarely changes | Re-download from Google Takeout |
| **Shelf (musicbrainz + listenbrainz)** | `learn` output: city, country, genre, member-of, similar-to | After adding new artists to rotation | `beat-track learn` or `beat-track learn "Artist"` |
| **Shelf (journal)** | Preferences (likes, misses) | Accumulates over time | Agent writes via `miss add` or `shelf like` |

### Before a "what's happening now" session

```bash
beat-track pull     # refresh scrobbles (captures today's plays)
beat-track momentum    # now this reflects the latest data
```

### Before a deep analysis session

```bash
beat-track pull        # refresh scrobbles
beat-track snapshot    # refresh top artists and loved tracks
beat-track learn       # enrich top artists with MusicBrainz metadata → shelf
beat-track analyze     # full cross-source analysis with fresh data
```

### How to check staleness

`beat-track status` shows file ages (e.g., "yesterday", "3 days ago", "2 months ago"). If the scrobble CSV is more than a day old, refresh before running time-windowed queries.

### Config precedence

1. Environment variables (`LASTFM_API_KEY`, `LASTFM_USER`, `BEAT_TRACK_DATA_DIR`, etc.)
2. Config file (`~/.config/beat-track/config`)
3. XDG defaults (`~/.local/share/beat-track/`, `~/.cache/beat-track/`)

---

## Workflow 3: Recording User Preferences

Users can declare preferences directly — these feed into recommendations even without listening history:

| User says | Action |
| --- | --- |
| "I love Slowdive" | Add to `~/.local/share/beat-track/my-favorites.md` |
| "I tried Johnny Marr, not for me" | `beat-track miss add "Johnny Marr" --reason "doesn't grab me"` |
| "Slowdive sounds like My Bloody Valentine" | Add to `~/.local/share/beat-track/my-similar-artists.md` |
| "Actually, give Johnny Marr another chance" | `beat-track miss remove "Johnny Marr"` |
| "Show my known misses" | `beat-track miss` |

Favorites and similarity entries are hand-edited markdown tables. Known misses have CLI commands for add/remove.

These are automatically used in analysis: favorites seed gap analysis, misses are excluded from all recommendations, and user-defined similarities supplement the ListenBrainz graph.

---

## Workflow 4: Baseline Reports

These require a scrobble CSV (from `pull` or manual import). They're fast and don't call any APIs.

| User prompt | Command | What it shows |
| --- | --- | --- |
| "What are my listening stats?" | `stats` | Eddington number, total artists, span, one-hit-wonders, busiest periods |
| "What's my momentum this week?" | `momentum` | Heating up, on repeat, new to you, comebacks, steady rotation, cooling off |
| "What changed this month?" | `momentum --window 30d` | Same momentum report over a 30-day window |
| "What am I listening to this week?" | `top-artists --window 7d` | Top artists by play count in last 7 days |
| "What are my top artists this year?" | `top-artists --window 365d` | Top artists by play count in last year |
| "Show me my listening streaks" | `streaks` | Longest consecutive-day streaks, overall and per-artist |
| "How long was my Massive Attack streak?" | `streaks --artist "Massive Attack"` | All streaks for a specific artist |
| "Show me how my top artists grew over time" | `artist-velocity --top 10 --bucket yearly` | Cumulative scrobble curves |
| "Any new discoveries this week?" | `new-discoveries --window 7d` | Engagement gradient: new discoveries, first clicks, rediscoveries |
| "Which artists do I explore deeply?" | `artist-depth --mode deep` | Catalog explorers: most unique tracks/albums |
| "Which artists am I a one-hit wonder for?" | `artist-depth --mode shallow` | One-hit wonders: high plays on 1-2 tracks |

---

## Workflow 5: Advanced Reports and Queries

These require a scrobble CSV + snapshot. All commands are fully local — no API calls.

### Full analysis

```bash
beat-track analyze
```

Runs everything: profile, gaps, slices, new interests, surging, re-engagement, strange absences.

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

When greeting the user or starting a session, run `momentum` first for a quick overview of what's shifting. For deeper engagement classification, run `new-discoveries --window 7d`. It classifies recent activity:

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

## Common Workflows

### "What should I listen to right now?"

1. `momentum` — what's heating up, on repeat, new, and coming back
2. `live -n 10` — see what's playing / recently played
3. `top-artists --window 7d` — current rotation
4. `beat-track analyze` then check `Strange absences` for targeted recommendations

### "Build me a playlist"

1. `top-artists --window 7d` + `top-artists --window 30d` — current rotation
2. `artist-depth --mode deep --limit 10` — true love artists
3. `new-discoveries --window 30d` — first clicks, rediscoveries, surging
4. `beat-track analyze` for re-engagement and strange absence picks
5. Mix: 40% current favorites, 30% re-engagement, 30% discovery

### "I want to discover new music"

1. `beat-track analyze` → `Gap analysis` section (similar to favorites, never heard)
2. Check `Strange absences` section (similar to many active artists, completely absent)
3. `artist-depth --mode shallow` — find covers/remixes that point to new artists
4. Validate every recommendation against scrobble data before presenting

### "Tell me about my listening habits"

1. `momentum` — what's shifting right now
2. `stats` — overall picture (span, Eddington number, one-hit-wonders)
3. `top-artists --window 365d` vs `top-artists --window 30d` — what's stable vs shifting
4. `streaks` — commitment patterns
5. `artist-velocity --top 10 --bucket yearly` — how taste evolved over time
6. `new-discoveries --window 60d` — engagement gradient

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
