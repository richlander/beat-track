using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), FieldLayout = FieldLayout.Inline)]
public class DurationView
{
    [MarkoutIgnore] public string Title { get; set; } = "Listening Duration";

    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Span { get; set; } = "";

    [MarkoutPropertyName("Active Days")]
    public string ActiveDays { get; set; } = "";

    [MarkoutSection(Name = "Windows as % of History")]
    public List<WindowPercentageRow>? Windows { get; set; }
}

[MarkoutSerializable]
public record WindowPercentageRow
{
    public string Window { get; init; } = "";

    [MarkoutPropertyName("% of History")]
    public string Percentage { get; init; } = "";
}
