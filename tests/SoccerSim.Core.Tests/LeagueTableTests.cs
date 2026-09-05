using SoccerSim.Core.Competitions;
using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Tests;

/// <summary>
/// The table is derived, so every question about it is a question about the results behind it.
/// Tie-breaking is checked separately from arithmetic because a table that adds up correctly
/// and orders wrongly still shows the wrong champion.
/// </summary>
public sealed class LeagueTableTests
{
    private const int League = 7;
    private const int OtherLeague = 8;

    private static int _ordinal;

    private static AppliedSimulationRun Result(
        int competitionId, int home, int away, int homeScore, int awayScore) =>
        new(++_ordinal, competitionId, _ordinal, home, away, homeScore, awayScore, 1UL, 3, 100, 0UL);

    private static IReadOnlyList<LeagueRow> Table(
        IReadOnlyList<int> clubs, params AppliedSimulationRun[] results) =>
        LeagueTable.Build(clubs, results, League);

    [Fact]
    public void An_unplayed_league_lists_every_club_on_nothing()
    {
        var table = Table([3, 1, 2]);

        Assert.Equal(3, table.Count);
        Assert.Equal(new[] { 1, 2, 3 }, table.Select(row => row.ClubId));
        Assert.All(table, row => Assert.Equal(0, row.Played));
        Assert.All(table, row => Assert.Equal(0, row.Points));
        Assert.Equal(new[] { 1, 2, 3 }, table.Select(row => row.Position));
    }

    [Fact]
    public void A_win_is_three_points_a_draw_is_one_and_a_loss_is_none()
    {
        var table = Table(
            [1, 2, 3],
            Result(League, 1, 2, 2, 0),
            Result(League, 2, 3, 1, 1));

        var one = table.Single(row => row.ClubId == 1);
        var two = table.Single(row => row.ClubId == 2);
        var three = table.Single(row => row.ClubId == 3);

        Assert.Equal((1, 1, 0, 0, 3), (one.Played, one.Won, one.Drawn, one.Lost, one.Points));
        Assert.Equal((2, 0, 1, 1, 1), (two.Played, two.Won, two.Drawn, two.Lost, two.Points));
        Assert.Equal((1, 0, 1, 0, 1), (three.Played, three.Won, three.Drawn, three.Lost, three.Points));
    }

    [Fact]
    public void Goals_are_counted_from_each_clubs_own_side_of_the_result()
    {
        var table = Table(
            [1, 2],
            Result(League, 1, 2, 3, 1),
            Result(League, 2, 1, 4, 0));

        var one = table.Single(row => row.ClubId == 1);
        var two = table.Single(row => row.ClubId == 2);

        Assert.Equal((3, 5, -2), (one.GoalsFor, one.GoalsAgainst, one.GoalDifference));
        Assert.Equal((5, 3, 2), (two.GoalsFor, two.GoalsAgainst, two.GoalDifference));
        Assert.Equal(2, table[0].ClubId);
    }

    [Fact]
    public void Points_beat_goal_difference()
    {
        // Club 2 wins by a cricket score once; club 1 wins twice narrowly.
        var table = Table(
            [1, 2, 3],
            Result(League, 1, 3, 1, 0),
            Result(League, 3, 1, 0, 1),
            Result(League, 2, 3, 9, 0));

        Assert.Equal(1, table[0].ClubId);
        Assert.Equal(6, table[0].Points);
        Assert.Equal(2, table[1].ClubId);
    }

    [Fact]
    public void Goal_difference_breaks_a_tie_on_points()
    {
        var table = Table(
            [1, 2, 3],
            Result(League, 1, 3, 4, 0),
            Result(League, 2, 3, 1, 0));

        Assert.Equal(new[] { 1, 2, 3 }, table.Select(row => row.ClubId));
        Assert.Equal(4, table[0].GoalDifference);
        Assert.Equal(1, table[1].GoalDifference);
    }

    [Fact]
    public void Goals_scored_break_a_tie_on_goal_difference()
    {
        var table = Table(
            [1, 2, 3, 4],
            Result(League, 1, 3, 1, 0),
            Result(League, 2, 4, 3, 2));

        // Both won by one; club 2 scored more doing it.
        Assert.Equal(2, table[0].ClubId);
        Assert.Equal(1, table[1].ClubId);
    }

    [Fact]
    public void Two_identical_records_still_order_the_same_way_every_time()
    {
        // Without a total order the table would depend on enumeration order, which is exactly
        // the incidental ordering the determinism contract forbids.
        var results = new[]
        {
            Result(League, 1, 3, 2, 0),
            Result(League, 2, 4, 2, 0)
        };

        var forwards = LeagueTable.Build([1, 2, 3, 4], results, League);
        var backwards = LeagueTable.Build([4, 3, 2, 1], results.Reverse().ToArray(), League);

        Assert.Equal(forwards, backwards);
        Assert.Equal(new[] { 1, 2, 3, 4 }, forwards.Select(row => row.ClubId));
    }

    [Fact]
    public void Results_from_another_competition_do_not_reach_this_table()
    {
        var table = Table(
            [1, 2],
            Result(OtherLeague, 1, 2, 5, 0),
            Result(0, 1, 2, 5, 0));

        Assert.All(table, row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void A_club_that_is_not_an_entrant_is_left_out_even_if_it_played()
    {
        var table = Table([1, 2], Result(League, 1, 99, 1, 0));

        Assert.Equal(new[] { 1, 2 }, table.Select(row => row.ClubId));
        Assert.Equal(1, table.Single(row => row.ClubId == 1).Played);
    }

    [Fact]
    public void Positions_are_dense_and_start_at_one()
    {
        var table = Table([5, 4, 3, 2, 1], Result(League, 3, 4, 1, 0));

        Assert.Equal(Enumerable.Range(1, 5), table.Select(row => row.Position));
    }
}
