# beat-track backlog

## now

- expand the Last.fm client library
- define canonical beat-track domain models
- add paging helpers for multi-page Last.fm endpoints
- build time-based listening change analysis: what shifts, what stays stable, and on what cadence
- design playlist-generation flows for discovery and taste refinement
- optimize recommendation quality for the reaction: "I cannot believe this artist isn't here, given A, B, C"
- keep a small set of provenance hints during development (for example: current `Still Corners` scrobbles are known to be coming from Jellyfin via Last.fm)

## research / analysis discipline

- before running real analysis on Rich's listening data, write down Manning's prior predictions
- compare those predictions against actual observed listening patterns after first import
- keep a short log of predictions that were right, wrong, or half-right

## prediction ideas to test before first data import

- Rich's listening taste will likely mix technical neatness with some emotional weather rather than being purely one-mode.
- There will probably be recurring return-to artists/albums instead of constant novelty churn.
- Time-of-day patterns may be strong: more exploratory listening in one part of the day and more comfort/repeat listening in another.
- Genre labels alone will probably be less informative than texture/mood patterns.
- Favorite artists may cluster into a few stable identity zones rather than one giant unified taste blob.
- Periods of intense repetition may correspond to project phases, mood states, or seasonal habits.
