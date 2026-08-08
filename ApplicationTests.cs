using SoccerSim.Application;
using SoccerSim.Core.Domain;

namespace SoccerSim.Application.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public void Roster_query_is_stably_ordered()
    {
        var world = new WorldState(
            [new Country(1, "BRA", "Brasil")],
            [new City(1, 1, "Recife")],
            [new Stadium(1, 1, "Teste", 1000)],
            [new Club(2, 1, 1, "B", "BBB"), new Club(1, 1, 1, "A", "AAA")],
            [
                new Player(2, 1, 1, "B", "Dois", new DateOnly(2000, 1, 1), 10, "ST"),
                new Player(1, 1, 1, "A", "Um", new DateOnly(2000, 1, 1), 1, "GK")
            ],
            []);

        var result = new ClubRosterQueryService().GetClubRosters(world);
        Assert.Equal(new[] { 1, 2 }, result.Select(x => x.ClubId));
        Assert.Equal(new[] { 1, 10 }, result[0].Players.Select(x => x.SquadNumber));
    }

    [Fact]
    public void Application_only_depends_on_core_inside_soccer_sim()
    {
        var references = typeof(CareerApplication).Assembly.GetReferencedAssemblies()
            .Select(x => x.Name)
            .Where(x => x is not null && x.StartsWith("SoccerSim.", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(new[] { "SoccerSim.Core" }, references);
    }
}
