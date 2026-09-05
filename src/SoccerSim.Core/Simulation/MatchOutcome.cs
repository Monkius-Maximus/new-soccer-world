using SoccerSim.Core.Match;

namespace SoccerSim.Core.Simulation;

/// <summary>
/// A finished match, waiting to be applied. It holds no reference to the world it ran against,
/// which is what lets the Application choose when and in which order to apply it.
/// </summary>
/// <param name="CompetitionId">Zero for a match that belongs to no competition.</param>
/// <param name="FixtureOrdinal">Zero when the match settled no scheduled fixture.</param>
public sealed record MatchOutcome(
    int HomeClubId,
    int AwayClubId,
    MatchResult Result,
    int CompetitionId = 0,
    int FixtureOrdinal = 0);
