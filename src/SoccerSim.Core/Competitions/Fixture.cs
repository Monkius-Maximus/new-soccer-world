namespace SoccerSim.Core.Competitions;

/// <summary>
/// One scheduled match. Identity is <see cref="Ordinal"/>, assigned by the scheduler, so a
/// fixture list is stable across saves and its order never depends on how it was stored.
/// </summary>
/// <param name="Matchday">1-based round this fixture belongs to.</param>
public sealed record Fixture(
    int Ordinal,
    int CompetitionId,
    int Matchday,
    int HomeClubId,
    int AwayClubId,
    DateOnly Date)
{
    public bool Involves(int clubId) => HomeClubId == clubId || AwayClubId == clubId;

    public override string ToString() => $"MD{Matchday} #{Ordinal}: {HomeClubId} v {AwayClubId} ({Date:yyyy-MM-dd})";
}
