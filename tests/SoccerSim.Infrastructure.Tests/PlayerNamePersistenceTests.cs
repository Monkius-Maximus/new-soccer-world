using SoccerSim.Core.Persistence;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

public sealed class PlayerNamePersistenceTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _template;

    public PlayerNamePersistenceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"soccer-nmg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspace);
        _template = Path.Combine(_workspace, "world_template.db");

        var root = FindRepoRoot();
        new SqliteWorldTemplateBuilder().Build(
            Path.Combine(root, "sql", "migrations"),
            Path.Combine(root, "sql", "seeds"),
            _template);
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public void Every_foundation_player_loads_the_exact_persisted_name_components()
    {
        var world = new SqliteWorldRepository().Load(_template);

        Assert.Equal(30, world.Players.Count);
        Assert.All(world.Players, player =>
        {
            Assert.NotNull(player.Name);
            Assert.Equal(player.FirstName, player.Name.GivenName);
            Assert.Equal(player.LastName, player.Name.FamilyName);
            Assert.Equal(player.Name.CommonName, player.DisplayName);
            Assert.Equal("pt-BR", player.Name.CultureId);
            Assert.Equal("0.0.0", player.Name.PackVersion);
            Assert.Equal("legacy-manual-v0", player.Name.AlgorithmVersion);
        });
    }

    [Fact]
    public void Missing_persisted_name_fails_instead_of_regenerating_on_load()
    {
        Execute("DELETE FROM player_name WHERE player_id = 1;");

        var failure = Assert.Throws<InvalidDataException>(() =>
            new SqliteWorldRepository().Load(_template));
        Assert.Contains("never regenerated", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Divergent_legacy_and_canonical_components_fail_loudly()
    {
        Execute("UPDATE player_name SET given_name = 'Outro' WHERE player_id = 1;");

        var failure = Assert.Throws<InvalidDataException>(() =>
            new SqliteWorldRepository().Load(_template));
        Assert.Contains("divergent", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Database_rejects_blank_or_overlong_presentation_names()
    {
        Assert.ThrowsAny<Exception>(() =>
            Execute("UPDATE player_name SET common_name = '' WHERE player_id = 1;"));
        Assert.ThrowsAny<Exception>(() =>
            Execute("UPDATE player_name SET shirt_name = 'ABCDEFGHIJKLMNOPQ' WHERE player_id = 1;"));
    }

    [Fact]
    public void Name_migration_is_the_schema_version_expected_by_this_build()
    {
        Assert.Equal(5, SchemaVersions.Expected);
    }

    private void Execute(string sql)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source={_template};Mode=ReadWrite;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
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
