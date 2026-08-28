using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

/// <summary>
/// MATCH-01 needs to drive the simulation a tick at a time so it can draw each one. These tests
/// pin the property that makes that safe: stepping is the same simulation, not a second one.
/// </summary>
public sealed class SteppedMatchTests
{
    private static MatchContext Context(ulong seed) => new(
        TestWorld.HomeClubId,
        TestWorld.AwayClubId,
        seed,
        SimulationSettings.SimulationVersion,
        TestWorld.Build().Snapshot());

    [Fact]
    public void Stepping_a_match_reproduces_the_batch_result_exactly()
    {
        var batch = MatchSimulation.Run(Context(777UL));

        var stepped = MatchSimulation.Start(Context(777UL));
        while (stepped.Step())
        {
        }

        // Identical digests mean identical positions on every tick, not merely the same score.
        Assert.Equal(batch, stepped.Result);
    }

    [Fact]
    public void A_match_reports_finished_only_after_the_last_tick()
    {
        var match = MatchSimulation.Start(Context(1UL));

        Assert.False(match.IsFinished);
        Assert.Null(match.Result);

        var steps = 0;
        while (match.Step())
        {
            steps++;
        }

        Assert.True(match.IsFinished);
        Assert.NotNull(match.Result);
        Assert.Equal(match.Result!.Ticks, steps);
    }

    [Fact]
    public void Stepping_past_the_end_is_harmless_and_keeps_the_same_result()
    {
        var match = MatchSimulation.Start(Context(2UL));
        while (match.Step())
        {
        }

        var settled = match.Result;

        Assert.False(match.Step());
        Assert.False(match.Step());
        Assert.Equal(settled, match.Result);
    }

    [Fact]
    public void A_snapshot_describes_the_current_tick()
    {
        var match = MatchSimulation.Start(Context(3UL));

        var atKickOff = match.Snapshot();
        Assert.Equal(0, atKickOff.Tick);
        Assert.Equal(22, atKickOff.Players.Count);
        Assert.False(atKickOff.IsFinished);
        Assert.Equal(TestWorld.HomeClubId, atKickOff.HomeClubId);

        // Someone is on the ball at kick-off, and they are a real player of a real club.
        Assert.NotEqual(0, atKickOff.BallCarrierPlayerId);
        Assert.Contains(atKickOff.Players, p => p.PlayerId == atKickOff.BallCarrierPlayerId);

        for (var i = 0; i < 200; i++)
        {
            match.Step();
        }

        Assert.Equal(200, match.Snapshot().Tick);
    }

    [Fact]
    public void Every_player_stays_on_the_pitch_and_keeps_a_valid_condition()
    {
        var match = MatchSimulation.Start(Context(4UL));

        for (var i = 0; i < 4000; i++)
        {
            match.Step();

            if (i % 500 != 0)
            {
                continue;
            }

            var snapshot = match.Snapshot();
            Assert.InRange(snapshot.Ball.X, 0.0, Pitch.Length);
            Assert.InRange(snapshot.Ball.Y, 0.0, Pitch.Width);

            foreach (var player in snapshot.Players)
            {
                Assert.InRange(player.Location.X, 0.0, Pitch.Length);
                Assert.InRange(player.Location.Y, 0.0, Pitch.Width);
                Assert.InRange(player.Stamina, 0.0, 1.0);
            }
        }
    }

    [Fact]
    public void Both_sides_field_eleven_with_exactly_one_goalkeeper()
    {
        var snapshot = MatchSimulation.Start(Context(5UL)).Snapshot();

        foreach (var clubId in new[] { snapshot.HomeClubId, snapshot.AwayClubId })
        {
            var side = snapshot.Players.Where(p => p.ClubId == clubId).ToArray();
            Assert.Equal(11, side.Length);
            Assert.Single(side, p => p.IsGoalkeeper);
            Assert.Equal(Enumerable.Range(0, 11), side.Select(p => p.Slot).Order());
        }
    }

    [Fact]
    public void Each_side_keeps_a_recognisable_shape_facing_the_right_way()
    {
        // Slots are ordered by Formation.Slots: 0 keeper, 1-4 defence, 5-8 midfield, 9-10 attack.
        // Whatever the ball is doing, a team's defenders must sit behind its midfielders, who sit
        // behind its attackers, measured along the direction that team attacks. This is what
        // catches a mirrored formation or a swapped-ends bug, which on screen just looks like
        // players standing in odd places.
        var match = MatchSimulation.Start(Context(11UL));

        for (var step = 0; step < 3000; step++)
        {
            match.Step();

            if (step % 750 != 0)
            {
                continue;
            }

            var snapshot = match.Snapshot();

            // Home attacks increasing x in the first half; away attacks decreasing x.
            AssertShape(snapshot, snapshot.HomeClubId, attackingPositiveX: true);
            AssertShape(snapshot, snapshot.AwayClubId, attackingPositiveX: false);
        }
    }

    private static void AssertShape(MatchSnapshot snapshot, int clubId, bool attackingPositiveX)
    {
        var side = snapshot.Players.Where(p => p.ClubId == clubId).ToArray();

        double MeanX(int fromSlot, int toSlot) => side
            .Where(p => p.Slot >= fromSlot && p.Slot <= toSlot)
            .Average(p => p.Location.X);

        var defence = MeanX(1, 4);
        var midfield = MeanX(5, 8);
        var attack = MeanX(9, 10);

        // Measured along the attacking direction, so one assertion covers both ends.
        var sign = attackingPositiveX ? 1.0 : -1.0;

        Assert.True(sign * defence < sign * midfield,
            $"club {clubId}: defence ({defence:F1}) is not behind midfield ({midfield:F1}).");
        Assert.True(sign * midfield < sign * attack,
            $"club {clubId}: midfield ({midfield:F1}) is not behind attack ({attack:F1}).");
    }

    [Fact]
    public void A_snapshot_is_a_copy_that_does_not_track_later_ticks()
    {
        var match = MatchSimulation.Start(Context(6UL));
        var taken = match.Snapshot();

        for (var i = 0; i < 100; i++)
        {
            match.Step();
        }

        // Holding a snapshot must not silently mutate under a renderer that is still drawing it.
        Assert.Equal(0, taken.Tick);
        Assert.NotEqual(taken.Tick, match.Snapshot().Tick);
    }
}
