using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

public sealed class DeterminismTests
{
    private static MatchResult Play(ulong seed, WorldState? world = null) => MatchSimulation.Run(
        new MatchContext(
            TestWorld.HomeClubId,
            TestWorld.AwayClubId,
            seed,
            SimulationSettings.SimulationVersion,
            (world ?? TestWorld.Build()).Snapshot()));

    [Fact]
    public void Same_seed_and_inputs_reproduce_exactly()
    {
        var first = Play(123456UL);
        var replay = Play(123456UL);

        // Digest folds in every player position on every tick, so equality here is total.
        Assert.Equal(first.Digest, replay.Digest);
        Assert.Equal(first.FinalRandomState, replay.FinalRandomState);
        Assert.Equal(first.HomeScore, replay.HomeScore);
        Assert.Equal(first.AwayScore, replay.AwayScore);
        Assert.Equal(first.Events, replay.Events);
    }

    [Fact]
    public void Different_seed_changes_the_match()
    {
        var first = Play(123456UL);
        var second = Play(123457UL);

        Assert.NotEqual(first.Digest, second.Digest);
        Assert.NotEqual(first.FinalRandomState, second.FinalRandomState);
    }

    [Fact]
    public void Two_matches_can_run_interleaved_without_touching_each_other()
    {
        // Proves the isolation the contract promises: no shared mutable state between matches.
        var alone = Play(999UL);

        var world = TestWorld.Build();
        var a = MatchSimulation.Run(new MatchContext(
            TestWorld.HomeClubId, TestWorld.AwayClubId, 999UL, SimulationSettings.SimulationVersion, world.Snapshot()));
        _ = MatchSimulation.Run(new MatchContext(
            TestWorld.HomeClubId, TestWorld.AwayClubId, 555UL, SimulationSettings.SimulationVersion, world.Snapshot()));

        Assert.Equal(alone.Digest, a.Digest);
    }

    [Fact]
    public void Core_does_not_reference_outer_layers_or_infrastructure_packages()
    {
        var references = typeof(WorldState).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("SoccerSim.Application", references);
        Assert.DoesNotContain("SoccerSim.Infrastructure", references);
        Assert.DoesNotContain("GodotSharp", references);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", references);
    }

    [Fact]
    public void Core_source_avoids_known_nondeterministic_apis()
    {
        var root = FindRepoRoot();
        var core = Path.Combine(root, "src", "SoccerSim.Core");
        var banned = new[] { "Random.Shared", "DateTime.Now", "DateTime.UtcNow", "Guid.NewGuid()" };
        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var source = File.ReadAllText(file);
            foreach (var token in banned)
            {
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SoccerDreamGame.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
