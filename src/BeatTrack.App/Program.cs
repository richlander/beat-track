using System.CommandLine;
using System.Text.Json;
using BeatTrack.App;
using BeatTrack.Core;
using BeatTrack.Core.Queries;
using BeatTrack.Discogs;
using BeatTrack.LastFm;
using BeatTrack.YouTube;
using Shelf.Core;
using Shelf.Core.Items;
using Shelf.Core.Relationships;

// --- Shared shelf stores ---
ShelfPaths.EnsureDirectories();
var shelfItems = new ShelfItems(ShelfPaths.ItemsDir);
var shelfRelationships = new ShelfRelationships(ShelfPaths.RelationshipsDir);

var rootCommand = new RootCommand("beat-track — music listening analysis across Last.fm, YouTube, and Discogs");

// --- Shared options (re-created per command to avoid sharing) ---
static Option<string> WindowOption() => new("--window") { Description = "Time window (7d, 30d, 90d, 365d, all)", DefaultValueFactory = _ => "7d" };
static Option<int> LimitOption(int defaultValue = 50) => new("--limit") { Description = "Max items to display", DefaultValueFactory = _ => defaultValue };

// --- Helper: load scrobbles ---
static (IReadOnlyList<LastFmScrobble>? Scrobbles, string? Path) LoadScrobbles()
{
    var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    var workspaceData = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, "..", "..", "data"));
    var homeData = BeatTrackPaths.DataDir;

    var statsPath = FindByPattern(
        Environment.GetEnvironmentVariable("BEAT_TRACK_LASTFM_STATS_CSV"),
        "lastfmstats-*.csv",
        System.IO.Path.Combine(workspaceData, "lastfmstats"), System.IO.Path.Combine(homeData, "lastfmstats"));

    if (statsPath is null) return (null, null);

    using var csvReader = File.OpenText(statsPath);
    return (LastFmStatsCsvReader.ParseCsv(csvReader), statsPath);
}

static int RunWithScrobbles(Func<IReadOnlyList<LastFmScrobble>, int> action)
{
    var (scrobbles, path) = LoadScrobbles();
    if (scrobbles is null)
    {
        Console.Error.WriteLine("No lastfmstats CSV found. Run 'beat-track history' first or set BEAT_TRACK_LASTFM_STATS_CSV.");
        return 1;
    }

    Console.Error.WriteLine($"loaded: {scrobbles.Count:N0} scrobbles from {System.IO.Path.GetFileName(path)}");
    Console.Error.WriteLine();
    return action(scrobbles);
}

// --- Helper: resolve API client ---
static (LastFmClient? Client, string? UserName, HttpClient? Http) CreateApiClient()
{
    var config = new BeatTrackConfig(BeatTrackPaths.ConfigFile);
    var apiKey = config.LastFmApiKey ?? Environment.GetEnvironmentVariable("LASTFM_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("No API key found. Set it in:");
        Console.Error.WriteLine($"  config:  {BeatTrackPaths.ConfigFile}  (lastfm_api_key=YOUR_KEY)");
        Console.Error.WriteLine("  env:     LASTFM_API_KEY");
        return (null, null, null);
    }

    var userName = config.LastFmUser ?? Environment.GetEnvironmentVariable("LASTFM_USER");
    var sharedSecret = config.LastFmSharedSecret ?? Environment.GetEnvironmentVariable("LASTFM_SHARED_SECRET");
    var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    var client = new LastFmClient(http, new LastFmClientOptions(apiKey, sharedSecret, "beat-track"));
    return (client, userName, http);
}

// ============================================================
// Commands
// ============================================================

// --- momentum ---
{
    var cmd = new Command("momentum", "What's heating up, new, fading, and coming back");
    var w = WindowOption(); var l = LimitOption(5);
    cmd.Options.Add(w); cmd.Options.Add(l);
    cmd.SetAction((pr) => RunWithScrobbles(s => MomentumQuery.Run(s, ["--window", pr.GetValue(w)!, "--limit", pr.GetValue(l).ToString()])));
    rootCommand.Subcommands.Add(cmd);
}

// --- top-artists ---
{
    var cmd = new Command("top-artists", "Top artists by play count in a time window");
    var w = WindowOption(); var l = LimitOption();
    cmd.Options.Add(w); cmd.Options.Add(l);
    cmd.SetAction((pr) => RunWithScrobbles(s => TopArtistsQuery.Run(s, ["--window", pr.GetValue(w)!, "--limit", pr.GetValue(l).ToString()])));
    rootCommand.Subcommands.Add(cmd);
}

// --- new-discoveries ---
{
    var cmd = new Command("new-discoveries", "Engagement gradient: new, first click, rediscovery, longtime fan");
    var w = WindowOption(); var l = LimitOption();
    var min = new Option<int>("--min") { Description = "Minimum plays to include", DefaultValueFactory = _ => 3 };
    var gap = new Option<int>("--gap") { Description = "Dormancy gap in days for rediscovery", DefaultValueFactory = _ => 180 };
    cmd.Options.Add(w); cmd.Options.Add(l); cmd.Options.Add(min); cmd.Options.Add(gap);
    cmd.SetAction((pr) => RunWithScrobbles(s => NewDiscoveriesQuery.Run(s,
        ["--window", pr.GetValue(w)!, "--limit", pr.GetValue(l).ToString(), "--min", pr.GetValue(min).ToString(), "--gap", pr.GetValue(gap).ToString()])));
    rootCommand.Subcommands.Add(cmd);
}

// --- artist-depth ---
{
    var cmd = new Command("artist-depth", "Catalog explorers vs one-hit wonders");
    var w = WindowOption(); var l = LimitOption();
    var mode = new Option<string>("--mode") { Description = "Mode: deep, shallow, or all", DefaultValueFactory = _ => "deep" };
    var min = new Option<int>("--min") { Description = "Minimum plays to include", DefaultValueFactory = _ => 20 };
    cmd.Options.Add(w); cmd.Options.Add(l); cmd.Options.Add(mode); cmd.Options.Add(min);
    cmd.SetAction((pr) => RunWithScrobbles(s => ArtistDepthQuery.Run(s,
        ["--window", pr.GetValue(w)!, "--limit", pr.GetValue(l).ToString(), "--mode", pr.GetValue(mode)!, "--min", pr.GetValue(min).ToString()])));
    rootCommand.Subcommands.Add(cmd);
}

