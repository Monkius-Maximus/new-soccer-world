using SoccerSim.Core.Domain;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

public sealed class DeterminismTests
{
    private static readonly WorldState EmptyWorld = new([], [], [], [], [], []);

    [Fact]
    public void Same_seed_and_inputs_reproduce_exactly()
    {
        var context = new MatchContext(1, 2, 123456UL, SimulationSettings.SimulationVersion, EmptyWorld);
        var first = DeterministicSimulationProbe.Run(context, 4096);
        var replay = DeterministicSimulationProbe.Run(context, 4096);
        Assert.Equal(first, replay);
    }

    [Fact]
    public void Different_seed_changes_probe_state()
    {
        var first = DeterministicSimulationProbe.Run(
            new MatchContext(1, 2, 123456UL, SimulationSettings.SimulationVersion, EmptyWorld), 4096);
        var second = DeterministicSimulationProbe.Run(
            new MatchContext(1, 2, 123457UL, SimulationSettings.SimulationVersion, EmptyWorld), 4096);
        Assert.NotEqual(first.Digest, second.Digest);
        Assert.NotEqual(first.FinalRandomState, second.FinalRandomState);
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
