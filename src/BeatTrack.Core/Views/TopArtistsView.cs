using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), DescriptionProperty = nameof(Summary), FieldLayout = FieldLayout.Inline)]
public class TopArtistsView
{
    [MarkoutIgnore] public string Title { get; set; } = "";
    [MarkoutIgnore] public string? Summary { get; set; }

    [MarkoutDisplayFormat("{0:N0}")]
    public int Scrobbles { get; set; }

    public string Window { get; set; } = "";

    [MarkoutSection(Name = "Artists")]
    [MarkoutMaxItems(50)]
    public List<TopArtistRow>? Artists { get; set; }
}

[MarkoutSerializable]
public record TopArtistRow
{
    [MarkoutPropertyName("Artist")]
    public string Name { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Plays { get; init; }
}
