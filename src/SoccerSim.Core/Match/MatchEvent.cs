namespace SoccerSim.Core.Match;

public enum MatchEventKind
{
    KickOff,
    Pass,
    PassIntercepted,
    Tackle,
    Shot,
    ShotOffTarget,
    ShotBlocked,
    Save,
    Goal,
    OutOfPlay,
    HalfTime,
    FullTime
}

/// <summary>
/// One thing that happened, timestamped by the tick it happened on. The event stream is the
/// match's account of itself: MATCH-01 replays it visually and COMP-00 aggregates it, so it is
/// ordered and complete rather than a debug log.
/// </summary>
/// <param name="Tick">Tick index within the match, at the centralized fixed timestep.</param>
/// <param name="Minute">Derived match minute, for reporting.</param>
/// <param name="ClubId">The club the event belongs to, or 0 when it belongs to neither.</param>
/// <param name="PlayerId">The player involved, or 0 when none.</param>
public sealed record MatchEvent(
    int Tick,
    int Minute,
    MatchEventKind Kind,
    int ClubId,
    int PlayerId)
{
    public override string ToString() => $"{Minute:D2}' {Kind} club={ClubId} player={PlayerId}";
}
