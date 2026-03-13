using BeatTrack.Discogs;

var csvPath = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("BEAT_TRACK_DISCOGS_CSV")
        ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "collection-csv", "runfaster2000-collection-20260310-2317.csv");

if (!File.Exists(csvPath))
{
    Console.Error.WriteLine($"Discogs CSV not found: {csvPath}");
    return 1;
}

var reader = new DiscogsCollectionCsvReader();
using var fileReader = File.OpenText(csvPath);
var releases = reader.ParseCsv(fileReader);

Console.WriteLine($"total_releases: {releases.Count}");

var uniqueArtists = releases
    .Where(static r => r.ArtistName is not null)
    .Select(static r => r.ArtistName!)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
    .ToList();

Console.WriteLine($"unique_artists: {uniqueArtists.Count}");

// Date range
var withDates = releases.Where(static r => r.DateAddedUnixTime.HasValue).ToList();
if (withDates.Count > 0)
{
    var earliest = DateTimeOffset.FromUnixTimeSeconds(withDates.Min(static r => r.DateAddedUnixTime!.Value));
    var latest = DateTimeOffset.FromUnixTimeSeconds(withDates.Max(static r => r.DateAddedUnixTime!.Value));
    Console.WriteLine($"date_range: {earliest:yyyy-MM-dd} to {latest:yyyy-MM-dd}");
}

// Format breakdown
Console.WriteLine();
Console.WriteLine("format_breakdown:");
var primaryFormats = releases
    .Select(static r => r.Formats.Count > 0 ? GetPrimaryFormat(r.Formats[0]) : "(none)")
    .GroupBy(static x => x, StringComparer.OrdinalIgnoreCase)
    .OrderByDescending(static g => g.Count())
    .ThenBy(static g => g.Key, StringComparer.OrdinalIgnoreCase);

foreach (var group in primaryFormats)
{
    Console.WriteLine($"  {group.Count(),4}  {group.Key}");
}

// Top labels
Console.WriteLine();
Console.WriteLine("top_labels:");
foreach (var group in releases
    .Where(static r => r.Label is not null)
    .GroupBy(static r => r.Label!, StringComparer.OrdinalIgnoreCase)
    .OrderByDescending(static g => g.Count())
    .Take(15))
{
    Console.WriteLine($"  {group.Count(),4}  {group.Key}");
}

// Top artists by release count
Console.WriteLine();
Console.WriteLine("top_artists:");
foreach (var group in releases
    .Where(static r => r.ArtistName is not null)
    .GroupBy(static r => r.ArtistName!, StringComparer.OrdinalIgnoreCase)
    .OrderByDescending(static g => g.Count())
    .Take(20))
{
    Console.WriteLine($"  {group.Count(),4}  {group.Key}");
}

// Year breakdown
Console.WriteLine();
Console.WriteLine("decade_breakdown:");
foreach (var group in releases
    .Where(static r => r.ReleasedYear.HasValue)
    .GroupBy(static r => (r.ReleasedYear!.Value / 10) * 10)
    .OrderBy(static g => g.Key))
{
    Console.WriteLine($"  {group.Count(),4}  {group.Key}s");
}

return 0;

static string GetPrimaryFormat(string format)
{
    // Normalize multi-disc prefixes like "2xLP" -> "LP", "2xCD" -> "CD"
    var f = format.Trim();
    if (f.Length > 2 && char.IsDigit(f[0]) && f[1] == 'x')
    {
        f = f[2..];
    }

    // Normalize common variants
    if (f.StartsWith("12\"", StringComparison.Ordinal) || f.StartsWith("10\"", StringComparison.Ordinal))
    {
        return "LP";
    }

    return f switch
    {
        "HDCD" or "SACD" => "CD",
        _ => f,
    };
}
