using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Simulation;

public sealed record MatchContext(
    int HomeClubId,
    int AwayClubId,
    ulong Seed,
    int SimulationVersion,
    WorldState WorldSnapshot);
