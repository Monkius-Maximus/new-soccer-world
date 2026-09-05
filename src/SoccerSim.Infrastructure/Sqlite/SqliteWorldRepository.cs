using System.Globalization;
using Microsoft.Data.Sqlite;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Persistence;
using SoccerSim.Core.Tactics;

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
            LoadSimulationRuns(connection),
            LoadClubTactics(connection));
    }

    private static IReadOnlyList<ClubTacticSetup> LoadClubTactics(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT club_id, formation, defensive_line_height, pressing_intensity, directness
            FROM club_tactics
            ORDER BY club_id;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<ClubTacticSetup>();
        while (reader.Read())
        {
            rows.Add(new ClubTacticSetup(
                reader.GetInt32(0),
                new TeamTactics(
                    Formations.Parse(reader.GetString(1)),
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4))));
        }
        return rows;
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
            SELECT id, nationality_country_id, club_id, first_name, last_name, birth_date, squad_number,
                   position_code, pace, stamina, strength, passing, shooting, tackling, dribbling,
                   positioning, goalkeeping
            FROM player
            ORDER BY id;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<Player>();
        while (reader.Read())
        {
            rows.Add(new Player(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
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
                    reader.GetInt32(16))));
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
