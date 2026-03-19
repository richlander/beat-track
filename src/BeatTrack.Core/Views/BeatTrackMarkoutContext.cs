using Markout;

namespace BeatTrack.Core.Views;

// Momentum
[MarkoutContext(typeof(MomentumView))]
[MarkoutContext(typeof(HeatingUpRow))]
[MarkoutContext(typeof(OnRepeatRow))]
[MarkoutContext(typeof(NewToYouRow))]
[MarkoutContext(typeof(ComebackRow))]
[MarkoutContext(typeof(SteadyRotationRow))]
[MarkoutContext(typeof(CoolingOffRow))]
// TopArtists
[MarkoutContext(typeof(TopArtistsView))]
[MarkoutContext(typeof(TopArtistRow))]
// Stats
[MarkoutContext(typeof(StatsView))]
// NewDiscoveries
[MarkoutContext(typeof(NewDiscoveriesView))]
[MarkoutContext(typeof(NewDiscoveryRow))]
[MarkoutContext(typeof(FirstClickRow))]
[MarkoutContext(typeof(RediscoveryRow))]
[MarkoutContext(typeof(LongtimeFanRow))]
// ArtistDepth
[MarkoutContext(typeof(ArtistDepthView))]
[MarkoutContext(typeof(ArtistDepthRow))]
[MarkoutContext(typeof(DepthRecommendationRow))]
[MarkoutContext(typeof(TrueLoveRow))]
[MarkoutContext(typeof(FanTierRow))]
// Streaks
[MarkoutContext(typeof(StreaksView))]
[MarkoutContext(typeof(StreakRow))]
[MarkoutContext(typeof(ArtistStreakRow))]
// ArtistVelocity
[MarkoutContext(typeof(ArtistVelocityView))]
[MarkoutContext(typeof(VelocityRow))]
// Duration
[MarkoutContext(typeof(DurationView))]
[MarkoutContext(typeof(WindowPercentageRow))]
// Status
[MarkoutContext(typeof(StatusView))]
[MarkoutContext(typeof(StatusItemRow))]
[MarkoutContext(typeof(SuggestionRow))]
// ClusterExploration
[MarkoutContext(typeof(ClusterExplorationView))]
[MarkoutContext(typeof(ClusterRow))]
[MarkoutContext(typeof(UnexploredRow))]
[MarkoutContext(typeof(ExploredRow))]
public partial class BeatTrackMarkoutContext : MarkoutSerializerContext { }
