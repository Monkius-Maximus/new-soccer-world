using SoccerSim.Core.Domain;
using SoccerSim.Core.Tactics;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

/// <summary>TACT-00 through the storage boundary: the schema enforces it, the seed uses it.</summary>
public sealed class TacticsPersistenceTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _template;

    public TacticsPersistenceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"soccer-tact-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspace);
        _template = Path.Combine(_workspace, "world_template.db");

        var root = FindRepoRoot();
        new SqliteWorldTemplateBuilder().Build(
            Path.Combine(root, "sql", "migrations"),
            Path.Combine(root, "sql", "seeds"),
            _template);
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    private WorldState LoadWorld() => new SqliteWorldRepository().Load(_template);

    [Fact]
    public void Every_seeded_club_has_a_setup_the_simulation_understands()
    {
        var world = LoadWorld();

        Assert.Equal(world.Clubs.Count, world.ClubTactics.Count);

        foreach (var club in world.Clubs)
        {
            var tactics = world.TacticsFor(club.Id);
            Assert.Contains(tactics.Formation, Formations.All);
            Assert.InRange(tactics.DefensiveLineHeight, TeamTactics.Minimum, TeamTactics.Maximum);
            Assert.InRange(tactics.PressingIntensity, TeamTactics.Minimum, TeamTactics.Maximum);
            Assert.InRange(tactics.Directness, TeamTactics.Minimum, TeamTactics.Maximum);
        }
    }

    [Fact]
    public void The_two_seeded_clubs_deliberately_set_up_differently()
    {
        // If both shipped the same setup, a match between them would prove nothing about TACT-00.
        var world = LoadWorld();
        var setups = world.Clubs.Select(club => world.TacticsFor(club.Id)).ToArray();

        Assert.Equal(2, setups.Length);
        Assert.NotEqual(setups[0].Formation.Name, setups[1].Formation.Name);
        Assert.NotEqual(setups[0].PressingIntensity, setups[1].PressingIntensity);
    }

    [Fact]
    public void The_schema_refuses_a_formation_the_simulation_cannot_build()
    {
        Assert.ThrowsAny<Exception>(() => Execute("UPDATE club_tactics SET formation = '4-4-3' WHERE club_id = 1;"));
    }

    [Theory]
    [InlineData("defensive_line_height", 0)]
    [InlineData("pressing_intensity", 21)]
    [InlineData("directness", 0)]
    public void The_schema_refuses_an_instruction_outside_the_scale(string column, int value)
    {
        Assert.ThrowsAny<Exception>(() => Execute($"UPDATE club_tactics SET {column} = {value} WHERE club_id = 1;"));
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
