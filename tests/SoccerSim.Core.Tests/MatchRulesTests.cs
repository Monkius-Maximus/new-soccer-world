using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

/// <summary>
/// MATCH-00: the football itself. These assert the rules and the shape of the output, plus the
/// statistical sanity that a scoreline generator has to have to be worth anything.
/// </summary>
public sealed class MatchRulesTests
{
    private static MatchResult Play(ulong seed, WorldState? world = null) => MatchSimulation.Run(
        new MatchContext(
            TestWorld.HomeClubId,
            TestWorld.AwayClubId,
            seed,
            SimulationSettings.SimulationVersion,
            (world ?? TestWorld.Build()).Snapshot()));

    [Fact]
    public void A_match_runs_a_full_ninety_minutes_at_the_fixed_timestep()
    {
        var result = Play(1UL);
        var expected = MatchTuning.MatchMinutes * 60 * 1000 / SimulationSettings.FixedTimeStepMilliseconds;

        Assert.Equal(expected, result.Ticks);
        Assert.Equal(SimulationSettings.SimulationVersion, result.SimulationVersion);
    }

    [Fact]
    public void The_event_stream_opens_with_a_kick_off_and_closes_with_full_time()
    {
        var events = Play(2UL).Events;

        Assert.Equal(MatchEventKind.KickOff, events[0].Kind);
        Assert.Equal(MatchEventKind.FullTime, events[^1].Kind);
        Assert.Contains(events, e => e.Kind == MatchEventKind.HalfTime);
    }

    [Fact]
    public void Events_are_ordered_by_tick_and_stay_inside_the_match()
    {
        var result = Play(3UL);

        for (var i = 1; i < result.Events.Count; i++)
        {
            Assert.True(result.Events[i].Tick >= result.Events[i - 1].Tick);
        }

        Assert.All(result.Events, e => Assert.InRange(e.Tick, 0, result.Ticks));
        Assert.All(result.Events, e => Assert.InRange(e.Minute, 0, MatchTuning.MatchMinutes));
    }

    [Fact]
    public void The_score_equals_the_number_of_goal_events()
    {
        var result = Play(4UL);

        Assert.Equal(result.HomeScore, result.Goals.Count(g => g.ClubId == TestWorld.HomeClubId));
        Assert.Equal(result.AwayScore, result.Goals.Count(g => g.ClubId == TestWorld.AwayClubId));
    }

    [Fact]
    public void Every_goal_is_credited_to_a_player_of_the_scoring_club()
    {
        var world = TestWorld.Build();
        var result = Play(5UL, world);

        foreach (var goal in result.Goals)
        {
            var scorer = world.Players.Single(p => p.Id == goal.PlayerId);
            Assert.Equal(goal.ClubId, scorer.ClubId);
        }
    }

    [Fact]
    public void Half_time_falls_at_the_midpoint()
    {
        var result = Play(6UL);
        var halfTime = result.Events.Single(e => e.Kind == MatchEventKind.HalfTime);

        Assert.Equal(result.Ticks / 2, halfTime.Tick);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(77UL)]
    [InlineData(4242UL)]
    public void Scorelines_stay_within_the_bounds_of_a_football_match(ulong seed)
    {
        var result = Play(seed);

        // Not a distribution test — just a guard against a rule change producing cricket scores.
        Assert.InRange(result.HomeScore, 0, 12);
        Assert.InRange(result.AwayScore, 0, 12);
    }

    [Fact]
    public void Across_many_seeds_neither_side_is_structurally_favoured()
    {
        // Both squads are identical here, so any persistent gap is a bug in the simulation,
        // not football. An earlier version handed every fifty-fifty to whichever team came
        // first in the player array and quietly produced a 1.5x home scoring advantage.
        var homeGoals = 0;
        var awayGoals = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Play(seed);
            homeGoals += result.HomeScore;
            awayGoals += result.AwayScore;
        }

        var total = homeGoals + awayGoals;
        Assert.True(total > 0, "Sixty matches produced no goals at all.");

        var gap = Math.Abs(homeGoals - awayGoals) / (double)total;
        Assert.True(gap < 0.15, $"Home {homeGoals} vs away {awayGoals} is a structural imbalance.");
    }

    [Fact]
    public void Sixty_matches_average_a_plausible_number_of_goals()
    {
        var goals = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Play(seed);
            goals += result.HomeScore + result.AwayScore;
        }

        var perMatch = goals / 60.0;
        Assert.InRange(perMatch, 1.5, 5.0);
    }

    [Fact]
    public void A_much_better_side_wins_far_more_often_than_it_loses()
    {
        // The point of attributes: they have to move results, or PLYR-00 bought nothing.
        var strongWins = 0;
        var weakWins = 0;

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var world = TestWorld.Build(homeQuality: 6, awayQuality: -6);
            var result = Play(seed, world);

            if (result.HomeScore > result.AwayScore) strongWins++;
            else if (result.AwayScore > result.HomeScore) weakWins++;
        }

        Assert.True(strongWins > weakWins * 2,
            $"The stronger side won {strongWins} and lost {weakWins}; attributes are not moving results.");
    }

    [Fact]
    public void Shots_saves_and_goals_are_mutually_consistent()
    {
        var result = Play(9UL);

        var shots = result.Events.Count(e => e.Kind == MatchEventKind.Shot);
        var goals = result.HomeScore + result.AwayScore;
        var saves = result.Events.Count(e => e.Kind == MatchEventKind.Save);

        Assert.True(shots >= goals, "There cannot be more goals than shots.");
        Assert.True(shots >= saves, "There cannot be more saves than shots.");
    }

    [Fact]
    public void Every_shot_ends_in_an_outcome_the_event_stream_names()
    {
        // A shot is saved, blocked, off target or a goal. Anything else means the stream lost a
        // shot somewhere, which is how blocked shots went unrecorded for a while: they were
        // neither saved nor off target nor goals, and simply vanished from the account.
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var result = Play(seed);

            int Count(MatchEventKind kind) => result.Events.Count(e => e.Kind == kind);

            var shots = Count(MatchEventKind.Shot);
            var accounted = Count(MatchEventKind.Save)
                            + Count(MatchEventKind.ShotBlocked)
                            + Count(MatchEventKind.ShotOffTarget)
                            + Count(MatchEventKind.Goal);

            // A shot still travelling at full time has no outcome yet, so allow exactly one.
            Assert.InRange(shots - accounted, 0, 1);
        }
    }
}
