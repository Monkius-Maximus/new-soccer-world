namespace SoccerSim.Core.Match;

/// <summary>One player as a renderer sees them: identity, side and where they are.</summary>
public readonly record struct PlayerFrame(int PlayerId, int ClubId, int Slot, Vec2 Location);

/// <summary>
/// The state of a match at one tick, copied out for something to draw.
/// <para>
/// This is a snapshot, not a view. A renderer cannot reach back into the simulation through it,
/// which is what lets the same match be watched and stay bit-identical to the headless run of
/// the same seed.
/// </para>
/// <para>
/// Positions are in metres on the <see cref="Pitch"/>: x runs 0..105, y runs 0..68. Converting
/// those to screen space is the renderer's business and no concern of the simulation's.
/// </para>
/// </summary>
public sealed record MatchFrame(
    int Tick,
    int TotalTicks,
    int Minute,
    int HomeClubId,
    int AwayClubId,
    int HomeScore,
    int AwayScore,
    Vec2 Ball,
    IReadOnlyList<PlayerFrame> Players);
