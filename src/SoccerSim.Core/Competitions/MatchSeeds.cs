namespace SoccerSim.Core.Competitions;

/// <summary>
/// Derives a match seed from the career seed and the fixture it settles.
/// <para>
/// Every match in a season needs its own seed, and those seeds have to be a function of the
/// career rather than of when the match happened to be played. Deriving them means a season
/// replays identically whether it was advanced one round at a time or all at once, and that a
/// single fixture can be re-simulated in isolation.
/// </para>
/// </summary>
public static class MatchSeeds
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static ulong For(ulong careerSeed, int competitionId, int fixtureOrdinal)
    {
        var seed = FnvOffsetBasis;
        seed = unchecked((seed ^ careerSeed) * FnvPrime);
        seed = unchecked((seed ^ (ulong)(uint)competitionId) * FnvPrime);
        seed = unchecked((seed ^ (ulong)(uint)fixtureOrdinal) * FnvPrime);
        return seed;
    }
}
