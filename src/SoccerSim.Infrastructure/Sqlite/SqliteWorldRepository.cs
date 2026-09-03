using System.Globalization;
using Microsoft.Data.Sqlite;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Persistence;

namespace SoccerSim.Infrastructure.Sqlite;

public sealed class SqliteWorldRepository : IWorldRepository
{
    public WorldState Load(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        using var connection = Open(databasePath);
        return new WorldState(
            LoadCountries(connection),
            LoadCities(connection),
            LoadStadiums(connection),
            LoadClubs(connection),
            LoadPlayers(connection),
            LoadCompetitions(connection),
            LoadSimulationRuns(connection));
    }

    private static IReadOnlyList<AppliedSimulationRun> LoadSimulationRuns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ordinal, home_club_id, away_club_id, home_score, away_score,
                   seed, simulation_version, ticks, digest
            FROM simulation_run
            ORDER BY ordinal;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<AppliedSimulationRun>();
        while (reader.Read())
        {
            rows.Add(new AppliedSimulationRun(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                ulong.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                reader.GetInt32(6),
                reader.GetInt32(7),
                ulong.Parse(reader.GetString(8), CultureInfo.InvariantCulture)));
        }
        return rows;
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

    private static IReadOnlyList<Country> LoadCountries(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, code, name FROM country ORDER BY id;";
        using var reader = command.ExecuteReader();
        var rows = new List<Country>();
        while (reader.Read())
        {
            rows.Add(new Country(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        return rows;
    }

    private static IReadOnlyList<City> LoadCities(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, country_id, name FROM city ORDER BY id;";
        using var reader = command.ExecuteReader();
        var rows = new List<City>();
        while (reader.Read())
        {
            rows.Add(new City(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2)));
        }
        return rows;
    }

    private static IReadOnlyList<Stadium> LoadStadiums(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, city_id, name, capacity FROM stadium ORDER BY id;";
        using var reader = command.ExecuteReader();
        var rows = new List<Stadium>();
        while (reader.Read())
        {
            rows.Add(new Stadium(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3)));
        }
        return rows;
    }

    private static IReadOnlyList<Club> LoadClubs(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, city_id, stadium_id, name, short_name FROM club ORDER BY id;";
        using var reader = command.ExecuteReader();
        var rows = new List<Club>();
        while (reader.Read())
        {
            rows.Add(new Club(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4)));
        }
        return rows;
    }

    private static IReadOnlyList<Player> LoadPlayers(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.nationality_country_id, p.club_id, p.first_name, p.last_name,
                   p.birth_date, p.squad_number, p.position_code, p.pace, p.stamina, p.strength,
                   p.passing, p.shooting, p.tackling, p.dribbling, p.positioning, p.goalkeeping,
                   pn.given_name, pn.additional_given_name, pn.family_name,
                   pn.additional_family_name, pn.full_name, pn.common_name, pn.shirt_name,
                   pn.scoreboard_name, pn.culture_id, pn.pack_version, pn.algorithm_version
            FROM player AS p
            LEFT JOIN player_name AS pn ON pn.player_id = p.id
            ORDER BY p.id;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<Player>();
        while (reader.Read())
        {
            var playerId = reader.GetInt32(0);
            if (reader.IsDBNull(17))
            {
                throw new InvalidDataException(
                    $"Player {playerId} has no persisted player_name row; names are never regenerated on load.");
            }

            var firstName = reader.GetString(3);
            var lastName = reader.GetString(4);
            var name = new PlayerName(
                reader.GetString(17),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.GetString(19),
                reader.IsDBNull(20) ? null : reader.GetString(20),
                reader.GetString(21),
                reader.GetString(22),
                reader.GetString(23),
                reader.GetString(24),
                reader.GetString(25),
                reader.GetString(26),
                reader.GetString(27));
            if (!string.Equals(firstName, name.GivenName, StringComparison.Ordinal)
                || !string.Equals(lastName, name.FamilyName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Player {playerId} has divergent legacy and canonical name components.");
            }

            rows.Add(new Player(
                playerId,
                reader.GetInt32(1),
                reader.GetInt32(2),
                firstName,
                lastName,
                DateOnly.ParseExact(reader.GetString(5), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetInt32(6),
                PlayerPositions.Parse(reader.GetString(7)),
                new PlayerAttributes(
                    reader.GetInt32(8),
                    reader.GetInt32(9),
                    reader.GetInt32(10),
                    reader.GetInt32(11),
                    reader.GetInt32(12),
                    reader.GetInt32(13),
                    reader.GetInt32(14),
                    reader.GetInt32(15),
                    reader.GetInt32(16)),
                name));
        }
        return rows;
    }

    private static IReadOnlyList<Competition> LoadCompetitions(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, country_id, name, competition_type FROM competition ORDER BY id;";
        using var reader = command.ExecuteReader();
        var rows = new List<Competition>();
        while (reader.Read())
        {
            rows.Add(new Competition(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));
        }
        return rows;
    }
}
