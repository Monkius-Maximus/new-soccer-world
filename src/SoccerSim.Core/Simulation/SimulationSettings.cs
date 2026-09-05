namespace SoccerSim.Core.Simulation;

public static class SimulationSettings
{
    /// <summary>
    /// The tick length every simulation advances by.
    /// <para>
    /// Foundation carried a provisional 100 ms. MATCH-00 revised it to 50 ms: at 100 ms a
    /// sprinting player covers most of a metre per tick and pressing resolves in visible jumps,
    /// which would also make MATCH-01's visual slice stutter. Ball tunnelling is handled by
    /// segment tests rather than by the tick rate, so this is about motion quality, not
    /// correctness.
    /// </para>
    /// </summary>
    public const int FixedTimeStepMilliseconds = 50;

    /// <summary>
    /// Bumped whenever results for a given seed would change: tick length, tuning constants, or
    /// the rules themselves. A stored result records the version that produced it, so a replay
    /// against a different version is detectable rather than silently wrong.
    /// <list type="bullet">
    ///   <item><description>1 — foundation probe, no football.</description></item>
    ///   <item><description>2 — MATCH-00 spatial simulation at a 50 ms step.</description></item>
///   <item><description>3 — TACT-00 formations, roles and phase-aware shape.</description></item>
    /// </list>
    /// </summary>
    public const int SimulationVersion = 3;
}
