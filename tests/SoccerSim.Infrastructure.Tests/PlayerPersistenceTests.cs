using SoccerSim.Application;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Persistence;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

/// <summary>
/// PLYR-00 through the storage boundary: the schema enforces the contract, the seed satisfies
/// it, and a stale save is refused rather than half-read.
/// </summary>
public sealed class PlayerPersistenceTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _template;

    public PlayerPersistenceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"soccer-plyr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspace);
        _template = Path.Combine(_workspace, "world_template.db");

        var root = FindRepoRoot();
        new SqliteWorldTemplateBuilder().Build(
            Path.Combine(root, "sql", "migrations"),
            Path.Combine(root, "sql", "seeds"),
            [],
            _template);
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    private WorldState LoadWorld() => new SqliteWorldRepository().Load(_template);

    [Fact]
    public void The_template_is_at_the_schema_version_this_build_expects()
    {
        var store = new SqliteCareerStore();
        var save = store.CreateCareer(
            _template, _workspace, "plyr", 1UL, "0.0.1", DateTimeOffset.UnixEpoch);

        Assert.Equal(SchemaVersions.Expected, save.Metadata.SchemaVersion);
    }

    [Fact]
    public void Every_seeded_player_has_a_typed_position_and_valid_attributes()
    {
        var players = LoadWorld().Players;
        Assert.Equal(30, players.Count);

        foreach (var player in players)
        {
            // Round-tripping through the enum proves the stored code was in the closed set.
            Assert.Equal(player.Position, PlayerPositions.Parse(player.PositionCode));

            foreach (var value in Attributes(player.Attributes))
            {
                Assert.InRange(value, PlayerAttributes.Minimum, PlayerAttributes.Maximum);
            }
        }
    }

    [Fact]
    public void Both_squads_can_field_a_goalkeeper_and_every_line()
    {
        // MATCH cannot pick a starting eleven from a squad with no keeper or no attackers.
        foreach (var club in LoadWorld().Clubs)
        {
            var lines = LoadWorld().GetRoster(club.Id).Select(player => player.Line).Distinct().ToArray();

            Assert.Contains(PitchLine.Goalkeeper, lines);
            Assert.Contains(PitchLine.Defence, lines);
            Assert.Contains(PitchLine.Midfield, lines);
            Assert.Contains(PitchLine.Attack, lines);
        }
    }

    [Fact]
    public void Goalkeepers_are_the_best_shot_stoppers_in_their_squad()
    {
        // Content sanity: the seed is hand-authored, so this catches a copy-paste that would
        // otherwise only surface as strange match results much later.
        var world = LoadWorld();

        foreach (var club in world.Clubs)
        {
            var roster = world.GetRoster(club.Id);
            var bestKeeper = roster.Where(p => p.Position == PlayerPosition.Goalkeeper)
                .Max(p => p.Attributes.Goalkeeping);
            var bestOutfield = roster.Where(p => p.Position != PlayerPosition.Goalkeeper)
                .Max(p => p.Attributes.Goalkeeping);

            Assert.True(bestKeeper > bestOutfield,
                $"{club.Name}: an outfielder rates {bestOutfield} in goalkeeping vs the keeper's {bestKeeper}.");
        }
    }

    [Fact]
    public void The_schema_refuses_an_attribute_outside_the_scale()
    {
        Assert.ThrowsAny<Exception>(() => ExecuteOnTemplate(
            "UPDATE player SET pace = 21 WHERE id = 1;"));
        Assert.ThrowsAny<Exception>(() => ExecuteOnTemplate(
            "UPDATE player SET goalkeeping = 0 WHERE id = 1;"));
    }

    [Fact]
    public void The_schema_refuses_a_position_outside_the_closed_set()
    {
        Assert.ThrowsAny<Exception>(() => ExecuteOnTemplate(
            "UPDATE player SET position_code = 'XX' WHERE id = 1;"));
    }

    [Fact]
    public void A_career_written_against_another_schema_version_is_refused()
    {
        var store = new SqliteCareerStore();
        var save = store.CreateCareer(
            _template, _workspace, "stale", 1UL, "0.0.1", DateTimeOffset.UnixEpoch);

        // Simulate a save produced by a build one migration behind.
        ExecuteOn(save.DatabasePath,
            $"UPDATE save_metadata SET schema_version = {SchemaVersions.Expected - 1};");

        var failure = Assert.Throws<InvalidDataException>(() => store.OpenCareer(save.DatabasePath));
        Assert.Contains("schema version", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<int> Attributes(PlayerAttributes a) =>
    [
        a.Pace, a.Stamina, a.Strength, a.Passing, a.Shooting,
        a.Tackling, a.Dribbling, a.Positioning, a.Goalkeeping
    ];

    private void ExecuteOnTemplate(string sql) => ExecuteOn(_template, sql);

    private static void ExecuteOn(string databasePath, string sql)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source={databasePath};Mode=ReadWrite;Pooling=False");
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
