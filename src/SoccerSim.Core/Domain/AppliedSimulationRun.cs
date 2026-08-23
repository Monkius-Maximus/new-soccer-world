namespace SoccerSim.Core.Domain;

/// <summary>
/// A simulation result after the Application applied it to the career world.
/// <para>
/// <see cref="Ordinal"/> is the career-local apply order, assigned when the result lands in
/// the world rather than when the match ran. It is what makes the applied sequence replayable:
/// matches may be produced in any order (and, later, in parallel), but they are applied — and
/// persisted, and reloaded — in exactly this one.
/// </para>
/// </summary>
public sealed record AppliedSimulationRun(
    int Ordinal,
    int HomeClubId,
    int AwayClubId,
    ulong Seed,
    int SimulationVersion,
    int Ticks,
    ulong Digest);