// --- artist-velocity ---
{
    var cmd = new Command("artist-velocity", "Cumulative play curves over time");
    var top = new Option<int>("--top") { Description = "Number of top artists", DefaultValueFactory = _ => 10 };
    var artists = new Option<string?>("--artists") { Description = "Comma-separated artist names" };
    var bucket = new Option<string>("--bucket") { Description = "Bucket: daily, weekly, monthly, quarterly, yearly", DefaultValueFactory = _ => "monthly" };
    cmd.Options.Add(top); cmd.Options.Add(artists); cmd.Options.Add(bucket);
    cmd.SetAction((pr) =>
    {
        var queryArgs = new List<string> { "--top", pr.GetValue(top).ToString(), "--bucket", pr.GetValue(bucket)! };
        var a = pr.GetValue(artists);
        if (a is not null) { queryArgs.Add("--artists"); queryArgs.Add(a); }
        return RunWithScrobbles(s => ArtistVelocityQuery.Run(s, [.. queryArgs]));
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- streaks ---
{
    var cmd = new Command("streaks", "Consecutive-day listening streaks");
    var artist = new Option<string?>("--artist") { Description = "Show streaks for a specific artist" };
    cmd.Options.Add(artist);
    cmd.SetAction((pr) =>
    {
        var a = pr.GetValue(artist);
        var queryArgs = a is not null ? new[] { "--artist", a } : Array.Empty<string>();
        return RunWithScrobbles(s => StreaksQuery.Run(s, queryArgs));
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- stats ---
{
    var cmd = new Command("stats", "Overall listening statistics");
    cmd.SetAction((_) => RunWithScrobbles(StatsQuery.Run));
    rootCommand.Subcommands.Add(cmd);
}

// --- duration ---
{
    var cmd = new Command("duration", "Listening hours by artist");
    cmd.SetAction((_) => RunWithScrobbles(DurationQuery.Run));
    rootCommand.Subcommands.Add(cmd);
}

// --- miss ---
{
    var cmd = new Command("miss", "Track artists you've tried but don't connect with");
    cmd.SetAction((_) =>
    {
        var misses = new KnownMisses(shelfItems, shelfRelationships);
        var all = misses.GetAll();
        if (all.Count == 0) { Console.WriteLine("No known misses. Use 'beat-track miss add \"Artist Name\"' to add one."); return 0; }
        Console.WriteLine($"known_misses ({all.Count}):");
        foreach (var (_, displayName, reason, dateAdded) in all)
        {
            var reasonText = reason is not null ? $" — {reason}" : "";
            Console.WriteLine($"  {displayName} (added {dateAdded}){reasonText}");
        }
        return 0;
    });

    var addCmd = new Command("add", "Add an artist to known misses");
    var artistArg = new Argument<string>("artist") { Description = "Artist name" };
    var reasonOpt = new Option<string?>("--reason") { Description = "Why this artist doesn't work for you" };
    addCmd.Arguments.Add(artistArg); addCmd.Options.Add(reasonOpt);
    addCmd.SetAction((pr) =>
    {
        var misses = new KnownMisses(shelfItems, shelfRelationships);
        misses.Add(pr.GetValue(artistArg)!, pr.GetValue(reasonOpt));
        misses.Save();
        Console.WriteLine($"Added '{pr.GetValue(artistArg)}' to known misses.");
        return 0;
    });
    cmd.Subcommands.Add(addCmd);

    var removeCmd = new Command("remove", "Remove an artist from known misses");
    var removeArg = new Argument<string>("artist") { Description = "Artist name" };
    removeCmd.Arguments.Add(removeArg);
    removeCmd.SetAction((pr) =>
    {
        var misses = new KnownMisses(shelfItems, shelfRelationships);
        var name = pr.GetValue(removeArg)!;
        if (misses.Remove(name)) { misses.Save(); Console.WriteLine($"Removed '{name}' from known misses."); }
        else Console.WriteLine($"'{name}' not found in known misses.");
        return 0;
    });
    cmd.Subcommands.Add(removeCmd);
    rootCommand.Subcommands.Add(cmd);
}

// --- status ---
{
    var cmd = new Command("status", "Show configuration and data availability");
    cmd.SetAction((_) =>
    {
        var pr = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var wd = System.IO.Path.GetFullPath(System.IO.Path.Combine(pr, "..", "..", "data"));
        var hd = BeatTrackPaths.DataDir;
        return StatusQuery.Run(hd, BeatTrackPaths.CacheDir, BeatTrackPaths.ConfigFile, [wd, hd]);
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- live ---
{
    var cmd = new Command("live", "Real-time scrobble feed (requires API key)");
    var n = new Option<int?>("-n") { Description = "Number of recent tracks to show" };
    var f = new Option<bool>("-f") { Description = "Follow mode (poll for new scrobbles)" };
    var userArg = new Argument<string?>("username") { Description = "Last.fm username (defaults to config)", Arity = ArgumentArity.ZeroOrOne };
    cmd.Options.Add(n); cmd.Options.Add(f); cmd.Arguments.Add(userArg);
    cmd.SetAction(async (pr, ct) =>
    {
        var (client, configUser, http) = CreateApiClient();
        if (client is null) return 1;
        using var _ = http;
        var user = pr.GetValue(userArg) ?? configUser;
        if (string.IsNullOrWhiteSpace(user)) { Console.Error.WriteLine("Pass a username or set lastfm_user in config."); return 1; }
        var liveArgs = new List<string>();
        var count = pr.GetValue(n);
        if (count is not null) { liveArgs.Add("-n"); liveArgs.Add(count.Value.ToString()); }
        if (pr.GetValue(f)) liveArgs.Add("-f");
        liveArgs.Add(user);
        return await LiveCommand.RunAsync(client, user, [.. liveArgs]);
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- snapshot ---
{
    var cmd = new Command("snapshot", "Fetch top artists and loved tracks from Last.fm");
    var output = new Option<string?>("--output") { Description = "Custom output path for snapshot JSON" };
    cmd.Options.Add(output);
    cmd.SetAction(async (pr, ct) =>
    {
        var (client, userName, http) = CreateApiClient();
        if (client is null) return 1;
        using var _ = http;
        if (string.IsNullOrWhiteSpace(userName)) { Console.Error.WriteLine("Set lastfm_user in config or LASTFM_USER env var."); return 1; }
        Console.Error.WriteLine($"Fetching snapshot for {userName}...");
        var snapshot = await SnapshotFetcher.FetchAsync(client, userName);
        var outputPath = pr.GetValue(output) ?? System.IO.Path.Combine(BeatTrackPaths.DataDir, $"{userName}-snapshot.json");
        var dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(snapshot, AppJsonContext.Default.BeatTrackSnapshot));
        Console.WriteLine($"user: {snapshot.Profile.UserName}");
        Console.WriteLine($"play_count: {snapshot.Profile.PlayCount?.ToString() ?? ""}");
        Console.WriteLine($"recent_tracks: {snapshot.RecentTracks.Count}");
        Console.WriteLine($"loved_tracks: {snapshot.LovedTracks.Count}");
        Console.WriteLine($"top_artist_periods: {snapshot.TopArtistsByPeriod.Count}");
        Console.WriteLine($"snapshot_written_to: {outputPath}");
        return 0;
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- pull (formerly history) ---
{
    var cmd = new Command("pull", "Pull latest scrobble history from Last.fm");
    cmd.Aliases.Add("history");
    cmd.SetAction(async (pr, ct) =>
    {
        var (client, userName, http) = CreateApiClient();
        if (client is null) return 1;
        using var _ = http;
        if (string.IsNullOrWhiteSpace(userName)) { Console.Error.WriteLine("Set lastfm_user in config or LASTFM_USER env var."); return 1; }
        var historyTracks = await HistoryFetcher.FetchAllAsync(client, userName);
        var historyDir = System.IO.Path.Combine(BeatTrackPaths.DataDir, "lastfmstats");
        Directory.CreateDirectory(historyDir);
        var historyPath = System.IO.Path.Combine(historyDir, $"lastfmstats-{userName}.csv");
        using (var writer = new StreamWriter(historyPath)) { HistoryFetcher.WriteCsv(writer, historyTracks, userName); }
        Console.WriteLine($"scrobbles: {historyTracks.Count:N0}");
        Console.WriteLine($"written_to: {historyPath}");
        return 0;
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- learn ---
{
    var cmd = new Command("learn", "Populate shelf with artist metadata from MusicBrainz");
    var topOption = new Option<int>("--top") { Description = "Number of top artists to enrich", DefaultValueFactory = _ => 100 };
    cmd.Options.Add(topOption);
    cmd.SetAction(async (pr, ct) =>
    {
        var topN = pr.GetValue(topOption);

        // Load scrobbles
        var (scrobbles, scrobblePath) = LoadScrobbles();
        if (scrobbles is null) return 1;
        Console.Error.WriteLine($"loaded: {scrobbles.Count:N0} scrobbles from {System.IO.Path.GetFileName(scrobblePath)}");

        var topArtists = scrobbles
            .Where(static s => s.TimestampMs > 0)
            .GroupBy(s => BeatTrackAnalysis.CanonicalizeArtistName(s.ArtistName), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static g => g.Count())
            .Take(topN)
            .Select(static g => (Canonical: g.Key, DisplayName: g.First().ArtistName, Plays: g.Count()))
            .ToList();

        Console.Error.WriteLine($"top {topArtists.Count} artists by play count");

        // Load MBID cache
        var cacheDir = BeatTrackPaths.CacheDir;
        Directory.CreateDirectory(cacheDir);
        var mbidCache = new MbidCache(System.IO.Path.Combine(cacheDir, "mbid-cache.md"));

        // Look up missing MBIDs
        var http = new HttpClient();
        http.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
        http.DefaultRequestHeaders.Add("User-Agent", "BeatTrack/0.2 (https://github.com/richlander/beat-track)");
        http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var missing = topArtists.Where(a => mbidCache.GetMbid(a.Canonical) is null).ToList();
        if (missing.Count > 0)
        {
            Console.Error.Write($"looking up {missing.Count} MBIDs... ");
            var mbHttp = new HttpClient();
            mbHttp.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
            mbHttp.DefaultRequestHeaders.Add("User-Agent", "BeatTrack/0.2 (https://github.com/richlander/beat-track)");
            mbHttp.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            using var mbLookup = new MusicBrainzArtistLookup(mbHttp);
            var confirmed = await mbLookup.LookupCandidatesAsync(missing.Select(static a => a.Canonical));
            foreach (var r in confirmed)
            {
                if (r.MusicBrainzId is not null)
                    mbidCache.Set(BeatTrackAnalysis.CanonicalizeArtistName(r.Query), r.MusicBrainzId, r.MatchedName, "musicbrainz");
            }
            mbidCache.Save();
            Console.Error.WriteLine($"{confirmed.Count} found");
        }

        // Enrich from MusicBrainz → shelf
        var enriched = 0;
        var memberOfCount = 0;

        // Build name → shelf ID lookup for member-of matching
        var shelfNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Console.Error.Write("enriching: ");
        foreach (var (canonical, displayName, plays) in topArtists)
        {
            var mbid = mbidCache.GetMbid(canonical);
            if (mbid is null) continue;

            await Task.Delay(1100, ct); // MusicBrainz rate limit

            try
            {
                var url = $"artist/{mbid}?fmt=json&inc=tags+artist-rels";
                var json = await http.GetStringAsync(url, ct);
                var detail = System.Text.Json.JsonSerializer.Deserialize(json, LearnJsonContext.Default.MbLearnArtist);
                if (detail is null) continue;

                // Genre tags
                var genreTags = (detail.Tags ?? [])
                    .Where(static t => t.Count >= 1)
                    .OrderByDescending(static t => t.Count)
                    .Take(5)
                    .Select(static t => t.Name)
                    .ToList();

                var city = detail.BeginArea?.Name;
                var country = detail.Area?.Name ?? detail.Country;
                var type = detail.Type?.ToLowerInvariant() ?? "artist";

                // Keywords: genre + city + country
                var keywords = new List<string>(genreTags);
                if (city is not null) keywords.Add(city.ToLowerInvariant());
                if (country is not null) keywords.Add(country.ToLowerInvariant());

                var item = shelfItems.Put(displayName, type == "person" ? "solo-artist" : "artist", "music",
                    string.Join(", ", keywords), "musicbrainz");
                shelfNameToId.TryAdd(displayName, item.Id);
                shelfNameToId.TryAdd(canonical, item.Id);

                // Member-of relationships
                if (detail.Relations is not null)
                {
                    foreach (var rel in detail.Relations)
                    {
                        if (rel.Type != "member of band" || rel.Artist is null) continue;

                        if (rel.Direction == "backward")
                        {
                            // This person is a member of our band
                            var memberName = rel.Artist.Name;
                            if (shelfNameToId.TryGetValue(memberName, out var memberShelfId) ||
                                shelfNameToId.TryGetValue(BeatTrackAnalysis.CanonicalizeArtistName(memberName), out memberShelfId))
                            {
                                if (string.Equals(memberShelfId, item.Id, StringComparison.OrdinalIgnoreCase)) continue;
                                var existing = shelfRelationships.GetBySubjectAndVerb(memberShelfId, "member-of");
                                if (existing.Any(r => string.Equals(r.TargetId, item.Id, StringComparison.OrdinalIgnoreCase))) continue;
                                var attrs = rel.Attributes is { Count: > 0 } ? string.Join(", ", rel.Attributes) : null;
                                shelfRelationships.Add(memberShelfId, "member-of", item.Id, attrs, "musicbrainz");
                                memberOfCount++;
                            }
                        }
                        else if (rel.Direction == "forward")
                        {
                            // Our artist is a member of this band
                            var bandName = rel.Artist.Name;
                            if (shelfNameToId.TryGetValue(bandName, out var targetBandId) ||
                                shelfNameToId.TryGetValue(BeatTrackAnalysis.CanonicalizeArtistName(bandName), out targetBandId))
                            {
                                if (string.Equals(item.Id, targetBandId, StringComparison.OrdinalIgnoreCase)) continue;
                                var existing = shelfRelationships.GetBySubjectAndVerb(item.Id, "member-of");
                                if (existing.Any(r => string.Equals(r.TargetId, targetBandId, StringComparison.OrdinalIgnoreCase))) continue;
                                var attrs = rel.Attributes is { Count: > 0 } ? string.Join(", ", rel.Attributes) : null;
                                shelfRelationships.Add(item.Id, "member-of", targetBandId, attrs, "musicbrainz");
                                memberOfCount++;
                            }
                        }
                    }
                }

                enriched++;
                Console.Error.Write(".");
            }
            catch (HttpRequestException)
            {
                Console.Error.Write("x");
            }
        }

        shelfItems.Save();
        shelfRelationships.Save();
        http.Dispose();

        Console.Error.WriteLine();
        Console.WriteLine($"enriched: {enriched} artists");
        Console.WriteLine($"member_of: {memberOfCount} relationships");
        Console.WriteLine($"shelf: {shelfItems.Count} items, {shelfRelationships.Count} relationships");
        return 0;
    });
    rootCommand.Subcommands.Add(cmd);
}

// --- analyze ---
{
    var cmd = new Command("analyze", "Full cross-source analysis (Last.fm + YouTube + Discogs)");
    cmd.SetAction(async (pr, ct) => await RunFullAnalysis());
    rootCommand.Subcommands.Add(cmd);
}

// --- skill ---
{
    var cmd = new Command("skill", "Print the agent skill definition");
    cmd.SetAction((_) =>
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("SKILL.md");
        if (stream is null) { Console.Error.WriteLine("SKILL.md not found in assembly resources."); return 1; }
        using var reader = new StreamReader(stream);
        Console.Write(reader.ReadToEnd());
        return 0;
    });
    rootCommand.Subcommands.Add(cmd);
}

// ============================================================
// Run
// ============================================================
return rootCommand.Parse(args).Invoke();

// ============================================================
// Full analysis (moved from bare invocation)
// ============================================================
async Task<int> RunFullAnalysis()
{
    var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    var workspaceData = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "data"));
    var homeData = BeatTrackPaths.DataDir;

    // --- Load Last.fm snapshot ---
    var lastFmPath = FindByPattern(
        Environment.GetEnvironmentVariable("BEAT_TRACK_SNAPSHOT_PATH"),
        "*-snapshot.json",
        Path.Combine(projectRoot, "data"), workspaceData, homeData);

    BeatTrackSnapshot? lastFmSnapshot = null;
    if (lastFmPath is not null)
    {
        await using var stream = File.OpenRead(lastFmPath);
        lastFmSnapshot = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.BeatTrackSnapshot);
        Console.WriteLine($"lastfm: {lastFmSnapshot?.RecentTracks.Count} recent tracks, {lastFmSnapshot?.TopArtistsByPeriod.Values.SelectMany(static x => x).Select(static x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()} unique top artists, {lastFmSnapshot?.LovedTracks.Count} loved tracks");
    }
    else
    {
        Console.WriteLine("lastfm: (no snapshot found)");
    }

    // --- Load Discogs collection ---
    var discogsCsvPath = FindByPattern(
        Environment.GetEnvironmentVariable("BEAT_TRACK_DISCOGS_CSV"),
        "*-collection-*.csv",
        Path.Combine(workspaceData, "collection-csv"), Path.Combine(homeData, "collection-csv"));

    BeatTrackDiscogsSnapshot? discogsSnapshot = null;
    if (discogsCsvPath is not null)
    {
        var discogsReader = new DiscogsCollectionCsvReader();
        using var csvReader = File.OpenText(discogsCsvPath);
        var releases = discogsReader.ParseCsv(csvReader);
        discogsSnapshot = new BeatTrackDiscogsSnapshot(DateTimeOffset.UtcNow, releases);
        Console.WriteLine($"discogs: {releases.Count} releases, {releases.Select(static r => r.ArtistName).Where(static n => n is not null).Distinct(StringComparer.OrdinalIgnoreCase).Count()} unique artists");
    }
    else
    {
        Console.WriteLine("discogs: (no CSV found)");
    }

    // --- Load YouTube data ---
    var takeoutDir = FindDir(
        Environment.GetEnvironmentVariable("BEAT_TRACK_TAKEOUT_DIR"),
        Path.Combine(workspaceData, "takeout"),
        Path.Combine(homeData, "takeout"))
        ?? Path.Combine(workspaceData, "takeout");

    var musicLibraryPath = Path.Combine(takeoutDir, "extracted", "Takeout", "YouTube and YouTube Music", "music (library and uploads)", "music library songs.csv");
    var watchHistoryPath = Path.Combine(takeoutDir, "extracted", "Takeout", "YouTube and YouTube Music", "history", "watch-history.html");

    BeatTrackYouTubeSnapshot? youTubeSnapshot = null;
    if (File.Exists(musicLibraryPath) && File.Exists(watchHistoryPath))
    {
        var knownArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (lastFmSnapshot is not null)
        {
            foreach (var track in lastFmSnapshot.RecentTracks)
                if (!string.IsNullOrWhiteSpace(track.ArtistName)) knownArtists.Add(track.ArtistName);
            foreach (var (_, periodArtists) in lastFmSnapshot.TopArtistsByPeriod)
                foreach (var artist in periodArtists)
                    if (!string.IsNullOrWhiteSpace(artist.Name)) knownArtists.Add(artist.Name);
            foreach (var track in lastFmSnapshot.LovedTracks)
                if (!string.IsNullOrWhiteSpace(track.ArtistName)) knownArtists.Add(track.ArtistName);
        }

        if (discogsSnapshot is not null)
            foreach (var release in discogsSnapshot.Releases)
                if (!string.IsNullOrWhiteSpace(release.ArtistName)) knownArtists.Add(release.ArtistName);

        var ytClient = new YouTubeTakeoutClient();
        using var musicReader = File.OpenText(musicLibraryPath);
        var savedTracks = ytClient.ParseMusicLibrarySongs(musicReader);
        foreach (var artist in savedTracks.SelectMany(static t => t.ArtistNames))
            knownArtists.Add(artist);

        var classifier = new MusicClassifier(knownArtists);
        var watchHtml = await File.ReadAllTextAsync(watchHistoryPath);
        var watchEvents = ytClient.ParseWatchHistoryHtml(watchHtml, classifier);

        youTubeSnapshot = new BeatTrackYouTubeSnapshot(DateTimeOffset.UtcNow, savedTracks, watchEvents);
        Console.WriteLine($"youtube: {savedTracks.Count} saved tracks, {watchEvents.Count} watch events ({watchEvents.Count(static e => e.IsMusicCandidate)} music candidates, {knownArtists.Count} known artists used)");
    }
    else
    {
        Console.WriteLine("youtube: (no takeout data found)");
    }

    Console.WriteLine();

    // --- Build unified profile ---
    var profile = BeatTrackUnifiedProfileBuilder.Build(lastFmSnapshot, youTubeSnapshot, discogsSnapshot);
    Console.WriteLine($"unified_artists: {profile.Artists.Count}");
    Console.WriteLine($"unified_releases: {profile.Releases.Count}");
    Console.WriteLine();

    // --- Cross-source analysis ---
    var multiSource = profile.Artists.Where(static a => a.Sources.Select(static s => s.Source).Distinct().Count() > 1).ToList();
    var allThree = profile.Artists.Where(static a => a.Sources.Select(static s => s.Source).Distinct().Count() == 3).ToList();
    var lastFmOnly = profile.Artists.Where(static a => a.Sources.All(static s => s.Source == BeatTrackSource.LastFm)).ToList();
    var youTubeOnly = profile.Artists.Where(static a => a.Sources.All(static s => s.Source == BeatTrackSource.YouTube)).ToList();
    var discogsOnly = profile.Artists.Where(static a => a.Sources.All(static s => s.Source == BeatTrackSource.Discogs)).ToList();

    Console.WriteLine("artist_source_breakdown:");
    Console.WriteLine($"  all_three_sources: {allThree.Count}");
    Console.WriteLine($"  multi_source: {multiSource.Count}");
    Console.WriteLine($"  lastfm_only: {lastFmOnly.Count}");
    Console.WriteLine($"  youtube_only: {youTubeOnly.Count}");
    Console.WriteLine($"  discogs_only: {discogsOnly.Count}");

    Console.WriteLine();
    Console.WriteLine("artists_in_all_three_sources:");
    foreach (var artist in allThree.OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
    {
        var names = artist.Sources.Select(static s => $"{s.Source}:{s.OriginalName}").Distinct();
        Console.WriteLine($"  {artist.CanonicalName} ({string.Join(", ", names)})");
    }

    var discogsAndLastFm = profile.Artists
        .Where(static a => a.Sources.Any(static s => s.Source == BeatTrackSource.Discogs)
            && a.Sources.Any(static s => s.Source == BeatTrackSource.LastFm))
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"discogs_and_lastfm ({discogsAndLastFm.Count} artists — owned + actively listened to):");
    foreach (var artist in discogsAndLastFm.OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  {artist.CanonicalName}");

    Console.WriteLine();
    Console.WriteLine($"discogs_only ({discogsOnly.Count} artists — owned but not in any listening data):");
    foreach (var artist in discogsOnly.OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  {artist.CanonicalName}");

    var youTubeNotDiscogs = profile.Artists
        .Where(static a => a.Sources.Any(static s => s.Source == BeatTrackSource.YouTube)
            && !a.Sources.Any(static s => s.Source == BeatTrackSource.Discogs))
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"youtube_not_discogs ({youTubeNotDiscogs.Count} artists — listen on YouTube but don't own):");
    foreach (var artist in youTubeNotDiscogs.Take(30).OrderBy(static a => a.CanonicalName, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"  {artist.CanonicalName}");
    if (youTubeNotDiscogs.Count > 30)
        Console.WriteLine($"  ... and {youTubeNotDiscogs.Count - 30} more");

    var multiSourceReleases = profile.Releases.Where(static r => r.Sources.Select(static s => s.Source).Distinct().Count() > 1).ToList();
    Console.WriteLine();
    Console.WriteLine($"cross_source_releases ({multiSourceReleases.Count}):");
    foreach (var release in multiSourceReleases.OrderBy(static r => r.ArtistCanonicalName).ThenBy(static r => r.Title))
    {
        var sources = string.Join(", ", release.Sources.Select(static s => s.Source).Distinct());
        Console.WriteLine($"  {release.ArtistCanonicalName} - {release.Title} [{sources}]");
    }

    // === MBID cache + Gap analysis ===
    var cacheDir = BeatTrackPaths.CacheDir;
    var mbidCachePath = Path.Combine(cacheDir, "mbid-cache.md");
    var mbidCache = new MbidCache(mbidCachePath);
    Console.WriteLine($"mbid_cache: {mbidCache.Count} cached entries from {mbidCachePath}");

    var knownMisses = new KnownMisses(shelfItems, shelfRelationships);
    if (knownMisses.Count > 0)
        Console.WriteLine($"known_misses: {knownMisses.Count} artists excluded from recommendations");

    var userFavorites = new UserFavorites(shelfItems, shelfRelationships);
    if (userFavorites.Count > 0)
        Console.WriteLine($"user_favorites: {userFavorites.Count} artists");

    var userSimilar = new UserSimilarArtists(shelfItems, shelfRelationships);
    if (userSimilar.Count > 0)
        Console.WriteLine($"user_similar_artists: {userSimilar.Count} artists with user-defined similarities");

    if (lastFmSnapshot is not null)
    {
        var added = mbidCache.AddFromLastFmSnapshot(lastFmSnapshot);
        if (added > 0) Console.WriteLine($"mbid_cache: added {added} from Last.fm snapshot");
    }

    var uncachedArtists = profile.Artists
        .Select(static a => a.CanonicalName)
        .Concat(userFavorites.GetCanonicalNames())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(a => mbidCache.GetMbid(a) is null)
        .Take(50)
        .ToList();

    if (uncachedArtists.Count > 0)
    {
        Console.Write($"mbid_lookup: resolving {uncachedArtists.Count} artists via MusicBrainz... ");
        using var mbLookup = new MusicBrainzArtistLookup();
        var confirmed = await mbLookup.LookupCandidatesAsync(uncachedArtists, cancellationToken: CancellationToken.None);
        foreach (var result in confirmed)
        {
            if (result.MusicBrainzId is not null)
                mbidCache.Set(BeatTrackAnalysis.CanonicalizeArtistName(result.Query), result.MusicBrainzId, result.MatchedName, "musicbrainz");
        }
        Console.WriteLine($"{confirmed.Count} confirmed");
    }

    mbidCache.Save();
    Console.WriteLine($"mbid_cache: {mbidCache.Count} total entries saved");

    var seedArtists = mbidCache.GetAll()
        .Select(static e => (Mbid: e.Mbid, Name: e.MatchedName ?? e.CanonicalName))
        .DistinctBy(static s => s.Mbid, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var allSimilar = new Dictionary<string, List<SimilarArtist>>(StringComparer.OrdinalIgnoreCase);
    var seedNameByMbid = seedArtists.ToDictionary(s => s.Mbid, s => s.Name, StringComparer.OrdinalIgnoreCase);
    var knownMbids = new HashSet<string>(mbidCache.GetAll().Select(static e => e.Mbid), StringComparer.OrdinalIgnoreCase);

    if (seedArtists.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("=== Gap analysis: similar artists you might be missing ===");
        Console.WriteLine($"seed_artists: {seedArtists.Count} (with MBIDs)");

        var knownCanonical = new HashSet<string>(profile.Artists.Select(static a => a.CanonicalName), StringComparer.OrdinalIgnoreCase);
        var similarCacheDir = Path.Combine(cacheDir, "similar-artists");
        Directory.CreateDirectory(similarCacheDir);

        using var similarLookup = new ListenBrainzSimilarArtistsLookup();
        var queriedCount = 0;
        var cachedCount = 0;

        Console.Write("  loading similar artists: ");
        foreach (var (mbid, name) in seedArtists)
        {
            var cacheFile = Path.Combine(similarCacheDir, $"{mbid}.md");
            if (File.Exists(cacheFile))
            {
                var cached = LoadSimilarArtistsCache(cacheFile);
                if (cached.Count > 0) { allSimilar[mbid] = cached; cachedCount++; continue; }
            }

            try
            {
                var similar = await similarLookup.GetSimilarArtistsAsync(mbid);
                if (similar.Count > 0) { allSimilar[mbid] = [.. similar]; SaveSimilarArtistsCache(cacheFile, name, similar); }
                queriedCount++;
                Console.Write(".");
            }
            catch (HttpRequestException) { Console.Write("x"); }
        }

        Console.WriteLine($" ({cachedCount} cached, {queriedCount} queried)");

        if (userSimilar.Count > 0)
        {
            var userSimilarCount = 0;
            foreach (var artist in userSimilar.GetAllArtists())
            {
                var mbid = mbidCache.GetMbid(artist);
                if (mbid is null) continue;
                var similar = userSimilar.GetSimilar(artist);
                foreach (var simName in similar)
                {
                    var simMbid = mbidCache.GetMbid(simName);
                    if (simMbid is null) continue;
                    if (!allSimilar.TryGetValue(mbid, out var list)) { list = []; allSimilar[mbid] = list; }
                    if (!list.Any(s => s.ArtistMbid.Equals(simMbid, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new SimilarArtist(simMbid, simName, 500, "user", mbid));
                        userSimilarCount++;
                    }
                }
            }
            if (userSimilarCount > 0) Console.WriteLine($"  merged {userSimilarCount} user-defined similarities into graph");
        }

        var aggregatedMap = new Dictionary<string, (string Name, int TotalScore, List<string> Seeds)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (seedMbid, similarList) in allSimilar)
        {
            var seedName = seedNameByMbid.GetValueOrDefault(seedMbid, seedMbid);
            foreach (var artist in similarList)
            {
                if (!aggregatedMap.TryGetValue(artist.ArtistMbid, out var entry)) { entry = (artist.Name, 0, []); aggregatedMap[artist.ArtistMbid] = entry; }
                entry.Seeds.Add(seedName);
                aggregatedMap[artist.ArtistMbid] = (entry.Name, entry.TotalScore + artist.Score, entry.Seeds);
            }
        }

        var aggregated = aggregatedMap
            .Where(kvp => !knownMbids.Contains(kvp.Key))
            .Select(kvp => new AggregatedSimilarArtist(kvp.Key, kvp.Value.Name, kvp.Value.Seeds.Count, kvp.Value.TotalScore, kvp.Value.Seeds))
            .OrderByDescending(static a => a.SeedCount).ThenByDescending(static a => a.TotalScore)
            .ToList();

        Console.WriteLine($"total_similar: {aggregatedMap.Count} artists found across {allSimilar.Count} seeds");

        var gaps = aggregated
            .Where(a => !knownCanonical.Contains(BeatTrackAnalysis.CanonicalizeArtistName(a.Name)))
            .Where(a => !knownMisses.Contains(a.Name))
            .ToList();

        Console.WriteLine($"gaps: {gaps.Count} artists similar to your favorites but not in your data");
        Console.WriteLine();

        Console.WriteLine("top_gaps (similar to multiple favorites but never listened to):");
        foreach (var gap in gaps.Take(30))
        {
            var seeds = string.Join(", ", gap.SimilarToSeeds.Take(5));
            Console.WriteLine($"  {gap.Name}  (similar to {gap.SeedCount} seeds: {seeds})");
        }
        if (gaps.Count > 30) Console.WriteLine($"  ... and {gaps.Count - 30} more");
    }

    // === Slice comparison: Last.fm vs YouTube ===
    var lastFmStatsPath = FindByPattern(
        Environment.GetEnvironmentVariable("BEAT_TRACK_LASTFM_STATS_CSV"),
        "lastfmstats-*.csv",
        Path.Combine(workspaceData, "lastfmstats"), Path.Combine(homeData, "lastfmstats"));

    if (lastFmStatsPath is not null && youTubeSnapshot is not null)
    {
        Console.WriteLine();
        Console.WriteLine("=== Slice comparison: Last.fm vs YouTube ===");
        Console.WriteLine();

        using var scrobbleReader = File.OpenText(lastFmStatsPath);
        var scrobbles = LastFmStatsCsvReader.ParseCsv(scrobbleReader);
        Console.WriteLine($"lastfm_full_history: {scrobbles.Count} scrobbles");

        var lastFmWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var scrobble in scrobbles)
        {
            var cleaned = ArtistNameMatcher.CleanChannelName(scrobble.ArtistName);
            var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
            lastFmWeights.TryGetValue(canonical, out var current);
            lastFmWeights[canonical] = current + 1;
        }

        lastFmWeights = ArtistNameMatcher.MergeWeights(lastFmWeights);
        var lastFmSlice = new BeatTrackSlice("Last.fm", lastFmWeights);
        var matcher = new ArtistNameMatcher(lastFmWeights.Keys);

        var ytWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var matcherResolutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var watch in youTubeSnapshot.WatchEvents)
        {
            if (!watch.IsMusicCandidate || watch.MusicMatchReason is null) continue;

            string? artistName = null;
            if (watch.MusicMatchReason.StartsWith("KnownArtist:", StringComparison.Ordinal))
                artistName = watch.MusicMatchReason["KnownArtist:".Length..];
            else if (watch.MusicMatchReason is "AutoMusicChannel" && !string.IsNullOrWhiteSpace(watch.ChannelName))
                artistName = watch.ChannelName;
            else if (watch.MusicMatchReason is "MusicPlatform" or "StrongHeuristic" or "MediumHeuristic")
            {
                var titleParts = watch.Title.Split([" - ", " – "], 2, StringSplitOptions.TrimEntries);
                if (titleParts.Length == 2 && titleParts[0].Length >= 2)
                {
                    var candidate = matcher.TryResolve(titleParts[0]);
                    if (candidate is not null) artistName = titleParts[0];
                }
            }

            if (artistName is not null)
            {
                var resolved = matcher.TryResolve(artistName);
                if (resolved is not null)
                {
                    matcherResolutions.TryAdd(artistName, resolved);
                    ytWeights.TryGetValue(resolved, out var current);
                    ytWeights[resolved] = current + 1;
                }
                else
                {
                    var cleaned = ArtistNameMatcher.CleanChannelName(artistName);
                    var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
                    ytWeights.TryGetValue(canonical, out var current);
                    ytWeights[canonical] = current + 1;
                }
            }
        }

        var ytSlice = new BeatTrackSlice("YouTube", ytWeights);

        Console.WriteLine($"lastfm_slice: {lastFmSlice.ArtistWeights.Count} artists");
        Console.WriteLine($"youtube_slice: {ytSlice.ArtistWeights.Count} artists");
        Console.WriteLine($"matcher_resolutions: {matcherResolutions.Count}");
        foreach (var (original, resolved) in matcherResolutions.OrderBy(static kvp => kvp.Value, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  {original} -> {resolved}");
        Console.WriteLine();

        var comparison = BeatTrackSliceComparer.Compare(lastFmSlice, ytSlice);

        Console.WriteLine($"shared ({comparison.Shared.Count} artists):");
        foreach (var artist in comparison.Shared.Take(40))
            Console.WriteLine($"  {artist.CanonicalName}  (lastfm={artist.WeightA:N0}, youtube={artist.WeightB:N0})");
        if (comparison.Shared.Count > 40) Console.WriteLine($"  ... and {comparison.Shared.Count - 40} more");

        Console.WriteLine();
        Console.WriteLine($"lastfm_only ({comparison.OnlyA.Count} artists):");
        foreach (var artist in comparison.OnlyA.Take(30))
            Console.WriteLine($"  {artist.CanonicalName}  ({artist.Weight:N0} scrobbles)");
        if (comparison.OnlyA.Count > 30) Console.WriteLine($"  ... and {comparison.OnlyA.Count - 30} more");

        Console.WriteLine();
        Console.WriteLine($"youtube_only ({comparison.OnlyB.Count} artists):");
        foreach (var artist in comparison.OnlyB.Take(30))
            Console.WriteLine($"  {artist.CanonicalName}  ({artist.Weight:N0} watches)");
        if (comparison.OnlyB.Count > 30) Console.WriteLine($"  ... and {comparison.OnlyB.Count - 30} more");

        // Time-windowed slices
        BeatTrackSlice BuildCombinedSlice(int days, string label)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            var cutoffMs = cutoff.ToUnixTimeMilliseconds();
            var cutoffSec = cutoff.ToUnixTimeSeconds();

            var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var scrobble in scrobbles.Where(s => s.TimestampMs >= cutoffMs))
            {
                var cleaned = ArtistNameMatcher.CleanChannelName(scrobble.ArtistName);
                var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned);
                var resolved = matcher.TryResolve(canonical) ?? canonical;
                weights.TryGetValue(resolved, out var c); weights[resolved] = c + 1;
            }

            foreach (var watch in youTubeSnapshot.WatchEvents)
            {
                if (!watch.IsMusicCandidate || watch.MusicMatchReason is null) continue;
                if (watch.WatchedAtUnixTime is null || watch.WatchedAtUnixTime < cutoffSec) continue;

                string? an = null;
                if (watch.MusicMatchReason.StartsWith("KnownArtist:", StringComparison.Ordinal))
                    an = watch.MusicMatchReason["KnownArtist:".Length..];
                else if (watch.MusicMatchReason is "AutoMusicChannel" && !string.IsNullOrWhiteSpace(watch.ChannelName))
                    an = watch.ChannelName;
                else if (watch.MusicMatchReason is "MusicPlatform" or "StrongHeuristic" or "MediumHeuristic")
                {
                    var tp = watch.Title.Split([" - ", " – "], 2, StringSplitOptions.TrimEntries);
                    if (tp.Length == 2 && tp[0].Length >= 2 && matcher.TryResolve(tp[0]) is not null) an = tp[0];
                }

                if (an is not null)
                {
                    var resolved = matcher.TryResolve(an);
                    if (resolved is not null) { weights.TryGetValue(resolved, out var c); weights[resolved] = c + 1; }
                    else { var cleaned = ArtistNameMatcher.CleanChannelName(an); var canonical = BeatTrackAnalysis.CanonicalizeArtistName(cleaned); weights.TryGetValue(canonical, out var c); weights[canonical] = c + 1; }
                }
            }

            return new BeatTrackSlice(label, weights);
        }

        var slice365 = BuildCombinedSlice(365, "All (365d)");
        var slice60 = BuildCombinedSlice(60, "All (60d)");

        Console.WriteLine();
        Console.WriteLine($"=== Last 365 days: {slice365.ArtistWeights.Count} artists, {slice365.ArtistWeights.Values.Sum():N0} events ===");
        Console.WriteLine($"=== Last 60 days: {slice60.ArtistWeights.Count} artists, {slice60.ArtistWeights.Values.Sum():N0} events ===");

        Console.WriteLine();
        Console.WriteLine("=== New interests (in last 60d but not in prior 305d) ===");
        Console.WriteLine();

        var newInterests = slice60.ArtistWeights
            .Where(kvp => !slice365.ArtistWeights.ContainsKey(kvp.Key) || slice365.ArtistWeights[kvp.Key] == kvp.Value)
            .Where(kvp => { if (!slice365.ArtistWeights.TryGetValue(kvp.Key, out var total365)) return true; return total365 <= kvp.Value; })
            .OrderByDescending(static kvp => kvp.Value).ToList();

        Console.WriteLine($"new_interests: {newInterests.Count} artists");
        foreach (var (name, weight) in newInterests)
            Console.WriteLine($"  {name}  ({weight:N0} plays in last 60d)");

        Console.WriteLine();
        Console.WriteLine("=== Surging (much more active in last 60d vs prior 305d) ===");
        Console.WriteLine();

        var surging = slice60.ArtistWeights
            .Where(kvp => slice365.ArtistWeights.TryGetValue(kvp.Key, out var total365) && total365 > kvp.Value)
            .Select(kvp => { var recent = kvp.Value; var total = slice365.ArtistWeights[kvp.Key]; var prior = total - recent; var recentRate = recent / 60.0; var priorRate = prior / 305.0; var surgeRatio = priorRate > 0 ? recentRate / priorRate : recent; return (Name: kvp.Key, Recent: recent, Prior: prior, Total: total, SurgeRatio: surgeRatio); })
            .Where(static x => x.SurgeRatio > 2.0 && x.Recent >= 3)
            .OrderByDescending(static x => x.SurgeRatio).ToList();

        Console.WriteLine($"surging: {surging.Count} artists");
        foreach (var s in surging.Take(30))
            Console.WriteLine($"  {s.Name}  (60d={s.Recent:N0}, prior={s.Prior:N0}, surge={s.SurgeRatio:N1}x)");

        // Re-engagement
        Console.WriteLine();
        Console.WriteLine("=== Re-engage: forgotten favorites similar to your new interests ===");
        Console.WriteLine();

        var invertedSimilar = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (seedMbid, similarList) in allSimilar)
        {
            var seedName2 = seedNameByMbid.GetValueOrDefault(seedMbid, seedMbid);
            foreach (var sim in similarList)
            {
                if (!invertedSimilar.TryGetValue(sim.ArtistMbid, out var seeds)) { seeds = []; invertedSimilar[sim.ArtistMbid] = seeds; }
                seeds.Add(seedName2);
            }
        }

        var mbidByCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in mbidCache.GetAll()) mbidByCanonical.TryAdd(entry.CanonicalName, entry.Mbid);

        var recentArtists = newInterests.Select(static kvp => (Name: kvp.Key, Weight: kvp.Value, Tag: "new"))
            .Concat(surging.Select(static s => (Name: s.Name, Weight: s.Recent, Tag: "surging")))
            .DistinctBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static x => x.Weight >= 3)
            .OrderByDescending(static x => x.Weight).ToList();

        var dormantFavorites = slice365.ArtistWeights
            .Where(kvp => kvp.Value >= 10)
            .Where(kvp => { slice60.ArtistWeights.TryGetValue(kvp.Key, out var recent); return recent < 2; })
            .ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, weight, tag) in recentArtists)
        {
            if (!mbidByCanonical.TryGetValue(name, out var artistMbid)) continue;
            if (!invertedSimilar.TryGetValue(artistMbid, out var clusterSeeds)) continue;
            var dormantInCluster = clusterSeeds
                .Where(sn => { var sc = BeatTrackAnalysis.CanonicalizeArtistName(sn); return dormantFavorites.ContainsKey(sc) && !knownMisses.Contains(sn); })
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            if (dormantInCluster.Count == 0) continue;
            Console.WriteLine($"  {name} ({tag}, {weight:N0} plays) → revisit: {string.Join(", ", dormantInCluster)}");
        }

        // Strange absences
        Console.WriteLine();
        Console.WriteLine("=== Strange absences (similar to many of your active artists, but never listened to) ===");
        Console.WriteLine();

        var active60dMbids = slice60.ArtistWeights.Keys
            .Select(n => (Name: n, Mbid: mbidByCanonical.GetValueOrDefault(n)))
            .Where(static x => x.Mbid is not null).ToList();

        var absenceCandidates = new Dictionary<string, (string Name, double Score, List<string> ActiveNeighbors)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (activeName, activeMbid) in active60dMbids)
        {
            if (activeMbid is null || !allSimilar.TryGetValue(activeMbid, out var similarList)) continue;
            foreach (var sim in similarList)
            {
                var simCanonical = BeatTrackAnalysis.CanonicalizeArtistName(sim.Name);
                if (slice365.ArtistWeights.ContainsKey(simCanonical)) continue;
                if (knownMbids.Contains(sim.ArtistMbid)) continue;
                if (!absenceCandidates.TryGetValue(sim.ArtistMbid, out var entry)) { entry = (sim.Name, 0, []); absenceCandidates[sim.ArtistMbid] = entry; }
                entry.ActiveNeighbors.Add(activeName);
                absenceCandidates[sim.ArtistMbid] = (entry.Name, entry.Score + sim.Score, entry.ActiveNeighbors);
            }
        }

        var strangeAbsences = absenceCandidates.Values
            .Where(static x => x.ActiveNeighbors.Count >= 3)
            .Where(x => !knownMisses.Contains(x.Name))
            .OrderByDescending(static x => x.ActiveNeighbors.Count).ThenByDescending(static x => x.Score).ToList();

        Console.WriteLine($"strange_absences: {strangeAbsences.Count} artists");
        foreach (var absence in strangeAbsences.Take(30))
        {
            var neighbors = string.Join(", ", absence.ActiveNeighbors.Distinct(StringComparer.OrdinalIgnoreCase).Take(5));
            Console.WriteLine($"  {absence.Name}  (neighbors: {absence.ActiveNeighbors.Distinct(StringComparer.OrdinalIgnoreCase).Count()} active artists — {neighbors})");
        }
        if (strangeAbsences.Count > 30) Console.WriteLine($"  ... and {strangeAbsences.Count - 30} more");

        Console.WriteLine();
        Console.WriteLine("dormant favorites you might revisit:");
        var suggestedDormant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (seedMbid, similarList) in allSimilar)
        {
            var seedName2 = seedNameByMbid.GetValueOrDefault(seedMbid, seedMbid);
            var seedCanonical = BeatTrackAnalysis.CanonicalizeArtistName(seedName2);
            if (!dormantFavorites.ContainsKey(seedCanonical)) continue;
            var overlapCount = similarList.Count(sim => { var sc = BeatTrackAnalysis.CanonicalizeArtistName(sim.Name); return slice60.ArtistWeights.ContainsKey(sc); });
            if (overlapCount >= 3 && !knownMisses.Contains(seedName2) && suggestedDormant.Add(seedCanonical))
            {
                var overlapping = similarList.Where(sim => slice60.ArtistWeights.ContainsKey(BeatTrackAnalysis.CanonicalizeArtistName(sim.Name))).Select(static sim => sim.Name).Take(5);
                Console.WriteLine($"  {seedName2} ({dormantFavorites[seedCanonical]:N0} plays in 365d) — you're currently into: {string.Join(", ", overlapping)}");
            }
        }
    }

    return 0;
}

// --- Helpers ---

static string? FindByPattern(string? envOverride, string pattern, params string[] searchDirs)
{
    if (envOverride is not null && File.Exists(envOverride)) return envOverride;
    foreach (var dir in searchDirs)
    {
        if (!Directory.Exists(dir)) continue;
        var match = Directory.EnumerateFiles(dir, pattern).FirstOrDefault();
        if (match is not null) return match;
    }
    return null;
}

static string? FindDir(string? envOverride, params string[] searchPaths)
{
    if (envOverride is not null && Directory.Exists(envOverride)) return envOverride;
    foreach (var path in searchPaths)
        if (Directory.Exists(path)) return path;
    return null;
}


static List<SimilarArtist> LoadSimilarArtistsCache(string filePath)
{
    var (_, rows) = MarkdownTableStore.Read(filePath);
    var results = new List<SimilarArtist>(rows.Count);
    foreach (var row in rows)
    {
        if (row.Length >= 3)
        {
            int.TryParse(row.Length > 2 ? row[2] : "0", out var score);
            results.Add(new SimilarArtist(row[0], row[1], score, row.Length > 3 ? row[3] : null, null));
        }
    }
    return results;
}

static void SaveSimilarArtistsCache(string filePath, string seedName, IReadOnlyList<SimilarArtist> artists)
{
    string[] headers = ["mbid", "name", "score", "type"];
    var rows = artists.Select(a => new[] { a.ArtistMbid, a.Name, a.Score.ToString(), a.Type ?? "" });
    MarkdownTableStore.Write(filePath, headers, rows);
}
