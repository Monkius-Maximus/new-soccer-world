using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using SoccerSim.Infrastructure.Mods;

namespace SoccerSim.Infrastructure.Sqlite;

public sealed class SqliteWorldTemplateBuilder
{
    /// <param name="modDirectories">
    /// Mods to apply over the seeded base, in the order the caller chose. That list position
    /// <em>is</em> the order between mods (ADR-0007), which is why there is no ordering field
    /// in a manifest. Pass an empty list for an unmodded template.
    /// </param>
    public void Build(
        string migrationsDirectory,
        string seedsDirectory,
        IReadOnlyList<string> modDirectories,
        string outputDatabasePath)
    {
        ArgumentNullException.ThrowIfNull(modDirectories);
        if (!Directory.Exists(migrationsDirectory))
        {
            throw new DirectoryNotFoundException(migrationsDirectory);
        }
        if (!Directory.Exists(seedsDirectory))
        {
            throw new DirectoryNotFoundException(seedsDirectory);
        }

        // Every mod is validated before the database is touched, so a malformed one fails
        // before any writing rather than halfway through a template.
        var mods = modDirectories.Select(ModLoader.Load).ToArray();
        RequireDistinctIds(mods);

        var fullOutputPath = Path.GetFullPath(outputDatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        if (File.Exists(fullOutputPath))
        {
            File.Delete(fullOutputPath);
        }

        using var connection = new SqliteConnection(
            $"Data Source={fullOutputPath};Mode=ReadWriteCreate;Pooling=False");
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = ON;");

        var migrationFiles = Directory.EnumerateFiles(migrationsDirectory, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        foreach (var file in migrationFiles)
        {
            ApplyScript(connection, file);
            var version = ParseMigrationVersion(file);
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO schema_migrations (version, name) VALUES ($version, $name);";
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$name", Path.GetFileName(file));
            command.ExecuteNonQuery();
        }

        foreach (var file in Directory.EnumerateFiles(seedsDirectory, "*.sql")
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            ApplyScript(connection, file);
        }

        ApplyMods(connection, mods);
    }

    /// <summary>
    /// Applies each mod as one transaction, then proves it changed only data.
    /// </summary>
    private static void ApplyMods(SqliteConnection connection, IReadOnlyList<ModPackage> mods)
    {
        var baseline = ReadStructuralFingerprint(connection, transaction: null);

        foreach (var mod in mods)
        {
            using var transaction = connection.BeginTransaction();

            foreach (var script in mod.Scripts)
            {
                Execute(connection, transaction, File.ReadAllText(script));
            }

            // Asking SQLite what the schema now looks like beats scanning the script for
            // words like CREATE: a string literal or a comment containing 'ALTER TABLE' is
            // not DDL, and an exotic statement that is DDL would not have to spell it that
            // way. The fingerprint is read inside the transaction, so the comparison sees
            // exactly what this mod did and nothing is committed when it did too much.
            if (!string.Equals(
                    ReadStructuralFingerprint(connection, transaction),
                    baseline,
                    StringComparison.Ordinal))
            {
                transaction.Rollback();
                throw new InvalidDataException(
                    $"Mod '{mod.Id}' altered the schema or the migration history. ADR-0007 " +
                    "makes mods data-only so that SchemaVersions.Expected stays true for " +
                    "every template this build produces. Schema belongs to sql/migrations.");
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Everything a mod must leave alone: the shape of the database, and the migration
    /// history that <c>SchemaVersions</c> is checked against. A mod inserting a row into
    /// <c>schema_migrations</c> would misreport the template's version without running a
    /// single DDL statement, so both are fingerprinted together.
    /// </summary>
    private static string ReadStructuralFingerprint(
        SqliteConnection connection,
        SqliteTransaction? transaction)
    {
        var fingerprint = new StringBuilder();

        using (var schema = connection.CreateCommand())
        {
            schema.Transaction = transaction;
            schema.CommandText =
                "SELECT type, name, COALESCE(sql, '') FROM sqlite_master ORDER BY type, name;";
            using var reader = schema.ExecuteReader();
            while (reader.Read())
            {
                fingerprint
                    .Append(reader.GetString(0)).Append('\u001f')
                    .Append(reader.GetString(1)).Append('\u001f')
                    .Append(reader.GetString(2)).Append('\u001e');
            }
        }

        fingerprint.Append("schema_migrations\u001e");

        using (var migrations = connection.CreateCommand())
        {
            migrations.Transaction = transaction;
            migrations.CommandText = "SELECT version, name FROM schema_migrations ORDER BY version;";
            using var reader = migrations.ExecuteReader();
            while (reader.Read())
            {
                fingerprint
                    .Append(reader.GetInt32(0).ToString(CultureInfo.InvariantCulture)).Append('\u001f')
                    .Append(reader.GetString(1)).Append('\u001e');
            }
        }

        return fingerprint.ToString();
    }

    /// <summary>
    /// Two mods sharing an id make provenance ambiguous: SaveMetadata records the ordered
    /// (id, version) pairs, and a repeated id no longer says which one produced a row.
    /// </summary>
    private static void RequireDistinctIds(IReadOnlyList<ModPackage> mods)
    {
        var seen = new List<string>(mods.Count);
        foreach (var mod in mods)
        {
            if (seen.Contains(mod.Id, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Mod id '{mod.Id}' appears more than once in the mod list.");
            }
            seen.Add(mod.Id);
        }
    }

    private static void ApplyScript(SqliteConnection connection, string file)
    {
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, File.ReadAllText(file));
        transaction.Commit();
    }

    private static int ParseMigrationVersion(string file)
    {
        var name = Path.GetFileName(file);
        var prefix = name.Split('_', 2)[0];
        return int.Parse(prefix, CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
