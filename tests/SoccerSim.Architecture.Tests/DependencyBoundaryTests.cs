namespace SoccerSim.Architecture.Tests;

/// <summary>
/// Enforces the layering decided in ADR-0002. These tests fail the build when a boundary
/// is crossed, which is the whole point of writing them before the layers grow.
/// </summary>
public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Core_references_no_other_project()
    {
        Assert.Empty(RepoLayout.ProjectReferences(RepoLayout.Core));
    }

    [Fact]
    public void Core_references_no_nuget_package()
    {
        // Core must stay engine- and storage-agnostic: no Godot, no SQLite, nothing at all.
        Assert.Empty(RepoLayout.PackageReferences(RepoLayout.Core));
    }

    [Fact]
    public void Core_source_never_mentions_godot_sqlite_or_outer_layers()
    {
        string[] forbidden = ["Godot", "Sqlite", "SQLite", "SoccerSim.Application", "SoccerSim.Infrastructure"];

        foreach (var (path, text) in RepoLayout.SourceFiles("src/SoccerSim.Core"))
        {
            foreach (var token in forbidden)
            {
                Assert.False(
                    text.Contains(token, StringComparison.Ordinal),
                    $"Core file '{path}' must not mention '{token}'.");
            }
        }
    }

    [Fact]
    public void Application_references_core_and_nothing_else()
    {
        Assert.Equal(new[] { "SoccerSim.Core" }, RepoLayout.ProjectReferences(RepoLayout.Application));
        Assert.Empty(RepoLayout.PackageReferences(RepoLayout.Application));
    }

    [Fact]
    public void Application_never_mentions_infrastructure_godot_or_sql()
    {
        string[] forbidden = ["Godot", "Sqlite", "SoccerSim.Infrastructure"];

        foreach (var (path, text) in RepoLayout.SourceFiles("src/SoccerSim.Application"))
        {
            foreach (var token in forbidden)
            {
                Assert.False(
                    text.Contains(token, StringComparison.Ordinal),
                    $"Application file '{path}' must not mention '{token}'.");
            }
        }
    }

    [Fact]
    public void Infrastructure_depends_on_core_only_and_implements_its_ports()
    {
        Assert.Equal(new[] { "SoccerSim.Core" }, RepoLayout.ProjectReferences(RepoLayout.Infrastructure));

        // Infrastructure exists to implement ports declared by the inner layers.
        var sources = RepoLayout.SourceFiles("src/SoccerSim.Infrastructure").ToArray();
        Assert.Contains(sources, file => file.Text.Contains(": ICareerStore", StringComparison.Ordinal));
        Assert.Contains(sources, file => file.Text.Contains(": IWorldRepository", StringComparison.Ordinal));

        foreach (var (path, text) in sources)
        {
            Assert.False(
                text.Contains("Godot", StringComparison.Ordinal),
                $"Infrastructure file '{path}' must not mention Godot.");
            Assert.False(
                text.Contains("SoccerSim.Application", StringComparison.Ordinal),
                $"Infrastructure file '{path}' must not reach up into Application.");
        }
    }

    [Fact]
    public void Ports_are_declared_by_core_not_by_infrastructure()
    {
        var portDirectory = Path.Combine(RepoLayout.Root, "src", "SoccerSim.Core", "Persistence");
        Assert.True(File.Exists(Path.Combine(portDirectory, "ICareerStore.cs")));
        Assert.True(File.Exists(Path.Combine(portDirectory, "IWorldRepository.cs")));
    }

    [Fact]
    public void Presentation_reaches_the_world_through_application_use_cases()
    {
        // Godot may reference Infrastructure, but only so the composition root can inject an
        // adapter. Everything the scenes consume has to come from Application.
        var references = RepoLayout.ProjectReferences(RepoLayout.Presentation);
        Assert.Contains("SoccerSim.Application", references);

        var sources = RepoLayout.SourceFiles("game/SoccerDreamGame").ToArray();
        Assert.Contains(sources, file => file.Text.Contains("CareerApplication", StringComparison.Ordinal));

        foreach (var (path, text) in sources)
        {
            if (Path.GetFileName(path) == "CompositionRoot.cs")
            {
                continue;
            }

            Assert.False(
                text.Contains("SoccerSim.Infrastructure", StringComparison.Ordinal),
                $"Godot script '{path}' must reach the world through Application, not Infrastructure.");
            Assert.False(
                text.Contains("Repository", StringComparison.Ordinal),
                $"Godot script '{path}' must not touch a repository directly.");
        }
    }

    [Fact]
    public void Presentation_never_executes_sql()
    {
        string[] forbidden =
        [
            "SqliteConnection", "SqliteCommand", "DbConnection", "CommandText",
            "SELECT ", "INSERT INTO", "UPDATE ", "DELETE FROM", "PRAGMA "
        ];

        foreach (var (path, text) in RepoLayout.SourceFiles("game/SoccerDreamGame"))
        {
            foreach (var token in forbidden)
            {
                Assert.False(
                    text.Contains(token, StringComparison.OrdinalIgnoreCase),
                    $"Godot script '{path}' must not execute SQL, but it contains '{token}'.");
            }
        }
    }

    [Fact]
    public void Presentation_declares_no_database_package()
    {
        foreach (var package in RepoLayout.PackageReferences(RepoLayout.Presentation))
        {
            Assert.DoesNotContain("Sqlite", package, StringComparison.OrdinalIgnoreCase);
        }
    }
}
