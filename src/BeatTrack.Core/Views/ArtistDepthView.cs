using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), DescriptionProperty = nameof(Summary), FieldLayout = FieldLayout.Inline)]
public class ArtistDepthView
{
    [MarkoutIgnore] public string Title { get; set; } = "";
    [MarkoutIgnore] public string? Summary { get; set; }

    public string Window { get; set; } = "";
    public string Mode { get; set; } = "";

    [MarkoutSection(Name = "Artists")]
    [MarkoutMaxItems(50)]
    public List<ArtistDepthRow>? Artists { get; set; }

    [MarkoutSection(Name = "Recommendations")]
    [MarkoutMaxItems(20)]
    public List<DepthRecommendationRow>? Recommendations { get; set; }

    [MarkoutSection(Name = "True Love Artists")]
    [MarkoutMaxItems(20)]
    public List<TrueLoveRow>? TrueLoveArtists { get; set; }

    [MarkoutSection(Name = "Fan Tiers: Staple")]
    [MarkoutMaxItems(20)]
    public List<FanTierRow>? Staples { get; set; }

    [MarkoutSection(Name = "Fan Tiers: Regular")]
    [MarkoutMaxItems(20)]
    public List<FanTierRow>? Regulars { get; set; }

    [MarkoutSection(Name = "Fan Tiers: Rotation")]
    [MarkoutMaxItems(20)]
    public List<FanTierRow>? Rotation { get; set; }
}

[MarkoutSerializable]
public record ArtistDepthRow
{
    public string Artist { get; init; } = "";

    [MarkoutDisplayFormat("{0:N0}")]
    public int Plays { get; init; }

    public int Tracks { get; init; }
    public int Albums { get; init; }
    public int Months { get; init; }

    [MarkoutPropertyName("Top %")]
    [MarkoutDisplayFormat("{0:P0}")]
    public double TopTrackShare { get; init; }

    [MarkoutPropertyName("Top Track")]
    [MarkoutSkipNull]
    public string? TopTrack { get; init; }
}

[MarkoutSerializable]
public record DepthRecommendationRow
{
    public string Artist { get; init; } = "";
    public string Recommendation { get; init; } = "";
}

[MarkoutSerializable]
public record TrueLoveRow
{
    public string Artist { get; init; } = "";
    public int Tracks { get; init; }
    public int Albums { get; init; }
    public int Months { get; init; }

    [MarkoutPropertyName("Top %")]
    [MarkoutDisplayFormat("{0:P0}")]
    public double TopTrackShare { get; init; }
}

[MarkoutSerializable]
public record FanTierRow
{
    public string Artist { get; init; } = "";

    [MarkoutPropertyName("Quarters")]
    public string QuarterLabel { get; init; } = "";

    [MarkoutDisplayFormat("{0:P0}")]
    public double Coverage { get; init; }

    [MarkoutDisplayFormat("{0:N0}")]
    public int Plays { get; init; }
}
