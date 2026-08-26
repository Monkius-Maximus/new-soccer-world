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
        if (schemaVersion != SchemaVersions.Expected)
        {
            File.Delete(databasePath);
            throw new InvalidDataException(
                $"World template is at schema version {schemaVersion}, but this build expects " +
                $"{SchemaVersions.Expected}. Rebuild it with SoccerSim.WorldBuilder.");
        }

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

    public CareerSave OpenCareer(string databasePath)
    {
        var metadata = ReadMetadata(databasePath);

        // No migrate-on-open path exists yet, so a mismatch is refused rather than guessed at.
        // Half-reading a career whose schema moved underneath it is how saves get corrupted.
        if (metadata.SchemaVersion != SchemaVersions.Expected)
        {
            throw new InvalidDataException(
                $"Career '{metadata.SaveId}' was written against schema version {metadata.SchemaVersion}, " +
                $"but this build reads version {SchemaVersions.Expected}. " +
                "Upgrade migrations for existing saves do not exist yet.");
        }

        return new CareerSave(databasePath, metadata);
    }

    public WorldState LoadWorld(string databasePath) => _worldRepository.Load(databasePath);

    public CareerSave Checkpoint(
        CareerSave save,
        WorldState world,
        CheckpointKind kind,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(world);
        _ = kind; // Every kind commits identically today; the distinction is for the caller's UX.

        var updated = save.Metadata with { LastPlayedAt = timestamp };

        // One connection, one transaction: progress and the metadata that describes it either
        // both land or neither does. A crash mid-checkpoint leaves the last good save intact.
        using var connection = Open(save.DatabasePath);
        using var transaction = connection.BeginTransaction();

        WriteSimulationRuns(connection, transaction, world.SimulationRuns);
        WriteMetadata(connection, transaction, updated, insert: false);

        transaction.Commit();
        world.MarkPersisted();

        return save with { Metadata = updated };
    }

    private static void WriteSimulationRuns(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<AppliedSimulationRun> runs)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO simulation_run
                (ordinal, home_club_id, away_club_id, home_score, away_score,
                 seed, simulation_version, ticks, digest)
            VALUES
                ($ordinal, $homeClubId, $awayClubId, $homeScore, $awayScore,
                 $seed, $simulationVersion, $ticks, $digest)
            ON CONFLICT(ordinal) DO UPDATE SET
                home_club_id = excluded.home_club_id,
                away_club_id = excluded.away_club_id,
                home_score = excluded.home_score,
                away_score = excluded.away_score,
                seed = excluded.seed,
                simulation_version = excluded.simulation_version,
                ticks = excluded.ticks,
                digest = excluded.digest;
            """;

        var ordinal = command.Parameters.Add("$ordinal", SqliteType.Integer);
        var homeClubId = command.Parameters.Add("$homeClubId", SqliteType.Integer);
        var awayClubId = command.Parameters.Add("$awayClubId", SqliteType.Integer);
        var homeScore = command.Parameters.Add("$homeScore", SqliteType.Integer);
        var awayScore = command.Parameters.Add("$awayScore", SqliteType.Integer);
        var seed = command.Parameters.Add("$seed", SqliteType.Text);
        var simulationVersion = command.Parameters.Add("$simulationVersion", SqliteType.Integer);
        var ticks = command.Parameters.Add("$ticks", SqliteType.Integer);
        var digest = command.Parameters.Add("$digest", SqliteType.Text);

        // Written in career apply order so the file mirrors the in-memory sequence.
        foreach (var run in runs)
        {
            ordinal.Value = run.Ordinal;
            homeClubId.Value = run.HomeClubId;
            awayClubId.Value = run.AwayClubId;
            homeScore.Value = run.HomeScore;
            awayScore.Value = run.AwayScore;
            seed.Value = run.Seed.ToString(CultureInfo.InvariantCulture);
            simulationVersion.Value = run.SimulationVersion;
            ticks.Value = run.Ticks;
            digest.Value = run.Digest.ToString(CultureInfo.InvariantCulture);
            command.ExecuteNonQuery();
        }
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
        WriteMetadata(connection, transaction, metadata, insert);
        transaction.Commit();
    }

    private static void WriteMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SaveMetadata metadata,
        bool insert)
    {
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
    }

    private static SqliteConnection Open(string databasePath)
    {
        var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadWrite;Pooling=False");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
