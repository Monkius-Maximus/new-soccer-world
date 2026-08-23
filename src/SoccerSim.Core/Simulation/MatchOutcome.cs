namespace SoccerSim.Core.Simulation;

/// <summary>
/// What a match hands back. It carries no reference to the world it ran against, which is
/// what lets the Application decide when and in which order to apply it.
/// </summary>
public sealed record MatchOutcome(
    int HomeClubId,
    int AwayClubId,
    SimulationProbeResult Result);
