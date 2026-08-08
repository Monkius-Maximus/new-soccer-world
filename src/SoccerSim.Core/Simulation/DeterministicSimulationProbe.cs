namespace SoccerSim.Core.Simulation;

/// <summary>
/// Foundation-only deterministic loop. It intentionally contains no football logic.
/// Its job is to prove seeded, fixed-step, replayable execution before MATCH is implemented.
/// </summary>
public static class DeterministicSimulationProbe
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static SimulationProbeResult Run(MatchContext context, int ticks)
    {
        if (ticks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }

        IRandomSource random = new Pcg32Random(context.Seed);
        var digest = FnvOffsetBasis;

        for (var tick = 0; tick < ticks; tick++)
        {
            var value = random.NextUInt32();
            digest = unchecked((digest ^ value) * FnvPrime);
            digest = unchecked((digest ^ (uint)tick) * FnvPrime);
        }

        return new SimulationProbeResult(
            context.Seed,
            ticks,
            context.SimulationVersion,
            digest,
            random.State);
    }
}
