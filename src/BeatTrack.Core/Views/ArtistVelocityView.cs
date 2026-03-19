using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), DescriptionProperty = nameof(Summary), FieldLayout = FieldLayout.Inline)]
public class ArtistVelocityView
{
    [MarkoutIgnore] public string Title { get; set; } = "";
    [MarkoutIgnore] public string? Summary { get; set; }

    public string Bucket { get; set; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Artists { get; set; }

    [MarkoutSection(Name = "Cumulative Plays")]
    [MarkoutMaxItems(500)]
    public List<VelocityRow>? Rows { get; set; }
}

[MarkoutSerializable]
public record VelocityRow
{
    public string Period { get; init; } = "";
    public string Artist { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Cumulative { get; init; }

    [MarkoutPropertyName("Period Plays")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int PeriodPlays { get; init; }
}
