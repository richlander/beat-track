using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), DescriptionProperty = nameof(Summary), FieldLayout = FieldLayout.Inline)]
public class ClusterExplorationView
{
    [MarkoutIgnore] public string Title { get; set; } = "Cluster Exploration";
    [MarkoutIgnore] public string? Summary { get; set; }

    [MarkoutDisplayFormat("{0:N0}")]
    public int Artists { get; set; }

    [MarkoutPropertyName("Min Score")]
    public int MinScore { get; set; }

    [MarkoutSection(Name = "Clusters")]
    [MarkoutMaxItems(20)]
    public List<ClusterRow>? Clusters { get; set; }

    [MarkoutSection(Name = "Unexplored")]
    [MarkoutMaxItems(50)]
    public List<UnexploredRow>? Unexplored { get; set; }

    [MarkoutSection(Name = "Explored")]
    [MarkoutMaxItems(50)]
    public List<ExploredRow>? Explored { get; set; }
}

[MarkoutSerializable]
public record ClusterRow
{
    public string Artist { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Plays { get; init; }

    [MarkoutPropertyName("Explored")]
    public string ExploredLabel { get; init; } = "";

    [MarkoutPropertyName("Coverage")]
    public string Coverage { get; init; } = "";

    [MarkoutPropertyName("Top Unexplored")]
    [MarkoutSkipNull]
    public string? TopUnexplored { get; init; }
}

[MarkoutSerializable]
public record UnexploredRow
{
    public string Artist { get; init; } = "";
    public int Similarity { get; init; }
}

[MarkoutSerializable]
public record ExploredRow
{
    public string Artist { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Plays { get; init; }

    public int Similarity { get; init; }
}
