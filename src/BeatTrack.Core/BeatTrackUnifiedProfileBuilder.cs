namespace BeatTrack.Core;

public static class BeatTrackUnifiedProfileBuilder
{
    public static BeatTrackUnifiedProfile Build(
        BeatTrackSnapshot? lastFmSnapshot = null,
        BeatTrackYouTubeSnapshot? youTubeSnapshot = null,
        BeatTrackDiscogsSnapshot? discogsSnapshot = null)
    {
        var artistRefs = new Dictionary<string, List<BeatTrackSourceReference>>(StringComparer.OrdinalIgnoreCase);
        var releaseRefs = new Dictionary<string, (string Title, string? ArtistCanonical, int? Year, List<BeatTrackSourceReference> Sources)>(StringComparer.OrdinalIgnoreCase);

        // Build a set of known individual artist names from Last.fm and YouTube
        // so we can validate Discogs collab splits
        var knownArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Last.fm artists
        if (lastFmSnapshot is not null)
        {
            foreach (var track in lastFmSnapshot.RecentTracks)
            {
                if (!string.IsNullOrWhiteSpace(track.ArtistName))
                {
                    AddArtist(artistRefs, track.ArtistName, BeatTrackSource.LastFm, sourceUrl: track.Url);
                    knownArtists.Add(track.ArtistName);
                }
            }

            foreach (var (_, periodArtists) in lastFmSnapshot.TopArtistsByPeriod)
            {
                foreach (var artist in periodArtists)
                {
                    AddArtist(artistRefs, artist.Name, BeatTrackSource.LastFm, sourceId: artist.Mbid, sourceUrl: artist.Url);
                    knownArtists.Add(artist.Name);
                }
            }

            foreach (var track in lastFmSnapshot.LovedTracks)
            {
                if (!string.IsNullOrWhiteSpace(track.ArtistName))
                {
                    AddArtist(artistRefs, track.ArtistName, BeatTrackSource.LastFm, sourceUrl: track.Url);
                    knownArtists.Add(track.ArtistName);
                }
            }
        }

        // YouTube artists (from saved tracks and music-candidate watch events)
        if (youTubeSnapshot is not null)
        {
            foreach (var saved in youTubeSnapshot.SavedTracks)
            {
                foreach (var artistName in saved.ArtistNames)
                {
                    AddArtist(artistRefs, artistName, BeatTrackSource.YouTube);
                    knownArtists.Add(artistName);
                }

                AddRelease(releaseRefs, saved.Title, saved.ArtistNames.FirstOrDefault(), null,
                    BeatTrackSource.YouTube, sourceId: saved.SourceId, sourceUrl: saved.SourceUrl);
            }

            foreach (var watch in youTubeSnapshot.WatchEvents)
            {
                if (!watch.IsMusicCandidate || watch.MusicMatchReason is null)
                {
                    continue;
                }

                if (watch.MusicMatchReason.StartsWith("KnownArtist:", StringComparison.Ordinal))
                {
                    var artistName = watch.MusicMatchReason["KnownArtist:".Length..];
                    AddArtist(artistRefs, artistName, BeatTrackSource.YouTube, sourceUrl: watch.Url);
                    knownArtists.Add(artistName);
                }
                else if (watch.MusicMatchReason is "AutoMusicChannel" or "MusicPlatform"
                    && !string.IsNullOrWhiteSpace(watch.ChannelName))
                {
                    AddArtist(artistRefs, watch.ChannelName, BeatTrackSource.YouTube, sourceUrl: watch.Url);
                }

                // Extract featured/collab artists only from confirmed music content
                // (KnownArtist or AutoMusicChannel — not heuristic or platform matches)
                if (watch.MusicMatchReason.StartsWith("KnownArtist:", StringComparison.Ordinal)
                    || watch.MusicMatchReason is "AutoMusicChannel")
                {
                    var titleArtists = ArtistNameSplitter.ExtractArtistsFromTitle(watch.Title);
                    foreach (var extracted in titleArtists)
                    {
                        AddArtist(artistRefs, extracted, BeatTrackSource.YouTube, sourceUrl: watch.Url);
                    }
                }
            }
        }

        // Discogs artists and releases (split compound names using known artist validation)
        if (discogsSnapshot is not null)
        {
            foreach (var release in discogsSnapshot.Releases)
            {
                if (!string.IsNullOrWhiteSpace(release.ArtistName))
                {
                    var discogsUrl = release.DiscogsReleaseId is not null
                        ? $"https://www.discogs.com/release/{release.DiscogsReleaseId}"
                        : null;

                    var individualArtists = ArtistNameSplitter.SplitCollaboration(release.ArtistName, knownArtists);
                    foreach (var artist in individualArtists)
                    {
                        AddArtist(artistRefs, artist, BeatTrackSource.Discogs, sourceUrl: discogsUrl);
                    }

                    AddRelease(releaseRefs, release.Title, release.ArtistName, release.ReleasedYear,
                        BeatTrackSource.Discogs, sourceId: release.DiscogsReleaseId?.ToString(), sourceUrl: discogsUrl);
                }
            }
        }

        var artists = artistRefs
            .Select(static kvp => new BeatTrackCanonicalArtist(kvp.Key, kvp.Value))
            .OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var releases = releaseRefs
            .Select(static kvp => new BeatTrackCanonicalRelease(kvp.Value.Title, kvp.Value.ArtistCanonical, kvp.Value.Year, kvp.Value.Sources))
            .OrderBy(static r => r.ArtistCanonicalName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BeatTrackUnifiedProfile(DateTimeOffset.UtcNow, artists, releases);
    }

    private static void AddArtist(
        Dictionary<string, List<BeatTrackSourceReference>> artistRefs,
        string name, BeatTrackSource source,
        string? sourceId = null, string? sourceUrl = null)
    {
        var canonical = BeatTrackAnalysis.CanonicalizeArtistName(name);

        if (!artistRefs.TryGetValue(canonical, out var refs))
        {
            refs = [];
            artistRefs[canonical] = refs;
        }

        // Only add one reference per source per original name
        if (!refs.Any(r => r.Source == source && string.Equals(r.OriginalName, name, StringComparison.OrdinalIgnoreCase)))
        {
            refs.Add(new BeatTrackSourceReference(source, sourceId, sourceUrl, name));
        }
    }

    private static void AddRelease(
        Dictionary<string, (string Title, string? ArtistCanonical, int? Year, List<BeatTrackSourceReference> Sources)> releaseRefs,
        string title, string? artistName, int? year,
        BeatTrackSource source, string? sourceId = null, string? sourceUrl = null)
    {
        var artistCanonical = artistName is not null
            ? BeatTrackAnalysis.CanonicalizeArtistName(artistName)
            : null;

        var key = $"{artistCanonical}||{title.Trim().ToLowerInvariant()}";

        if (!releaseRefs.TryGetValue(key, out var entry))
        {
            entry = (title.Trim(), artistCanonical, year, []);
            releaseRefs[key] = entry;
        }

        entry.Sources.Add(new BeatTrackSourceReference(source, sourceId, sourceUrl, title));
    }
}
