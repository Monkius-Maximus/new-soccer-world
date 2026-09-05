namespace SoccerSim.Core.Competitions;

/// <summary>
/// Builds a double round-robin: everyone plays everyone home and away.
/// <para>
/// Uses the circle method, which is deterministic and needs no randomness at all — a fixture
/// list that shuffled would make a season unreplayable from its seed. An odd number of clubs
/// gets a bye each round, so the generator works for any size the world happens to have.
/// </para>
/// </summary>
public static class RoundRobinScheduler
{
    /// <summary>Days between matchdays. A weekly calendar is enough until congestion is modelled.</summary>
    public const int DaysBetweenMatchdays = 7;

    private const int Bye = 0;

    /// <summary>
    /// How many matchdays a double round-robin between this many clubs takes. An odd league
    /// plays a bye round, so it needs one more matchday per half than an even one.
    /// </summary>
    public static int MatchdayCount(int clubCount)
    {
        if (clubCount < 2)
        {
            return 0;
        }

        var teams = clubCount % 2 == 0 ? clubCount : clubCount + 1;
        return 2 * (teams - 1);
    }

    public static IReadOnlyList<Fixture> Build(int competitionId, IReadOnlyList<int> clubIds, DateOnly firstMatchday)
    {
        ArgumentNullException.ThrowIfNull(clubIds);

        if (clubIds.Count < 2)
        {
            throw new ArgumentException("A competition needs at least two clubs.", nameof(clubIds));
        }

        if (clubIds.Distinct().Count() != clubIds.Count)
        {
            throw new ArgumentException("A club cannot appear twice in a competition.", nameof(clubIds));
        }

        // A bye keeps the rotation even; the club drawn against it simply does not play.
        var rotation = clubIds.OrderBy(id => id).ToList();
        if (rotation.Count % 2 != 0)
        {
            rotation.Add(Bye);
        }

        var teams = rotation.Count;
        var roundsPerHalf = MatchdayCount(clubIds.Count) / 2;
        var fixtures = new List<Fixture>();
        var ordinal = 0;

        for (var half = 0; half < 2; half++)
        {
            for (var round = 0; round < roundsPerHalf; round++)
            {
                var matchday = (half * roundsPerHalf) + round + 1;
                var date = firstMatchday.AddDays((matchday - 1) * DaysBetweenMatchdays);

                for (var pair = 0; pair < teams / 2; pair++)
                {
                    var (first, second) = PairFor(rotation, round, pair, teams);
                    if (first == Bye || second == Bye)
                    {
                        continue;
                    }

                    // Alternating which side is at home spreads home matches evenly, and the
                    // second half reverses every tie so each pair meets once at each ground.
                    var homeFirst = (round + pair) % 2 == 0;
                    if (half == 1)
                    {
                        homeFirst = !homeFirst;
                    }

                    var home = homeFirst ? first : second;
                    var away = homeFirst ? second : first;

                    fixtures.Add(new Fixture(++ordinal, competitionId, matchday, home, away, date));
                }
            }
        }

        return fixtures;
    }

    /// <summary>The circle method: club zero is fixed and the rest rotate around it.</summary>
    private static (int First, int Second) PairFor(List<int> rotation, int round, int pair, int teams)
    {
        var last = teams - 1;

        int Rotated(int index) =>
            index == 0 ? rotation[0] : rotation[(((index - 1 + round) % last) + 1)];

        return (Rotated(pair), Rotated(teams - 1 - pair));
    }
}
