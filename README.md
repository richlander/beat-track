# beat-track

Cross-source music listening analysis. Merges data from Last.fm, YouTube, and Discogs into a unified profile, then runs queries to surface insights — what's new, what's surging, what you're missing, and what you should revisit.

Built with .NET 11, AOT-compatible, zero external runtime dependencies.

## Quick start

```bash
git clone https://github.com/richlander/beat-track.git
cd beat-track
dotnet build
```

Start with your Last.fm scrobble history (see [Data sources](#data-sources)), then:

```bash
dotnet run --project src/BeatTrack.App -- stats
dotnet run --project src/BeatTrack.App -- top-artists --window 30d
dotnet run --project src/BeatTrack.App -- streaks
dotnet run --project src/BeatTrack.App -- artist-velocity --top 5 --bucket yearly
```

## Using without data

You can explore the tool's queries and code without any data. The scrobble-only queries (`stats`, `streaks`, `top-artists`, `artist-velocity`) need a single CSV file. The full cross-source analysis needs at least two sources to be interesting.

If you don't have a Last.fm account, you can create one and start scrobbling today — the tool works with any amount of data. Even a week of scrobbles is enough for `top-artists` and `stats`.

## Data sources

Each source is optional. Start with one and add more over time.

### Last.fm scrobble history (recommended starting point)

Export your full scrobble history from [lastfmstats.com](https://lastfmstats.com):

1. Go to `lastfmstats.com/user/YOUR_USERNAME`
2. Wait for it to load all scrobbles
3. Click **CSV** (top right)
4. Place the file at `~/.local/share/beat-track/lastfmstats/lastfmstats-YOUR_USERNAME.csv`

The CSV is semicolon-delimited: `Artist;Album;AlbumId;Track;Date` (epoch milliseconds).

### Last.fm API snapshot (adds top artist MBIDs)

For richer metadata (MusicBrainz IDs, top artists by time period, loved tracks):

1. Get a [Last.fm API key](https://www.last.fm/api/account/create)
2. Run the snapshot tool:

```bash
LASTFM_API_KEY=your_key dotnet run --project src/BeatTrack.LastFm.App -- YOUR_USERNAME
```

The snapshot JSON is written to stdout or to a path via `BEAT_TRACK_OUTPUT_PATH`. Place it at `~/.local/share/beat-track/YOUR_USERNAME-snapshot.json`.

#### Live scrobble feed

```bash
# Show last 10 scrobbles
LASTFM_API_KEY=your_key dotnet run --project src/BeatTrack.LastFm.App -- live -n 10 YOUR_USERNAME

# Follow mode — polls every 15s, ctrl-c to stop
LASTFM_API_KEY=your_key dotnet run --project src/BeatTrack.LastFm.App -- live -f YOUR_USERNAME

# Show last 5, then follow
LASTFM_API_KEY=your_key dotnet run --project src/BeatTrack.LastFm.App -- live -f -n 5 YOUR_USERNAME
```

### Discogs collection (adds physical media)

1. Go to your Discogs collection page
2. Click **Export** → CSV
3. Place the file at `~/.local/share/beat-track/collection-csv/YOUR_USERNAME-collection-*.csv`

### YouTube watch history (adds video listening)

1. Go to [takeout.google.com](https://takeout.google.com)
2. Select only **YouTube and YouTube Music**
3. Download and extract
4. Place at `~/.local/share/beat-track/takeout/extracted/Takeout/`

BeatTrack classifies YouTube watch history to identify music content using known artist matching, VEVO/Topic channel detection, music platform recognition (KEXP, Sub Pop, 3voor12, Cercle, etc.), and title pattern analysis.

## Queries

### Scrobble-only queries (fast, no API calls)

```bash
# General listening statistics — Eddington number, span, one-hit-wonders
dotnet run --project src/BeatTrack.App -- stats

# Top artists with time windows (7d, 30d, 90d, 365d, all)
dotnet run --project src/BeatTrack.App -- top-artists --window 7d
dotnet run --project src/BeatTrack.App -- top-artists --window 365d --limit 30

# Listening streaks — overall and per-artist
dotnet run --project src/BeatTrack.App -- streaks
dotnet run --project src/BeatTrack.App -- streaks --artist "Massive Attack"

# Cumulative play count curves over time
dotnet run --project src/BeatTrack.App -- artist-velocity --top 10 --bucket yearly
dotnet run --project src/BeatTrack.App -- artist-velocity --artists "Radiohead,Björk,Caribou" --bucket monthly

# New discoveries — artists you started listening to within a time window
dotnet run --project src/BeatTrack.App -- new-discoveries --window 7d
dotnet run --project src/BeatTrack.App -- new-discoveries --window 30d

# Artist depth — catalog explorers vs one-hit wonders
dotnet run --project src/BeatTrack.App -- artist-depth --mode deep     # who you explore deeply
dotnet run --project src/BeatTrack.App -- artist-depth --mode shallow  # one-hit wonders
dotnet run --project src/BeatTrack.App -- artist-depth --mode all --window 365d --min 10
```

### Cross-source analysis (loads all data sources)

Run without a command for the full analysis:

```bash
dotnet run --project src/BeatTrack.App
```

This produces:

- **Unified profile** — merged artist/release data across all sources
- **Cross-source breakdown** — which artists appear in 1, 2, or all 3 sources
- **MBID resolution** — maps artist names to MusicBrainz IDs (cached at `~/.cache/beat-track/mbid-cache.md`)
- **Gap analysis** — artists similar to your favorites that you've never listened to
- **Slice comparison** — Last.fm vs YouTube artist overlap with time windows
- **New interests** — artists appearing for the first time in the last 60 days
- **Surging** — artists with disproportionately high recent play rates
- **Re-engagement** — dormant favorites similar to what you're currently into
- **Strange absences** — artists surrounded by your favorites but completely absent from your data

### Known misses

Track artists you've tried but don't connect with. These are automatically excluded from gap analysis, strange absences, re-engagement, and dormant favorites recommendations.

```bash
# Add an artist (with optional reason)
dotnet run --project src/BeatTrack.App -- miss add "Johnny Marr" --reason "tried multiple times, doesn't grab me"

# List all known misses
dotnet run --project src/BeatTrack.App -- miss

# Remove (give them another chance)
dotnet run --project src/BeatTrack.App -- miss remove "Johnny Marr"
```

Known misses are stored at `~/.local/share/beat-track/known-misses.md`.

### Combining queries for playlists

Run multiple queries to build a listening plan:

1. `top-artists --window 7d` to see current rotation
2. `streaks` to find artists you've been on a run with
3. Full analysis for re-engagement suggestions and strange absences
4. Use the output to build a playlist mixing current favorites with recommended revisits and discoveries

## Data directory layout

BeatTrack follows the [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir-spec/latest/). Data and cache are stored separately so you can safely clear the cache without losing user data.

```
~/.local/share/beat-track/            # Persistent user data ($XDG_DATA_HOME/beat-track)
├── lastfmstats/
│   └── lastfmstats-*.csv            # Scrobble history export
├── collection-csv/
│   └── *-collection-*.csv           # Discogs CSV export
├── takeout/
│   └── extracted/Takeout/...        # YouTube Takeout
├── *-snapshot.json                  # Last.fm API snapshot
└── known-misses.md                  # Artists excluded from recommendations

~/.cache/beat-track/                  # Regenerable cache ($XDG_CACHE_HOME/beat-track)
├── mbid-cache.md                    # Artist → MusicBrainz ID mappings
└── similar-artists/                 # ListenBrainz similarity data
    └── {mbid}.md
```

Legacy `~/.beattrack/` paths are still checked as a fallback.

## Updating data

Re-export your data sources and replace the files. Caches persist across runs — delete `~/.cache/beat-track/` to force a full refresh.

```bash
# Re-download scrobble history from lastfmstats.com, then:
dotnet run --project src/BeatTrack.App -- stats

# Re-run full analysis with fresh data
dotnet run --project src/BeatTrack.App
```

## Cache format

Caches are stored as markdown pipe tables — human-readable, GitHub-renderable, hand-editable:

```markdown
| canonical_name | mbid | matched_name | source |
| --- | --- | --- | --- |
| radiohead | a74b1b7f-... | Radiohead | lastfm |
| belle and sebastian | 59ea0a7b-... | Belle and Sebastian | lastfm |
```

## Architecture

```
BeatTrack.Core          # Models, parsers, analysis, queries (zero dependencies)
BeatTrack.LastFm        # Last.fm API client
BeatTrack.YouTube       # YouTube Takeout parser + music classifier
BeatTrack.Discogs       # Discogs CSV parser
BeatTrack.App           # Main CLI — unified analysis + queries
BeatTrack.LastFm.App    # Last.fm CLI — snapshots + live scrobble feed
```

### Adding queries

Query logic lives in `BeatTrack.Core/Queries/` as static classes:

1. Create `src/BeatTrack.Core/Queries/YourQuery.cs`
2. Implement `public static int Run(IReadOnlyList<LastFmScrobble> scrobbles, string[] args)`
3. Add routing in the command dispatch block at the top of `src/BeatTrack.App/Program.cs`

Key types to build on:

| Type | Purpose |
| --- | --- |
| `LastFmScrobble` | Single scrobble: `ArtistName`, `Album`, `Track`, `TimestampMs` |
| `BeatTrackSlice` | Weighted artist bag for any filtered view of the data |
| `BeatTrackSliceComparer` | Three-way comparison: shared, only-A, only-B |
| `ArtistNameMatcher` | Fuzzy name resolution (spaceless, ASCII-fold, Levenshtein) |
| `MbidCache` | Persistent artist → MusicBrainz ID mappings |
| `MarkdownTableStore` | Read/write markdown tables for caches |
| `BeatTrackAnalysis.CanonicalizeArtistName` | Universal join key across sources |

### Building new tools on the core

Reference `BeatTrack.Core` from a new project:

```xml
<ProjectReference Include="../BeatTrack.Core/BeatTrack.Core.csproj" />
```

Load scrobble data with `LastFmStatsCsvReader.ParseCsv(reader)` and build analysis on top.

## Development

Targets .NET 11 preview. Build defaults:

- `TreatWarningsAsErrors=true`
- `IsAotCompatible=true`
- `System.Text.Json` source generation (no reflection)
- JSON uses `snake_case_lower` naming policy
- Repo naming follows `kebab-case-lower`

```bash
dotnet test
dotnet run --project src/BeatTrack.App -- stats
```

## License

MIT
