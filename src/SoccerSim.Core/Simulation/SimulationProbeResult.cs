namespace SoccerSim.Core.Simulation;

public sealed record SimulationProbeResult(
    ulong Seed,
    int Ticks,
    int SimulationVersion,
    ulong Digest,
    ulong FinalRandomState);
