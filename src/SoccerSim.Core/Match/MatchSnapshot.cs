namespace SoccerSim.Core.Match;

/// <summary>One player's visible state at a single tick. A copy — presentation cannot write back.</summary>
public readonly record struct MatchPlayerSnapshot(
    int PlayerId,
    int ClubId,
    int Slot,
    bool IsGoalkeeper,
    Vec2 Location,
    double Stamina);

/// <summary>
/// Everything a renderer needs for one tick of a running match.
/// <para>
/// This exists so MATCH-01 can draw the simulation rather than re-derive it. Positions are not
/// stored in <see cref="MatchResult"/> because a full match is 108,000 ticks of 22 players;
/// the presentation steps the simulation and reads the current tick instead.
/// </para>
/// </summary>
public sealed record MatchSnapshot(
    int Tick,
    int Minute,
    int HomeClubId,
    int AwayClubId,
    int HomeScore,
    int AwayScore,
    Vec2 Ball,
    int BallCarrierPlayerId,
    IReadOnlyList<MatchPlayerSnapshot> Players,
    bool IsFinished);
