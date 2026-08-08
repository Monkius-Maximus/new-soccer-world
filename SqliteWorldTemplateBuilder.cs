using Microsoft.Data.Sqlite;

namespace SoccerSim.Infrastructure.Sqlite;

public sealed class SqliteWorldTemplateBuilder
{
    public void Build(string migrationsDirectory, string seedsDirectory, string outputDatabasePath)
    {
        if (!Directory.Exists(migrationsDirectory))
        {
            throw new DirectoryNotFoundException(migrationsDirectory);
        }
        if (!Directory.Exists(seedsDirectory))
        {
            throw new DirectoryNotFoundException(seedsDirectory);
        }

        var fullOutputPath = Path.GetFullPath(outputDatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        if (File.Exists(fullOutputPath))
        {
            File.Delete(fullOutputPath);
        }

        using var connection = new SqliteConnection($"Data Source={fullOutputPath};Mode=ReadWriteCreate");
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
    }

    private static void ApplyScript(SqliteConnection connection, string file)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = File.ReadAllText(file);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static int ParseMigrationVersion(string file)
    {
        var name = Path.GetFileName(file);
        var prefix = name.Split('_', 2)[0];
        return int.Parse(prefix, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
