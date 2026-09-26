using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

/// <summary>
/// MATCH-01 makes a match watchable. These tests guard the one thing that has to stay true
/// for a renderer to exist at all: watching a match must not change it.
/// </summary>
public sealed class MatchPlaybackTests
{
    private const ulong Seed = 8675309UL;

    private static MatchContext Context() => new(
        TestWorld.HomeClubId,
        TestWorld.AwayClubId,
        Seed,
        SimulationSettings.SimulationVersion,
        TestWorld.Build().Snapshot());

    [Fact]
    public void Stepping_a_match_reproduces_the_headless_run_exactly()
    {
        // Run is written in terms of Advance, so this is closer to a structural guarantee than
        // a coincidence — but it is the guarantee MATCH-01 rests on, so it gets a test.
        var expected = MatchSimulation.Run(Context());

        var stepped = MatchSimulation.Begin(Context());
        while (stepped.Advance())
        {
        }

        Assert.Equal(expected, stepped.Result);
    }

    [Fact]
    public void Reading_frames_while_stepping_changes_nothing()
    {
        // The renderer's actual usage pattern. If a frame were a window onto live state rather
        // than a copy, this is where it would show up as a different digest.
        var expected = MatchSimulation.Run(Context());

        var watched = MatchSimulation.Begin(Context());
        while (watched.Advance())
        {
            _ = watched.CurrentFrame();
        }

        Assert.Equal(expected.Digest, watched.Result.Digest);
        Assert.Equal(expected.FinalRandomState, watched.Result.FinalRandomState);
    }

    [Fact]
    public void A_frame_carries_twenty_two_players_and_a_ball_on_the_pitch()
    {
        var match = MatchSimulation.Begin(Context());
        match.Advance();

        var frame = match.CurrentFrame();

        Assert.Equal(22, frame.Players.Count);
        Assert.True(Pitch.IsInsidePlay(frame.Ball), $"Ball left the pitch at {frame.Ball}.");

        foreach (var player in frame.Players)
        {
            Assert.True(
                Pitch.IsInsidePlay(player.Location),
                $"Player {player.PlayerId} is off the pitch at {player.Location}.");
        }

        Assert.Equal(11, frame.Players.Count(player => player.ClubId == TestWorld.HomeClubId));
        Assert.Equal(11, frame.Players.Count(player => player.ClubId == TestWorld.AwayClubId));
    }

    [Fact]
    public void Frames_show_the_match_moving()
    {
        // A renderer drawing a frame that never changes would look identical to a broken one,
        // so this asserts there is motion to draw at all.
        var match = MatchSimulation.Begin(Context());
        match.Advance();
        var first = match.CurrentFrame();

        for (var i = 0; i < 200; i++)
        {
            match.Advance();
        }
        var later = match.CurrentFrame();

        Assert.True(later.Tick > first.Tick);
        Assert.NotEqual(first.Ball, later.Ball);
        Assert.NotEqual(
            first.Players.Select(player => player.Location),
            later.Players.Select(player => player.Location));
    }

    [Fact]
    public void A_match_reports_when_it_is_over_and_refuses_a_result_before_then()
    {
        var match = MatchSimulation.Begin(Context());

        Assert.False(match.IsFinished);
        Assert.Throws<InvalidOperationException>(() => { _ = match.Result; });

        while (match.Advance())
        {
        }

        Assert.True(match.IsFinished);
        Assert.False(match.Advance());
        Assert.Equal(match.CurrentFrame().TotalTicks, match.Result.Ticks);
    }
}
