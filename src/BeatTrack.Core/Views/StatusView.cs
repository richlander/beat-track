using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title))]
public class StatusView
{
    [MarkoutIgnore] public string Title { get; set; } = "beat-track status";

    [MarkoutSection(Name = "Config")]
    public List<StatusItemRow>? Config { get; set; }

    [MarkoutSection(Name = "Directories")]
    public List<StatusItemRow>? Directories { get; set; }

    [MarkoutSection(Name = "Data Sources")]
    public List<StatusItemRow>? DataSources { get; set; }

    [MarkoutSection(Name = "Cache")]
    public List<StatusItemRow>? Cache { get; set; }

    [MarkoutSection(Name = "Suggestions")]
    public List<SuggestionRow>? Suggestions { get; set; }
}

[MarkoutSerializable]
public record StatusItemRow
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
}

[MarkoutSerializable]
public record SuggestionRow
{
    public string Action { get; init; } = "";
}
