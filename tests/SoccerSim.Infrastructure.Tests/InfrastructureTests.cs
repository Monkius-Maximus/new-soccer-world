using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Migrations_and_seed_build_the_minimal_recife_world()
    {
        var root = FindRepoRoot();
        var temp = Path.Combine(Path.GetTempPath(), $"soccer-world-{Guid.NewGuid():N}.db");
        try
        {
            new SqliteWorldTemplateBuilder().Build(
                Path.Combine(root, "sql", "migrations"),
                Path.Combine(root, "sql", "seeds"),
                temp);

            var world = new SqliteWorldRepository().Load(temp);
            Assert.Single(world.Countries);
            Assert.Single(world.Cities);
            Assert.Equal("Recife", world.Cities[0].Name);
            Assert.Equal(2, world.Clubs.Count);
            Assert.Equal(30, world.Players.Count);

            // COMP-00: the friendly the Foundation shipped, plus the league a career plays.
            Assert.Equal(
                new[] { "friendly", "league" },
                world.Competitions.Select(competition => competition.CompetitionType));
            Assert.NotNull(world.Season);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Career_save_is_a_template_copy_with_metadata_and_explicit_checkpoint()
    {
        var root = FindRepoRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"soccer-save-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var template = Path.Combine(tempRoot, "world_template.db");
        try
        {
            new SqliteWorldTemplateBuilder().Build(
                Path.Combine(root, "sql", "migrations"),
                Path.Combine(root, "sql", "seeds"),
                template);

            var store = new SqliteCareerStore();
            var createdAt = new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);
            var save = store.CreateCareer(template, tempRoot, "career-1", 99UL, "0.0.1", "foundation-1", createdAt);
            Assert.True(File.Exists(save.DatabasePath));
            // Tracks the highest applied migration in sql/migrations.
            Assert.Equal(SoccerSim.Core.Persistence.SchemaVersions.Expected, save.Metadata.SchemaVersion);
            Assert.Equal(99UL, save.Metadata.CareerSeed);

            var world = store.LoadWorld(save.DatabasePath);
            var checkpointAt = createdAt.AddMinutes(5);
            var updated = store.Checkpoint(save, world, SoccerSim.Core.Persistence.CheckpointKind.Manual, checkpointAt);
            var reopened = store.OpenCareer(save.DatabasePath);
            Assert.Equal(checkpointAt, updated.Metadata.LastPlayedAt);
            Assert.Equal(checkpointAt, reopened.Metadata.LastPlayedAt);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Infrastructure_does_not_reference_godot_or_application()
    {
        var references = typeof(SqliteWorldRepository).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("GodotSharp", references);
        Assert.DoesNotContain("SoccerSim.Application", references);
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
