using System.Diagnostics;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

/// <summary>
/// The CI half of ADR-0008. It does not check the criterion — that is measured on the
/// reference machine by <c>tools/SoccerSim.Benchmark</c> and read by a person.
/// <para>
/// This guard exists to catch an order-of-magnitude regression, and nothing finer.
/// Asserting the real ten-second budget on a shared runner would fail from noise, and a
/// test that goes red at random is one the team learns to ignore — worse than no test at
/// all. The ceiling below is ten times the criterion's per-match budget, so ordinary
/// runner variance cannot reach it and a simulation that suddenly costs ten times more
/// cannot hide.
/// </para>
/// </summary>
public sealed class PerformanceGuardTests
{
    /// <summary>ADR-0008 budgets 100 matches in 10 s, so 100 ms per match. This allows 10x.</summary>
    private const double OrderOfMagnitudeCeilingMs = 1000;

    private const int MeasuredMatches = 3;

    [Fact]
    public void A_match_stays_within_an_order_of_magnitude_of_the_round_advance_budget()
    {
        var world = TestWorld.Build();

        // Discarded: the first match pays for JIT compilation of the whole simulation loop.
        _ = MatchSimulation.Run(new MatchContext(
            TestWorld.HomeClubId,
            TestWorld.AwayClubId,
            1UL,
            SimulationSettings.SimulationVersion,
            world.Snapshot()));

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < MeasuredMatches; i++)
        {
            _ = MatchSimulation.Run(new MatchContext(
                TestWorld.HomeClubId,
                TestWorld.AwayClubId,
                (ulong)(1000 + i),
                SimulationSettings.SimulationVersion,
                world.Snapshot()));
        }
        stopwatch.Stop();

        var msPerMatch = stopwatch.Elapsed.TotalMilliseconds / MeasuredMatches;

        Assert.True(
            msPerMatch < OrderOfMagnitudeCeilingMs,
            $"A match cost {msPerMatch:F1} ms, over the {OrderOfMagnitudeCeilingMs} ms order-of-magnitude ceiling " +
            $"(ten times the {OrderOfMagnitudeCeilingMs / 10} ms per-match budget in ADR-0008). " +
            "This is not runner noise at this margin — measure on the reference machine with tools/SoccerSim.Benchmark.");
    }
}
