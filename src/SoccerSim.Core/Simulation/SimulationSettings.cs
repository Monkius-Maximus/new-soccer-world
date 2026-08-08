namespace SoccerSim.Core.Simulation;

public static class SimulationSettings
{
    /// <summary>
    /// Foundation value. MATCH may revise this before gameplay semantics depend on it.
    /// Once a released simulation depends on it, a change requires a SimulationVersion bump.
    /// </summary>
    public const int FixedTimeStepMilliseconds = 100;

    public const int SimulationVersion = 1;
}
