using SoccerSim.Application;
using SoccerSim.Core.Competitions;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

/// <summary>
/// COMP-00 end to end: the shipped world carries a league season, advancing it plays a whole
/// round, and the round survives a checkpoint unchanged.
/// </summary>
public sealed class SeasonPersistenceTests : IDisposable
{
    private const int LeagueId = 2;
    private static readonly DateTimeOffset Start = new(2026, 2, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly string _workspace;
    private readonly string _template;
    private readonly CareerApplication _application = new(new SqliteCareerStore());

    public SeasonPersistenceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"soccer-season-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspace);
        _template = Path.Combine(_workspace, "world_template.db");

        var root = FindRepoRoot();
        new SqliteWorldTemplateBuilder().Build(
            Path.Combine(root, "sql", "migrations"),
            Path.Combine(root, "sql", "seeds"),
            _template);
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    private ActiveCareer StartCareer(string saveId) =>
        _application.CreateCareer(_template, _workspace, saveId, 4242UL, "0.0.1", "foundation-recife-1", Start);

    [Fact]
    public void The_shipped_world_starts_a_league_season_that_has_not_kicked_off()
    {
        var career = StartCareer("kickoff");
        var season = career.World.Season;

        Assert.NotNull(season);
        Assert.Equal(LeagueId, season.CompetitionId);
        Assert.Equal(Season.NotStarted, season.CurrentMatchday);
        Assert.Equal(new[] { 1, 2 }, season.ClubIds);
        Assert.Equal(new DateOnly(2026, 2, 7), season.StartDate);

        var league = career.World.Competitions.Single(c => c.Id == LeagueId);
        Assert.Equal("league", league.CompetitionType);
    }

    [Fact]
    public void Advancing_a_matchday_plays_its_fixtures_and_moves_the_season_on()
    {
        var career = StartCareer("advance");
        var expected = career.World.Season!.NextFixtures();

        var applied = _application.AdvanceMatchday(career);

        Assert.Equal(expected.Count, applied.Count);
        Assert.Equal(1, career.World.Season!.CurrentMatchday);
        Assert.All(applied, run => Assert.Equal(LeagueId, run.CompetitionId));
        Assert.All(applied, run => Assert.True(run.IsCompetitive));
        Assert.Equal(
            expected.Select(f => (f.Ordinal, f.HomeClubId, f.AwayClubId)),
            applied.Select(r => (r.FixtureOrdinal, r.HomeClubId, r.AwayClubId)));
    }

    [Fact]
    public void A_season_can_be_played_to_completion_and_then_refuses_to_go_further()
    {
        var career = StartCareer("complete");
        var total = career.World.Season!.TotalMatchdays;

        for (var matchday = 0; matchday < total; matchday++)
        {
            _application.AdvanceMatchday(career);
        }

        Assert.True(career.World.Season!.IsComplete);
        Assert.Equal(career.World.Season.Fixtures.Count, career.World.SimulationRuns.Count);
        Assert.Throws<InvalidOperationException>(() => _application.AdvanceMatchday(career));
    }

    [Fact]
    public void The_table_adds_up_to_the_results_behind_it()
    {
        var career = StartCareer("table");
        _application.AdvanceMatchday(career);
        _application.AdvanceMatchday(career);

        var table = _application.LeagueTableFor(career, LeagueId);
        var runs = career.World.SimulationRuns;

        Assert.Equal(2, table.Count);
        Assert.Equal(runs.Count * 2, table.Sum(row => row.Played));
        Assert.Equal(runs.Sum(r => r.HomeScore + r.AwayScore), table.Sum(row => row.GoalsFor));
        Assert.Equal(table.Sum(row => row.GoalsFor), table.Sum(row => row.GoalsAgainst));

        // Every match distributes either three points or two.
        var drawn = runs.Count(r => r.HomeScore == r.AwayScore);
        Assert.Equal(((runs.Count - drawn) * 3) + (drawn * 2), table.Sum(row => row.Points));
    }

    [Fact]
    public void A_matchday_replays_identically_from_the_career_seed()
    {
        // Two careers with the same seed, one advanced a round at a time and one in one go:
        // the results have to be identical, or a season could not be reproduced from its seed.
        var first = StartCareer("replay-a");
        var second = StartCareer("replay-b");

        var oneAtATime = _application.AdvanceMatchday(first).Concat(_application.AdvanceMatchday(first));

        _application.AdvanceMatchday(second);
        _application.AdvanceMatchday(second);

        Assert.Equal(oneAtATime, second.World.SimulationRuns);
        Assert.All(second.World.SimulationRuns, run => Assert.NotEqual(0UL, run.Digest));
    }

    [Fact]
    public void The_season_survives_a_checkpoint_and_a_reopen()
    {
        var career = StartCareer("persist");
        _application.AdvanceMatchday(career);
        _application.Save(career, Start.AddHours(2));

        var reopened = _application.OpenCareer(career.Save.DatabasePath);

        Assert.Equal(career.World.Season, reopened.World.Season);
        Assert.Equal(career.World.SimulationRuns, reopened.World.SimulationRuns);
        Assert.Equal(
            _application.LeagueTableFor(career, LeagueId),
            _application.LeagueTableFor(reopened, LeagueId));
    }

    [Fact]
    public void An_unsaved_matchday_is_lost_exactly_like_any_other_progress()
    {
        var career = StartCareer("discard");
        _application.AdvanceMatchday(career);
        Assert.True(career.World.HasUnsavedChanges);

        var reloaded = _application.DiscardAndReload(career);

        Assert.Empty(reloaded.World.SimulationRuns);
        Assert.Equal(Season.NotStarted, reloaded.World.Season!.CurrentMatchday);
    }

    [Fact]
    public void A_friendly_is_stored_without_a_competition_and_stays_out_of_the_table()
    {
        var career = StartCareer("friendly");
        var clubs = career.World.Clubs;

        _application.ApplyOutcomes(career, [_application.RunMatch(career, clubs[0].Id, clubs[1].Id, 11UL)]);
        _application.Save(career, Start.AddHours(1));

        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        var run = Assert.Single(reopened.World.SimulationRuns);

        Assert.Equal(0, run.CompetitionId);
        Assert.Equal(0, run.FixtureOrdinal);
        Assert.False(run.IsCompetitive);
        Assert.All(_application.LeagueTableFor(reopened, LeagueId), row => Assert.Equal(0, row.Played));
    }

    [Fact]
    public void The_schema_refuses_a_result_that_half_belongs_to_a_competition()
    {
        // competition_id and fixture_ordinal only mean anything together: a competitive result
        // settles a numbered fixture and a friendly settles none. Neither half alone is a
        // state the loader could make sense of.
        var career = StartCareer("half-competitive");

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => ExecuteOn(
            career.Save.DatabasePath,
            """
            INSERT INTO simulation_run
                (ordinal, competition_id, fixture_ordinal, home_club_id, away_club_id,
                 home_score, away_score, seed, simulation_version, ticks, digest)
            VALUES (1, 2, 0, 1, 2, 0, 0, '1', 3, 100, '0');
            """));

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => ExecuteOn(
            career.Save.DatabasePath,
            """
            INSERT INTO simulation_run
                (ordinal, competition_id, fixture_ordinal, home_club_id, away_club_id,
                 home_score, away_score, seed, simulation_version, ticks, digest)
            VALUES (2, NULL, 4, 1, 2, 0, 0, '1', 3, 100, '0');
            """));
    }

    [Fact]
    public void The_schema_refuses_a_competition_the_code_cannot_schedule()
    {
        var career = StartCareer("unschedulable");

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => ExecuteOn(
            career.Save.DatabasePath,
            "INSERT INTO competition (id, country_id, name, competition_type) " +
            "VALUES (9, 1, 'Copa Fantasma', 'cup');"));
    }

    [Fact]
    public void The_schema_refuses_a_second_season()
    {
        // WorldState models one season in progress. A second row would make the loader pick.
        var career = StartCareer("two-seasons");

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => ExecuteOn(
            career.Save.DatabasePath,
            "INSERT INTO season (id, competition_id, start_date, current_matchday) " +
            "VALUES (2, 2, '2027-02-06', 0);"));
    }

    private static void ExecuteOn(string databasePath, string sql)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source={databasePath};Mode=ReadWrite;Pooling=False");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
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
        throw new DirectoryNotFoundException("Could not locate SoccerDreamGame.sln.");
    }
}
