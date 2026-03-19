using Markout;

namespace BeatTrack.Core.Views;

[MarkoutSerializable(TitleProperty = nameof(Title))]
public class StatsView
{
    [MarkoutIgnore] public string Title { get; set; } = "Listening Stats";

    [MarkoutPropertyName("First Scrobble")]
    public string FirstScrobble { get; set; } = "";

    [MarkoutPropertyName("Last Scrobble")]
    public string LastScrobble { get; set; } = "";

    [MarkoutPropertyName("Total Scrobbles")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int TotalScrobbles { get; set; }

    [MarkoutPropertyName("Unique Artists")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int UniqueArtists { get; set; }

    [MarkoutPropertyName("Unique Albums")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int UniqueAlbums { get; set; }

    [MarkoutPropertyName("Unique Tracks")]
    [MarkoutDisplayFormat("{0:N0}")]
    public int UniqueTracks { get; set; }

    [MarkoutPropertyName("One-Hit Wonders")]
    public string OneHitWonders { get; set; } = "";

    [MarkoutPropertyName("Listening Span")]
    public string ListeningSpan { get; set; } = "";

    [MarkoutPropertyName("Active Days")]
    public string ActiveDays { get; set; } = "";

    [MarkoutPropertyName("Avg per Active Day")]
    [MarkoutDisplayFormat("{0:F1}")]
    public double AvgPerActiveDay { get; set; }

    [MarkoutPropertyName("Most Popular Month")]
    public string MostPopularMonth { get; set; } = "";

    [MarkoutPropertyName("Most Popular Year")]
    public string MostPopularYear { get; set; } = "";

    [MarkoutPropertyName("Busiest Day of Week")]
    public string BusiestDayOfWeek { get; set; } = "";

    [MarkoutPropertyName("Eddington Number")]
    public int EddingtonNumber { get; set; }

    [MarkoutPropertyName("Days to Next Eddington")]
    public int DaysToNextEddington { get; set; }
}
