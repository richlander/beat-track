# beat-track

Music-listening analysis infrastructure for personality, taste, and behavior inference.

## Current focus

- build a reusable Last.fm client library
- ingest and normalize listening data
- create analysis libraries on top of a canonical listening-event model
- track how Rich's listening changes (or does not change) over time
- generate playlists for discovery and taste refinement

## Repo layout

- `src/` — product libraries and applications
- `tests/` — test projects
- `artifacts/` — consolidated build output

## Development

This repo targets **.NET 11** and enables the Preview 1 runtime-async experiment.

```bash
dotnet test
```

Build defaults include:
- `EnablePreviewFeatures=true`
- `Features=$(Features);runtime-async=on`

## Notes

- repo naming follows `kebab-case-lower`
- JSON uses `snake_case_lower`
- `System.Text.Json` source generation is the default
- `[JsonPropertyName]` is only used where global naming policy is insufficient
- pre-analysis predictions are tracked in `docs/predictions.md`
