using SoccerSim.Core.Match;

namespace SoccerSim.Core.Simulation;

/// <summary>
/// A finished match, waiting to be applied. It holds no reference to the world it ran against,
/// which is what lets the Application choose when and in which order to apply it.
/// </summary>
public sealed record MatchOutcome(int HomeClubId, int AwayClubId, MatchResult Result);
