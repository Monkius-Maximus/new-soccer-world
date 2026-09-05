using SoccerSim.Core.Competitions;

namespace SoccerSim.Core.Tests;

/// <summary>
/// A season is a cursor over a generated calendar. What matters is that the cursor never skips
/// a round, never replays one, and that the calendar it points into is the same one every time.
/// </summary>
public sealed class SeasonTests
{
    private static readonly DateOnly Start = new(2026, 2, 7);

    private static Season Of(int clubCount, int currentMatchday = Season.NotStarted) =>
        new(1, Start, currentMatchday, [.. Enumerable.Range(1, clubCount)]);

    [Fact]
    public void A_new_season_has_not_kicked_off()
    {
        var season = Of(4);

        Assert.Equal(0, season.CurrentMatchday);
        Assert.False(season.IsComplete);
        Assert.Equal(1, season.NextFixtures()[0].Matchday);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 6)]
    [InlineData(20, 38)]
    [InlineData(3, 6)]
    [InlineData(5, 10)]
    public void Total_matchdays_matches_the_calendar_it_describes(int clubCount, int expected)
    {
        var season = Of(clubCount);

        Assert.Equal(expected, season.TotalMatchdays);
        // The cheap answer and the expensive one have to agree, or the season could call itself
        // complete with fixtures left.
        Assert.Equal(season.Fixtures.Max(f => f.Matchday), season.TotalMatchdays);
    }

    [Fact]
    public void Walking_the_season_plays_every_fixture_exactly_once()
    {
        var season = Of(6);
        var played = new List<int>();

        while (!season.IsComplete)
        {
            played.AddRange(season.NextFixtures().Select(f => f.Ordinal));
            season = season with { CurrentMatchday = season.CurrentMatchday + 1 };
        }

        Assert.Equal(season.Fixtures.Count, played.Count);
        Assert.Equal(season.Fixtures.Select(f => f.Ordinal).OrderBy(x => x), played.OrderBy(x => x));
    }

    [Fact]
    public void A_finished_season_offers_no_more_fixtures()
    {
        var season = Of(4, currentMatchday: 6);

        Assert.True(season.IsComplete);
        Assert.Empty(season.NextFixtures());
    }

    [Fact]
    public void Every_matchday_is_a_week_after_the_last()
    {
        var season = Of(4);

        Assert.Equal(Start, season.DateOf(1));
        Assert.Equal(Start.AddDays(7), season.DateOf(2));
        Assert.Equal(Start.AddDays(35), season.DateOf(6));

        foreach (var fixture in season.Fixtures)
        {
            Assert.Equal(season.DateOf(fixture.Matchday), fixture.Date);
        }
    }

    [Fact]
    public void The_calendar_does_not_depend_on_how_far_the_season_has_got()
    {
        Assert.Equal(Of(8, 1).Fixtures, Of(8, 15).Fixtures);
    }

    [Fact]
    public void Seasons_compare_by_value_including_their_entrants()
    {
        // Loaded from a save, the entrant list is a different object with the same contents.
        Assert.Equal(new Season(1, Start, 3, [1, 2, 3]), new Season(1, Start, 3, [1, 2, 3]));
        Assert.NotEqual(new Season(1, Start, 3, [1, 2, 3]), new Season(1, Start, 3, [1, 2, 4]));
        Assert.NotEqual(new Season(1, Start, 3, [1, 2, 3]), new Season(1, Start, 4, [1, 2, 3]));
    }

    [Fact]
    public void A_two_club_league_is_a_home_and_away_pair()
    {
        // The shipped world. Degenerate, but a real double round-robin rather than a special case.
        var season = Of(2);

        Assert.Equal(2, season.TotalMatchdays);
        Assert.Equal(2, season.Fixtures.Count);
        Assert.Equal((1, 2), (season.Fixtures[0].HomeClubId, season.Fixtures[0].AwayClubId));
        Assert.Equal((2, 1), (season.Fixtures[1].HomeClubId, season.Fixtures[1].AwayClubId));
    }
}
