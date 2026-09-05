using SoccerSim.Core.Competitions;

namespace SoccerSim.Core.Tests;

public sealed class MatchSeedTests
{
    [Fact]
    public void The_same_fixture_in_the_same_career_always_gets_the_same_seed()
    {
        Assert.Equal(MatchSeeds.For(99UL, 2, 17), MatchSeeds.For(99UL, 2, 17));
    }

    [Fact]
    public void Every_input_changes_the_seed()
    {
        var baseline = MatchSeeds.For(99UL, 2, 17);

        Assert.NotEqual(baseline, MatchSeeds.For(100UL, 2, 17));
        Assert.NotEqual(baseline, MatchSeeds.For(99UL, 3, 17));
        Assert.NotEqual(baseline, MatchSeeds.For(99UL, 2, 18));
    }

    [Fact]
    public void A_whole_season_of_fixtures_gets_distinct_seeds()
    {
        var fixtures = RoundRobinScheduler.Build(1, [.. Enumerable.Range(1, 20)], new DateOnly(2026, 2, 7));

        var seeds = fixtures.Select(f => MatchSeeds.For(123456789UL, f.CompetitionId, f.Ordinal)).ToArray();

        Assert.Equal(fixtures.Count, seeds.Distinct().Count());
    }
}
