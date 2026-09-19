using SoccerSim.Application;
using SoccerSim.Core.Persistence;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

/// <summary>
/// The headless deterministic slice as a test-suite member, so CI gates on it rather than
/// only on the console runner. Walks the full foundation path:
/// migrations → seeds → world_template → career save → repository → WorldState →
/// Application → deterministic simulation loop.
/// </summary>
public sealed class HeadlessVerticalSliceTests : IDisposable
{
    private const int Ticks = 4096;

    private readonly string _workspace;
    private readonly string _template;

    public HeadlessVerticalSliceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"soccer-slice-{Guid.NewGuid():N}");
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

    private ActiveCareer StartCareer(CareerApplication application, string saveId) =>
        application.CreateCareer(
            _template,
            _workspace,
            saveId,
            careerSeed: 424242UL,
            gameVersion: "0.0.1-foundation",
            timestamp: new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Full_path_loads_the_seeded_world_into_memory()
    {
        var application = new CareerApplication(new SqliteCareerStore());
        var career = StartCareer(application, "slice-load");

        var rosters = new ClubRosterQueryService().GetClubRosters(career.World);
        Assert.Equal(2, rosters.Count);
        Assert.Equal(30, rosters.Sum(club => club.Players.Count));
        Assert.Equal(["Recife Azul", "Recife Vermelho"], rosters.Select(club => club.ClubName));
    }

    [Fact]
    public void Same_seed_reproduces_exactly_across_independent_careers()
    {
        var application = new CareerApplication(new SqliteCareerStore());
        var first = RunProbe(application, "slice-seed-a", seed: 123456789UL);
        var second = RunProbe(application, "slice-seed-b", seed: 123456789UL);

        // Two careers, two WorldState instances, two IRandomSource instances, one result.
        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seed_produces_a_different_result()
    {
        var application = new CareerApplication(new SqliteCareerStore());
        var first = RunProbe(application, "slice-seed-c", seed: 123456789UL);
        var other = RunProbe(application, "slice-seed-d", seed: 123456790UL);

        Assert.NotEqual(first.Digest, other.Digest);
        Assert.NotEqual(first.FinalRandomState, other.FinalRandomState);
    }

    [Fact]
    public void Result_records_the_simulation_version_needed_to_reproduce_it()
    {
        var application = new CareerApplication(new SqliteCareerStore());
        var result = RunProbe(application, "slice-version", seed: 5UL);

        Assert.Equal(SimulationSettings.SimulationVersion, result.SimulationVersion);
        Assert.True(result.Ticks > 0);
        Assert.Equal(5UL, result.Seed);
    }

    [Fact]
    public void Checkpoint_is_the_only_thing_that_advances_persisted_state()
    {
        var application = new CareerApplication(new SqliteCareerStore());
        var career = StartCareer(application, "slice-checkpoint");
        var createdAt = career.Save.Metadata.LastPlayedAt;

        application.RunMatch(career, career.World.Clubs[0].Id, career.World.Clubs[1].Id, 1UL);

        // Simulating did not touch the database; only an explicit checkpoint does.
        var beforeCheckpoint = new SqliteCareerStore().OpenCareer(career.Save.DatabasePath);
        Assert.Equal(createdAt, beforeCheckpoint.Metadata.LastPlayedAt);

        var checkpointAt = createdAt.AddMinutes(10);
        application.Checkpoint(career, CheckpointKind.SaveAndExit, checkpointAt);

        var afterCheckpoint = new SqliteCareerStore().OpenCareer(career.Save.DatabasePath);
        Assert.Equal(checkpointAt, afterCheckpoint.Metadata.LastPlayedAt);
    }

    private MatchResult RunProbe(CareerApplication application, string saveId, ulong seed)
    {
        var career = StartCareer(application, saveId);
        var clubs = career.World.Clubs;
        return application.RunMatch(career, clubs[0].Id, clubs[1].Id, seed).Result;
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
