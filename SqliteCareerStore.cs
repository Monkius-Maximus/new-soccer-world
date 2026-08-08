using System.Globalization;
using Microsoft.Data.Sqlite;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Persistence;

namespace SoccerSim.Infrastructure.Sqlite;

public sealed class SqliteCareerStore : ICareerStore
{
    private readonly IWorldRepository _worldRepository;

    public SqliteCareerStore()
        : this(new SqliteWorldRepository())
    {
    }

    public SqliteCareerStore(IWorldRepository worldRepository)
    {
        _worldRepository = worldRepository;
    }

    public CareerSave CreateCareer(
        string templateDatabasePath,
        string savesRoot,
        string saveId,
        ulong careerSeed,
        string gameVersion,
        string contentVersion,
        DateTimeOffset timestamp)
    {
        ValidateSaveId(saveId);
        if (!File.Exists(templateDatabasePath))
        {
            throw new FileNotFoundException("World template database was not found.", templateDatabasePath);
        }

        var saveDirectory = Path.Combine(savesRoot, saveId);
        Directory.CreateDirectory(saveDirectory);
        var databasePath = Path.Combine(saveDirectory, "world.db");
        if (File.Exists(databasePath))
        {
            throw new IOException($"Save '{saveId}' already exists at '{databasePath}'.");
        }

        File.Copy(templateDatabasePath, databasePath, overwrite: false);

        var schemaVersion = ReadSchemaVersion(databasePath);
        var metadata = new SaveMetadata(
            saveId,
            gameVersion,
            schemaVersion,
            contentVersion,
            timestamp,
            timestamp,
            careerSeed);

        WriteMetadata(databasePath, metadata, insert: true);
        return new CareerSave(databasePath, metadata);
    }

    public CareerSave OpenCareer(string databasePath) => new(databasePath, ReadMetadata(databasePath));

    public WorldState LoadWorld(string databasePath) => _worldRepository.Load(databasePath);

    public CareerSave Checkpoint(
        CareerSave save,
        WorldState world,
        CheckpointKind kind,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(world);
        _ = kind; // Foundation records only metadata; entity persistence arrives with mutable career systems.

        var updated = save.Metadata with { LastPlayedAt = timestamp };
        WriteMetadata(save.DatabasePath, updated, insert: false);
        return save with { Metadata = updated };
    }

    private static void ValidateSaveId(string saveId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveId);
        if (saveId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || saveId.Contains('/') || saveId.Contains('\\'))
        {
            throw new ArgumentException("SaveId must be a safe single directory name.", nameof(saveId));
        }
    }

    private static int ReadSchemaVersion(string databasePath)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static SaveMetadata ReadMetadata(string databasePath)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT save_id, game_version, schema_version, content_version, created_at, last_played_at, career_seed
            FROM save_metadata
            LIMIT 1;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("Career database has no SaveMetadata row.");
        }

        return new SaveMetadata(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
            ulong.Parse(reader.GetString(6), CultureInfo.InvariantCulture));
    }

    private static void WriteMetadata(string databasePath, SaveMetadata metadata, bool insert)
    {
        using var connection = Open(databasePath);
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = insert
            ? """
                INSERT INTO save_metadata
                    (save_id, game_version, schema_version, content_version, created_at, last_played_at, career_seed)
                VALUES
                    ($saveId, $gameVersion, $schemaVersion, $contentVersion, $createdAt, $lastPlayedAt, $careerSeed);
                """
            : """
                UPDATE save_metadata
                SET game_version = $gameVersion,
                    schema_version = $schemaVersion,
                    content_version = $contentVersion,
                    last_played_at = $lastPlayedAt,
                    career_seed = $careerSeed
                WHERE save_id = $saveId;
                """;
        command.Parameters.AddWithValue("$saveId", metadata.SaveId);
        command.Parameters.AddWithValue("$gameVersion", metadata.GameVersion);
        command.Parameters.AddWithValue("$schemaVersion", metadata.SchemaVersion);
        command.Parameters.AddWithValue("$contentVersion", metadata.ContentVersion);
        command.Parameters.AddWithValue("$createdAt", metadata.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$lastPlayedAt", metadata.LastPlayedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$careerSeed", metadata.CareerSeed.ToString(CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static SqliteConnection Open(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadWrite");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
