namespace SoccerSim.Core.Match;

/// <summary>
/// What a match hands back. It holds no reference to the world it ran against, so the
/// Application decides when and in which order to apply it.
/// </summary>
public sealed record MatchResult(
    int HomeClubId,
    int AwayClubId,
    int HomeScore,
    int AwayScore,
    ulong Seed,
    int SimulationVersion,
    int Ticks,
    ulong Digest,
    ulong FinalRandomState,
    IReadOnlyList<MatchEvent> Events,
    IReadOnlyList<MatchFrame> Frames)
{
    public IEnumerable<MatchEvent> Goals => Events.Where(e => e.Kind == MatchEventKind.Goal);

    /// <summary>
    /// Value equality, including the event stream.
    /// <para>
    /// The compiler-generated version would compare <see cref="Events"/> by reference, so two
    /// identical replays would never be equal — exactly the comparison callers reach for when
    /// checking that a match reproduced.
    /// </para>
    /// </summary>
    public bool Equals(MatchResult? other) =>
        other is not null &&
        HomeClubId == other.HomeClubId &&
        AwayClubId == other.AwayClubId &&
        HomeScore == other.HomeScore &&
        AwayScore == other.AwayScore &&
        Seed == other.Seed &&
        SimulationVersion == other.SimulationVersion &&
        Ticks == other.Ticks &&
        Digest == other.Digest &&
        FinalRandomState == other.FinalRandomState &&
        Events.SequenceEqual(other.Events) &&
        FramesEqual(Frames, other.Frames);

    public override int GetHashCode() =>
        HashCode.Combine(HomeClubId, AwayClubId, HomeScore, AwayScore, Seed, Digest, Events.Count, Frames.Count);

    private static bool FramesEqual(IReadOnlyList<MatchFrame> left, IReadOnlyList<MatchFrame> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            var a = left[i];
            var b = right[i];
            if (a.Tick != b.Tick ||
                a.HomeScore != b.HomeScore ||
                a.AwayScore != b.AwayScore ||
                a.BallLocation != b.BallLocation ||
                a.CarrierPlayerId != b.CarrierPlayerId ||
                !a.Players.SequenceEqual(b.Players))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString() => $"{HomeScore}-{AwayScore} (seed {Seed}, digest {Digest:X16})";
}
