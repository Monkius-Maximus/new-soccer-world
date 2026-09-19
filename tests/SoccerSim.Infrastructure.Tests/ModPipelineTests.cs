using System.Globalization;
using Microsoft.Data.Sqlite;
using SoccerSim.Core.Persistence;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

/// <summary>
/// Tripwires for ADR-0007. Each contract the ADR states is verified by a deliberate
/// violation, because a rule nothing enforces is a rule the next mod author discovers is
/// optional.
/// </summary>
public sealed class ModPipelineTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), $"soccer-mods-{Guid.NewGuid():N}");

    private string Template => Path.Combine(_workspace, "world_template.db");

    public ModPipelineTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    [Fact]
    public void Mods_apply_in_the_order_the_caller_listed_them()
    {
        // Order between mods is the list position and nothing else: no manifest field can
        // reorder these, so whichever is passed last is the one that wins.
        var first = WriteMod("first", "010_rename.sql", "UPDATE club SET name = 'First' WHERE id = 1;");
        var second = WriteMod("second", "010_rename.sql", "UPDATE club SET name = 'Second' WHERE id = 1;");

        Build([first, second]);
        Assert.Equal("Second", ReadClubName(1));

        Build([second, first]);
        Assert.Equal("First", ReadClubName(1));
    }

    [Fact]
    public void Scripts_inside_a_mod_apply_in_numeric_prefix_order()
    {
        var mod = WriteMod(
            "ordered",
            ("020_second.sql", "UPDATE club SET name = 'Second' WHERE id = 1;"),
            ("010_first.sql", "UPDATE club SET name = 'First' WHERE id = 1;"));

        Build([mod]);

        // Written to disk second-then-first; applied first-then-second.
        Assert.Equal("Second", ReadClubName(1));
    }

    [Fact]
    public void A_mod_that_creates_a_table_is_rejected()
    {
        var mod = WriteMod("ddl", "010_schema.sql", "CREATE TABLE sneaky (id INTEGER PRIMARY KEY);");

        var error = Assert.Throws<InvalidDataException>(() => Build([mod]));
        Assert.Contains("altered the schema", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mod_that_rewrites_the_migration_history_is_rejected()
    {
        // No DDL runs here at all. It is still the failure ADR-0007 exists to prevent:
        // SchemaVersions is checked against MAX(version) in this table, so a row inserted
        // by a mod would make a template misreport the schema it was built against.
        var mod = WriteMod(
            "history",
            "010_bump.sql",
            "INSERT INTO schema_migrations (version, name) VALUES (9999, 'fake.sql');");

        var error = Assert.Throws<InvalidDataException>(() => Build([mod]));
        Assert.Contains("migration history", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rejected_mod_leaves_none_of_its_data_behind()
    {
        // The valid statement runs before the offending one. A mod is one transaction, so
        // neither survives: a half-applied mod is worse than a refused one.
        var mod = WriteMod(
            "partial",
            ("010_data.sql", "UPDATE club SET name = 'Should not survive' WHERE id = 1;"),
            ("020_schema.sql", "CREATE TABLE sneaky (id INTEGER PRIMARY KEY);"));

        Assert.Throws<InvalidDataException>(() => Build([mod]));
        Assert.Equal("Recife Azul", ReadClubName(1));
    }

    [Fact]
    public void A_mod_targeting_another_schema_version_is_rejected()
    {
        var mod = WriteMod("stale", "010_data.sql", "UPDATE club SET name = 'X' WHERE id = 1;");
        File.WriteAllText(
            Path.Combine(mod, "manifest.json"),
            Manifest("stale", SchemaVersions.Expected + 1));

        var error = Assert.Throws<InvalidDataException>(() => Build([mod]));
        Assert.Contains("targets schema version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manifest_carrying_an_unknown_key_is_rejected()
    {
        // load_order is the specific key ADR-0007 refuses: ordering is the launcher's list,
        // and a field claiming otherwise would be a second way to express it.
        var mod = WriteMod("extra", "010_data.sql", "UPDATE club SET name = 'X' WHERE id = 1;");
        File.WriteAllText(
            Path.Combine(mod, "manifest.json"),
            $$"""
            {
              "id": "extra",
              "name": "Extra",
              "version": "1.0.0",
              "schema_version": {{SchemaVersions.Expected}},
              "load_order": 10
            }
            """);

        var error = Assert.Throws<InvalidDataException>(() => Build([mod]));
        Assert.Contains("not a valid mod manifest", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_script_without_a_numeric_prefix_is_rejected()
    {
        var mod = WriteMod("unordered", "clubs.sql", "UPDATE club SET name = 'X' WHERE id = 1;");

        var error = Assert.Throws<InvalidDataException>(() => Build([mod]));
        Assert.Contains("numeric prefix", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_directory_without_a_manifest_is_not_a_mod()
    {
        var directory = Path.Combine(_workspace, "bare");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "010_data.sql"), "UPDATE club SET name = 'X' WHERE id = 1;");

        Assert.Throws<FileNotFoundException>(() => Build([directory]));
    }

    [Fact]
    public void The_same_mod_id_cannot_appear_twice_in_one_list()
    {
        var first = WriteMod("twice", "010_a.sql", "UPDATE club SET name = 'A' WHERE id = 1;");

        var second = Path.Combine(_workspace, "twice-copy");
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(second, "manifest.json"), Manifest("twice", SchemaVersions.Expected));
        File.WriteAllText(Path.Combine(second, "010_b.sql"), "UPDATE club SET name = 'B' WHERE id = 1;");

        var error = Assert.Throws<InvalidDataException>(() => Build([first, second]));
        Assert.Contains("more than once", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unmodded_template_is_unchanged_by_the_mod_phase()
    {
        Build([]);
        Assert.Equal("Recife Azul", ReadClubName(1));
        Assert.Equal(SchemaVersions.Expected, ReadSchemaVersion());
    }

    private void Build(IReadOnlyList<string> mods) =>
        new SqliteWorldTemplateBuilder().Build(
            Path.Combine(FindRepoRoot(), "sql", "migrations"),
            Path.Combine(FindRepoRoot(), "sql", "seeds"),
            mods,
            Template);

    private string WriteMod(string id, string scriptName, string sql) =>
        WriteMod(id, (scriptName, sql));

    private string WriteMod(string id, params (string Name, string Sql)[] scripts)
    {
        var directory = Path.Combine(_workspace, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), Manifest(id, SchemaVersions.Expected));
        foreach (var (name, sql) in scripts)
        {
            File.WriteAllText(Path.Combine(directory, name), sql);
        }
        return directory;
    }

    private static string Manifest(string id, int schemaVersion) =>
        $$"""
        {
          "id": "{{id}}",
          "name": "{{id}} mod",
          "version": "1.0.0",
          "schema_version": {{schemaVersion}}
        }
        """;

    private string ReadClubName(int clubId)
    {
        using var connection = new SqliteConnection($"Data Source={Template};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM club WHERE id = $id;";
        command.Parameters.AddWithValue("$id", clubId);
        return (string)command.ExecuteScalar()!;
    }

    private int ReadSchemaVersion()
    {
        using var connection = new SqliteConnection($"Data Source={Template};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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
