using System.Reflection;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Architecture.Tests;

/// <summary>
/// A match must run against a snapshot and hand results back, never reach into the world
/// it was launched from. Foundation only proves the shape; MATCH will add the football.
/// </summary>
public sealed class MatchIsolationTests
{
    private static readonly PlayerAttributes Ordinary = new(10, 10, 10, 10, 10, 10, 10, 10, 10);

    private static WorldState BuildWorld() => new(
        [new Country(1, "BRA", "Brasil")],
        [new City(1, 1, "Recife")],
        [new Stadium(1, 1, "Estadio das Pontes", 18000)],
        [new Club(1, 1, 1, "Recife Azul", "RAZ"), new Club(2, 1, 1, "Recife Vermelho", "RVM")],
        [new Player(1, 1, 1, "Caio", "Alencar", new DateOnly(2000, 1, 1), 1, PlayerPosition.Goalkeeper, Ordinary)],
        [new Competition(1, 1, "Amistoso da Fundacao", "friendly")]);

    [Fact]
    public void World_state_exposes_no_public_mutation_surface()
    {
        var settable = typeof(WorldState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(settable);

        var collections = typeof(WorldState)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType));

        foreach (var property in collections)
        {
            // IReadOnlyList, never IList: presentation and simulation cannot write through it.
            Assert.False(
                typeof(System.Collections.IList).IsAssignableFrom(property.PropertyType),
                $"WorldState.{property.Name} must not be exposed as a mutable list.");
        }
    }

    [Fact]
    public void Match_context_receives_a_snapshot_that_is_not_the_live_world()
    {
        var world = BuildWorld();
        var snapshot = world.Snapshot();

        Assert.NotSame(world, snapshot);
        Assert.Equal(world.Clubs.Select(club => club.Id), snapshot.Clubs.Select(club => club.Id));
    }

    [Fact]
    public void Running_a_match_leaves_the_originating_world_untouched()
    {
        var world = BuildWorld();
        var before = Describe(world);

        var context = new MatchContext(1, 2, 4242UL, SimulationSettings.SimulationVersion, world.Snapshot());
        var result = DeterministicSimulationProbe.Run(context, 512);

        // The match returns a result; it does not write anything back.
        Assert.Equal(SimulationSettings.SimulationVersion, result.SimulationVersion);
        Assert.Equal(before, Describe(world));
    }

    [Fact]
    public void Mutating_a_snapshot_cannot_reach_the_world_it_came_from()
    {
        var world = BuildWorld();
        world.ApplySimulationRun(1, 2, 1UL, SimulationSettings.SimulationVersion, 128, 42UL);
        world.MarkPersisted();

        var snapshot = world.Snapshot();
        snapshot.ApplySimulationRun(2, 1, 99UL, SimulationSettings.SimulationVersion, 128, 7UL);

        // The snapshot moved on; the career world did not.
        Assert.Equal(2, snapshot.SimulationRuns.Count);
        Assert.Single(world.SimulationRuns);
        Assert.False(world.HasUnsavedChanges);
    }

    [Fact]
    public void Applied_progress_is_the_only_thing_that_marks_a_world_unsaved()
    {
        var world = BuildWorld();
        Assert.False(world.HasUnsavedChanges);

        // Reading the world — which is all a match does — never dirties it.
        _ = world.GetRoster(1);
        _ = world.Snapshot();
        DeterministicSimulationProbe.Run(
            new MatchContext(1, 2, 5UL, SimulationSettings.SimulationVersion, world.Snapshot()), 128);
        Assert.False(world.HasUnsavedChanges);

        world.ApplySimulationRun(1, 2, 5UL, SimulationSettings.SimulationVersion, 128, 1UL);
        Assert.True(world.HasUnsavedChanges);
    }

    private static string Describe(WorldState world) => string.Join(
        "|",
        world.Countries.Select(x => x.ToString())
            .Concat(world.Cities.Select(x => x.ToString()))
            .Concat(world.Stadiums.Select(x => x.ToString()))
            .Concat(world.Clubs.Select(x => x.ToString()))
            .Concat(world.Players.Select(x => x.ToString()))
            .Concat(world.Competitions.Select(x => x.ToString()))
            .Concat(world.SimulationRuns.Select(x => x.ToString()))
            .Append($"dirty={world.HasUnsavedChanges}"));
}
