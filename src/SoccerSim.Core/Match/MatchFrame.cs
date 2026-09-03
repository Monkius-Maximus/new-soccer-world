namespace SoccerSim.Core.Match;

/// <summary>Controls whether a simulation retains presentation samples.</summary>
public enum MatchCaptureMode
{
    None,
    Playback
}

/// <summary>A compact immutable view of one player at one sampled tick.</summary>
public readonly record struct MatchPlayerSample(
    int PlayerId,
    int ClubId,
    int Slot,
    Vec2 Location,
    double Stamina);

/// <summary>
/// Authoritative spatial state sampled from MATCH-00 for presentation. Frames are read models:
/// renderers may interpolate them but may not feed changes back into the simulation.
/// </summary>
public sealed record MatchFrame(
    int Tick,
    int HomeScore,
    int AwayScore,
    Vec2 BallLocation,
    int? CarrierPlayerId,
    IReadOnlyList<MatchPlayerSample> Players);
