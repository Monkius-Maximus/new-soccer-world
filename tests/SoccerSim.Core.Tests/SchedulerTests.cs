using SoccerSim.Core.Competitions;

namespace SoccerSim.Core.Tests;

/// <summary>
/// A fixture list is easy to get subtly wrong — a pair that meets three times, a club playing
/// twice on one day, or a season that quietly loses a match. Each of those is checked here for
/// every squad size the world might plausibly have.
/// </summary>
public sealed class SchedulerTests
{
    private static readonly DateOnly Start = new(2026, 8, 8);

    private static IReadOnlyList<int> Clubs(int count) => [.. Enumerable.Range(1, count)];

    private static IReadOnlyList<Fixture> Build(int count) =>
        RoundRobinScheduler.Build(1, Clubs(count), Start);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(20)]
    public void Every_pair_meets_exactly_twice_once_at_each_ground(int count)
    {
        var fixtures = Build(count);

        foreach (var home in Clubs(count))
        {
            foreach (var away in Clubs(count))
            {
                if (home == away)
                {
                    continue;
                }

                var atHome = fixtures.Count(f => f.HomeClubId == home && f.AwayClubId == away);
                Assert.True(atHome == 1, $"{count} clubs: {home} hosts {away} {atHome} times, expected 1.");
            }
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(20)]
    public void No_club_plays_twice_on_the_same_matchday(int count)
    {
        foreach (var matchday in Build(count).GroupBy(f => f.Matchday))
        {
            var appearances = matchday.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).ToArray();
            Assert.Equal(appearances.Length, appearances.Distinct().Count());
        }
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 6)]
    [InlineData(6, 10)]
    [InlineData(8, 14)]
    public void An_even_league_runs_two_rounds_of_n_minus_one_matchdays(int count, int expectedMatchdays)
    {
        var fixtures = Build(count);

        Assert.Equal(expectedMatchdays, fixtures.Max(f => f.Matchday));
        Assert.Equal(count * (count - 1), fixtures.Count);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void An_odd_league_still_plays_a_full_double_round_robin(int count)
    {
        var fixtures = Build(count);

        // Byes mean some matchdays are short, but every tie is still played twice.
        Assert.Equal(count * (count - 1), fixtures.Count);

        foreach (var club in Clubs(count))
        {
            Assert.Equal(2 * (count - 1), fixtures.Count(f => f.Involves(club)));
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void Home_and_away_matches_are_split_evenly(int count)
    {
        var fixtures = Build(count);

        foreach (var club in Clubs(count))
        {
            Assert.Equal(count - 1, fixtures.Count(f => f.HomeClubId == club));
            Assert.Equal(count - 1, fixtures.Count(f => f.AwayClubId == club));
        }
    }

    [Fact]
    public void Ordinals_are_dense_and_matchdays_are_dated_a_week_apart()
    {
        var fixtures = Build(6);

        Assert.Equal(Enumerable.Range(1, fixtures.Count), fixtures.Select(f => f.Ordinal));

        foreach (var fixture in fixtures)
        {
            var expected = Start.AddDays((fixture.Matchday - 1) * RoundRobinScheduler.DaysBetweenMatchdays);
            Assert.Equal(expected, fixture.Date);
        }
    }

    [Fact]
    public void The_same_clubs_always_produce_the_same_fixture_list()
    {
        // A season has to be replayable from its seed, which a shuffled draw would prevent.
        Assert.Equal(Build(8), RoundRobinScheduler.Build(1, [.. Clubs(8).Reverse()], Start));
    }

    [Fact]
    public void A_competition_needs_at_least_two_distinct_clubs()
    {
        Assert.Throws<ArgumentException>(() => RoundRobinScheduler.Build(1, [1], Start));
        Assert.Throws<ArgumentException>(() => RoundRobinScheduler.Build(1, [1, 1], Start));
    }
}
