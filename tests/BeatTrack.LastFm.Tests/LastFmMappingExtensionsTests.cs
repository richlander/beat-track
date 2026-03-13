namespace BeatTrack.LastFm.Tests;

public sealed class LastFmMappingExtensionsTests
{
    [Fact]
    public void ToBeatTrackUserProfile_maps_user_payload()
    {
        var response = new BeatTrack.LastFm.LastFmUserGetInfoResponse(
            new BeatTrack.LastFm.LastFmUser(
                Name: "runfaster2000",
                RealName: "Rich",
                Url: "https://www.last.fm/user/runfaster2000",
                Registered: new BeatTrack.LastFm.LastFmRegisteredAt("123"),
                PlayCount: "456",
                Image: [new BeatTrack.LastFm.LastFmImage("large", "https://img") ]));

        var profile = response.ToBeatTrackUserProfile();

        Assert.Equal("runfaster2000", profile.UserName);
        Assert.Equal("Rich", profile.RealName);
        Assert.Equal(123L, profile.RegisteredAtUnixTime);
        Assert.Equal(456L, profile.PlayCount);
        Assert.Single(profile.Images);
    }

    [Fact]
    public void ToBeatTrackListeningEvents_maps_recent_tracks_payload()
    {
        var response = new BeatTrack.LastFm.LastFmRecentTracksResponse(
            new BeatTrack.LastFm.LastFmRecentTracks(
                Track:
                [
                    new BeatTrack.LastFm.LastFmTrack(
                        Name: "Dayvan Cowboy",
                        Artist: new BeatTrack.LastFm.LastFmTrackArtist("Boards of Canada", null),
                        Album: new BeatTrack.LastFm.LastFmTrackAlbum("The Campfire Headphase", null),
                        Date: new BeatTrack.LastFm.LastFmTrackDate("1000", null),
                        Url: "https://track",
                        Image: [new BeatTrack.LastFm.LastFmImage("medium", "https://img")],
                        Attributes: new BeatTrack.LastFm.LastFmNowPlayingAttributes("true"))
                ],
                Attributes: new BeatTrack.LastFm.LastFmRecentTracksAttributes("runfaster2000", "2", "50", "9", "420")));

        var result = response.ToBeatTrackListeningEvents();

        Assert.Single(result.Items);
        Assert.Equal("Dayvan Cowboy", result.Items[0].TrackName);
        Assert.Equal("Boards of Canada", result.Items[0].ArtistName);
        Assert.Equal("The Campfire Headphase", result.Items[0].AlbumName);
        Assert.Equal(1000L, result.Items[0].PlayedAtUnixTime);
        Assert.True(result.Items[0].IsNowPlaying);
        Assert.False(result.Items[0].IsLoved);
        Assert.Equal(2, result.Page);
        Assert.Equal(420, result.Total);
    }

    [Fact]
    public void ToBeatTrackArtistSummaries_maps_chart_payload()
    {
        var response = new BeatTrack.LastFm.LastFmTopArtistsResponse(
            new BeatTrack.LastFm.LastFmTopArtists(
                Artist:
                [
                    new BeatTrack.LastFm.LastFmChartArtist(
                        Name: "Boards of Canada",
                        Url: "https://artist",
                        Mbid: "mbid-1",
                        PlayCount: "99",
                        Image: [new BeatTrack.LastFm.LastFmImage("small", "https://img")],
                        Streamable: "0")
                ],
                Attributes: new BeatTrack.LastFm.LastFmChartAttributes("runfaster2000", "1", "50", "1", "1", "7day")));

        var result = response.ToBeatTrackArtistSummaries();

        Assert.Single(result.Items);
        Assert.Equal("Boards of Canada", result.Items[0].Name);
        Assert.Equal(99L, result.Items[0].PlayCount);
        Assert.Equal("7day", result.Items[0].Period);
    }

    [Fact]
    public void ToBeatTrackLovedListeningEvents_marks_tracks_as_loved()
    {
        var response = new BeatTrack.LastFm.LastFmLovedTracksResponse(
            new BeatTrack.LastFm.LastFmLovedTracks(
                Track:
                [
                    new BeatTrack.LastFm.LastFmLovedTrack(
                        Name: "Roygbiv",
                        Url: "https://track",
                        Mbid: null,
                        Artist: new BeatTrack.LastFm.LastFmTrackArtist("Boards of Canada", null),
                        Date: new BeatTrack.LastFm.LastFmTrackDate("2000", null),
                        Image: [],
                        Streamable: new BeatTrack.LastFm.LastFmStreamable("0", "0"))
                ],
                Attributes: new BeatTrack.LastFm.LastFmChartAttributes("runfaster2000", "1", "50", "1", "1", null)));

        var result = response.ToBeatTrackLovedListeningEvents();

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsLoved);
        Assert.False(result.Items[0].IsNowPlaying);
        Assert.Equal("Boards of Canada", result.Items[0].ArtistName);
        Assert.Equal(2000L, result.Items[0].PlayedAtUnixTime);
    }
}
