using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), FieldLayout = FieldLayout.Inline)]
public class StreaksView
{
    [MarkoutIgnore] public string Title { get; set; } = "Streaks";

    public string Longest { get; set; } = "";
    public string Current { get; set; } = "";

    [MarkoutSection(Name = "Top Streaks")]
    [MarkoutMaxItems(10)]
    public List<StreakRow>? TopStreaks { get; set; }

    [MarkoutSection(Name = "Artist Streaks")]
    [MarkoutMaxItems(20)]
    public List<ArtistStreakRow>? ArtistStreaks { get; set; }
}

[MarkoutSerializable]
public record StreakRow
{
    public int Days { get; init; }
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
}

[MarkoutSerializable]
public record ArtistStreakRow
{
    public string Artist { get; init; } = "";

    [MarkoutPropertyName("Longest")]
    public int Days { get; init; }

    public string Start { get; init; } = "";
    public string End { get; init; } = "";

    [MarkoutPropertyName("Total Plays")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int TotalPlays { get; init; }
}
