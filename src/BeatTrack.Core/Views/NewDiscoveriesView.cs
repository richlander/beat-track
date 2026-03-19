using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), DescriptionProperty = nameof(Summary), FieldLayout = FieldLayout.Inline)]
public class NewDiscoveriesView
{
    [MarkoutIgnore] public string Title { get; set; } = "";
    [MarkoutIgnore] public string? Summary { get; set; }

    [MarkoutDisplayFormat("{0:N0}")]
    [MarkoutPropertyName("Active Artists")]
    public int ActiveArtists { get; set; }

    public string Window { get; set; } = "";

    [MarkoutSection(Name = "New Discovery")]
    [MarkoutMaxItems(50)]
    public List<NewDiscoveryRow>? NewDiscoveries { get; set; }

    [MarkoutSection(Name = "First Click")]
    [MarkoutMaxItems(50)]
    public List<FirstClickRow>? FirstClicks { get; set; }

    [MarkoutSection(Name = "Rediscovery")]
    [MarkoutMaxItems(50)]
    public List<RediscoveryRow>? Rediscoveries { get; set; }

    [MarkoutSection(Name = "Longtime Fan")]
    [MarkoutMaxItems(50)]
    public List<LongtimeFanRow>? LongtimeFans { get; set; }
}

[MarkoutSerializable]
public record NewDiscoveryRow
{
    public string Artist { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Plays { get; init; }

    public string Discovered { get; init; } = "";
}

[MarkoutSerializable]
public record FirstClickRow
{
    public string Artist { get; init; } = "";

    [MarkoutPropertyName("Recent")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int RecentPlays { get; init; }

    [MarkoutPropertyName("Prior")]
    public int PriorPlays { get; init; }

    [MarkoutPropertyName("First Heard")]
    public string FirstHeard { get; init; } = "";
}

[MarkoutSerializable]
public record RediscoveryRow
{
    public string Artist { get; init; } = "";

    [MarkoutPropertyName("Recent")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int RecentPlays { get; init; }

    [MarkoutPropertyName("Prior")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int PriorPlays { get; init; }

    public string Absent { get; init; } = "";
}

[MarkoutSerializable]
public record LongtimeFanRow
{
    public string Artist { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Recent { get; init; }

    [MarkoutDisplayFormat("{0:N0}")]
    public int Total { get; init; }
}
