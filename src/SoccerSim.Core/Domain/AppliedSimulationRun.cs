namespace SoccerSim.Core.Domain;

/// <summary>
/// A finished match after the Application applied it to the career world.
/// <para>
/// <see cref="Ordinal"/> is the career-local apply order, assigned when the result lands in the
/// world rather than when the match ran. It is what makes the applied sequence replayable:
/// matches may be produced in any order (and, later, in parallel), but they are applied — and
/// persisted, and reloaded — in exactly this one.
/// </para>
/// </summary>
public sealed record AppliedSimulationRun(
    int Ordinal,
    int CompetitionId,
    int FixtureOrdinal,
    int HomeClubId,
    int AwayClubId,
    int HomeScore,
    int AwayScore,
    ulong Seed,
    int SimulationVersion,
    int Ticks,
    ulong Digest)
{
    /// <summary>Zero when the match belonged to no competition, such as a one-off friendly.</summary>
    public bool IsCompetitive => CompetitionId != 0;

    public override string ToString() =>
        $"#{Ordinal} comp {CompetitionId} fix {FixtureOrdinal}: {HomeClubId} {HomeScore}-{AwayScore} {AwayClubId}";
}
