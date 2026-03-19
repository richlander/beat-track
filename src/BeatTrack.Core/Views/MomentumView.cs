using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title), DescriptionProperty = nameof(Summary), FieldLayout = FieldLayout.Inline)]
public class MomentumView
{
    [MarkoutIgnore] public string Title { get; set; } = "";
    [MarkoutIgnore] public string? Summary { get; set; }

    [MarkoutDisplayFormat("{0:N0}")]
    public int Scrobbles { get; set; }

    public string Window { get; set; } = "";

    [MarkoutSection(Name = "Heating Up")]
    [MarkoutMaxItems(10)]
    public List<HeatingUpRow>? HeatingUp { get; set; }

    [MarkoutSection(Name = "On Repeat")]
    [MarkoutMaxItems(10)]
    public List<OnRepeatRow>? OnRepeat { get; set; }

    [MarkoutSection(Name = "New to You")]
    [MarkoutMaxItems(10)]
    public List<NewToYouRow>? NewToYou { get; set; }

    [MarkoutSection(Name = "Comeback")]
    [MarkoutMaxItems(10)]
    public List<ComebackRow>? Comeback { get; set; }

    [MarkoutSection(Name = "Cooling Off")]
    [MarkoutMaxItems(10)]
    public List<CoolingOffRow>? CoolingOff { get; set; }
}

[MarkoutSerializable]
public record HeatingUpRow
{
    [MarkoutPropertyName("Artist")]
    public string Name { get; init; } = "";

    [MarkoutPropertyName("This Period")]
    public int RecentPlays { get; init; }

    public string Baseline { get; init; } = "";

    [MarkoutPropertyName("Catalog")]
    [MarkoutSkipNull]
    public string? Depth { get; init; }
}

[MarkoutSerializable]
public record OnRepeatRow
{
    public string Track { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Plays { get; init; } = "";
}

[MarkoutSerializable]
public record NewToYouRow
{
    public string Artist { get; init; } = "";
    public int Plays { get; init; }

    [MarkoutPropertyName("First Listen")]
    public string FirstListen { get; init; } = "";
}

[MarkoutSerializable]
public record ComebackRow
{
    public string Artist { get; init; } = "";

    [MarkoutPropertyName("Recent Plays")]
    public int RecentPlays { get; init; }

    [MarkoutPropertyName("Away")]
    public string Gap { get; init; } = "";

    [MarkoutPropertyName("Prior Plays")]
    public int PriorPlays { get; init; }
}

[MarkoutSerializable]
public record CoolingOffRow
{
    public string Artist { get; init; } = "";

    [MarkoutPropertyName("This Period")]
    public string RecentActivity { get; init; } = "";

    public string Baseline { get; init; } = "";
}
