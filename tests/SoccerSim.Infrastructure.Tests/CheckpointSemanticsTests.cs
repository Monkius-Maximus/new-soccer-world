using SoccerSim.Application;
using SoccerSim.Core.Persistence;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerSim.Infrastructure.Tests;

/// <summary>
/// The save contract: WorldState is the authority during a session, the database only catches
/// up at explicit checkpoints, and abandoning a session loses exactly the work applied since
/// the last one.
/// </summary>
public sealed class CheckpointSemanticsTests : IDisposable
{
    private const int Ticks = 512;
    private static readonly DateTimeOffset Start = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly string _workspace;
    private readonly string _template;
    private readonly CareerApplication _application = new(new SqliteCareerStore());

    public CheckpointSemanticsTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"soccer-ckpt-{Guid.NewGuid():N}");
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

    private ActiveCareer StartCareer(string saveId) =>
        _application.CreateCareer(_template, _workspace, saveId, 7UL, "0.0.1", Start);

    private IReadOnlyList<Core.Domain.AppliedSimulationRun> PlayAndApply(ActiveCareer career, ulong seed)
    {
        var clubs = career.World.Clubs;
        var outcome = _application.RunMatch(career, clubs[0].Id, clubs[1].Id, seed);
        return _application.ApplyOutcomes(career, [outcome]);
    }

    [Fact]
    public void A_new_career_starts_clean_and_empty()
    {
        var career = StartCareer("clean");

        Assert.Empty(career.World.SimulationRuns);
        Assert.False(career.World.HasUnsavedChanges);
    }

    [Fact]
    public void Applying_an_outcome_dirties_the_world_without_touching_disk()
    {
        var career = StartCareer("dirty");
        PlayAndApply(career, 11UL);

        Assert.Single(career.World.SimulationRuns);
        Assert.True(career.World.HasUnsavedChanges);

        // The session holds the change; the file does not know about it yet.
        var onDisk = _application.OpenCareer(career.Save.DatabasePath);
        Assert.Empty(onDisk.World.SimulationRuns);
    }

    [Fact]
    public void Checkpoint_commits_progress_and_clears_the_dirty_flag()
    {
        var career = StartCareer("commit");
        PlayAndApply(career, 11UL);

        _application.Save(career, Start.AddMinutes(5));

        Assert.False(career.World.HasUnsavedChanges);

        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        Assert.Single(reopened.World.SimulationRuns);
        Assert.Equal(career.World.SimulationRuns[0], reopened.World.SimulationRuns[0]);
        Assert.Equal(Start.AddMinutes(5), reopened.Save.Metadata.LastPlayedAt);
    }

    [Fact]
    public void Quitting_without_saving_discards_everything_after_the_last_checkpoint()
    {
        var career = StartCareer("discard");

        PlayAndApply(career, 11UL);
        _application.Save(career, Start.AddMinutes(5));

        // Work done after the checkpoint, then abandoned.
        PlayAndApply(career, 22UL);
        PlayAndApply(career, 33UL);
        Assert.Equal(3, career.World.SimulationRuns.Count);
        Assert.True(career.World.HasUnsavedChanges);

        var reloaded = _application.DiscardAndReload(career);

        Assert.Single(reloaded.World.SimulationRuns);
        Assert.Equal(11UL, reloaded.World.SimulationRuns[0].Seed);
        Assert.False(reloaded.World.HasUnsavedChanges);
    }

    [Fact]
    public void Autosave_writes_only_when_there_is_something_to_write()
    {
        var career = StartCareer("autosave");

        Assert.False(_application.Autosave(career, Start.AddMinutes(1)));
        Assert.Equal(Start, _application.OpenCareer(career.Save.DatabasePath).Save.Metadata.LastPlayedAt);

        PlayAndApply(career, 11UL);

        Assert.True(_application.Autosave(career, Start.AddMinutes(2)));
        Assert.False(_application.Autosave(career, Start.AddMinutes(3)));

        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        Assert.Single(reopened.World.SimulationRuns);
        Assert.Equal(Start.AddMinutes(2), reopened.Save.Metadata.LastPlayedAt);
    }

    [Fact]
    public void Save_and_exit_commits_before_handing_the_career_back()
    {
        var career = StartCareer("save-and-exit");
        PlayAndApply(career, 44UL);

        var closed = _application.SaveAndExit(career, Start.AddMinutes(9));

        Assert.False(career.World.HasUnsavedChanges);
        Assert.Equal(Start.AddMinutes(9), closed.Metadata.LastPlayedAt);

        var reopened = _application.OpenCareer(closed.DatabasePath);
        Assert.Single(reopened.World.SimulationRuns);
    }

    [Fact]
    public void Repeated_checkpoints_stay_idempotent_rather_than_duplicating_progress()
    {
        var career = StartCareer("idempotent");
        PlayAndApply(career, 11UL);

        _application.Save(career, Start.AddMinutes(5));
        _application.Save(career, Start.AddMinutes(6));
        PlayAndApply(career, 22UL);
        _application.Save(career, Start.AddMinutes(7));

        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        Assert.Equal(2, reopened.World.SimulationRuns.Count);
        Assert.Equal([1, 2], reopened.World.SimulationRuns.Select(run => run.Ordinal));
    }

    [Fact]
    public void A_resumed_career_keeps_numbering_where_it_left_off()
    {
        var career = StartCareer("resume");
        PlayAndApply(career, 11UL);
        _application.Save(career, Start.AddMinutes(5));

        var resumed = _application.OpenCareer(career.Save.DatabasePath);
        PlayAndApply(resumed, 22UL);
        _application.Save(resumed, Start.AddMinutes(10));

        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        Assert.Equal([1, 2], reopened.World.SimulationRuns.Select(run => run.Ordinal));
        Assert.Equal([11UL, 22UL], reopened.World.SimulationRuns.Select(run => run.Seed));
    }

    [Fact]
    public void Outcomes_are_applied_in_a_deterministic_order_regardless_of_arrival_order()
    {
        var first = StartCareer("order-a");
        var second = StartCareer("order-b");

        var clubs = first.World.Clubs;
        MatchOutcome[] outcomes =
        [
            _application.RunMatch(first, clubs[1].Id, clubs[0].Id, 30UL),
            _application.RunMatch(first, clubs[0].Id, clubs[1].Id, 20UL),
            _application.RunMatch(first, clubs[0].Id, clubs[1].Id, 10UL)
        ];

        _application.ApplyOutcomes(first, outcomes);
        _application.ApplyOutcomes(second, outcomes.Reverse());

        Assert.Equal(
            first.World.SimulationRuns.Select(run => (run.Ordinal, run.HomeClubId, run.Seed)),
            second.World.SimulationRuns.Select(run => (run.Ordinal, run.HomeClubId, run.Seed)));
    }

    [Fact]
    public void Persisted_progress_survives_a_checkpoint_byte_for_byte()
    {
        var career = StartCareer("fidelity");
        var applied = PlayAndApply(career, 123456789UL);
        _application.Save(career, Start.AddMinutes(5));

        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        var restored = reopened.World.SimulationRuns[0];

        // Digest and seed are ulong; they round-trip through TEXT without losing the high bit.
        Assert.Equal(applied[0].Digest, restored.Digest);
        Assert.Equal(applied[0].Seed, restored.Seed);
        Assert.Equal(SimulationSettings.SimulationVersion, restored.SimulationVersion);
        Assert.Equal(applied[0].Ticks, restored.Ticks);
    }

    [Fact]
    public void A_failed_checkpoint_leaves_the_previous_save_intact()
    {
        var career = StartCareer("rollback");
        PlayAndApply(career, 11UL);
        _application.Save(career, Start.AddMinutes(5));

        // Apply a run that violates the schema's club foreign key, so the commit must fail.
        career.World.ApplySimulationRun(9999, 8888, 1, 0, 77UL, SimulationSettings.SimulationVersion, Ticks, 5UL);

        Assert.ThrowsAny<Exception>(() => _application.Save(career, Start.AddMinutes(6)));

        // The transaction rolled back: the good first run is still there and nothing partial landed.
        var reopened = _application.OpenCareer(career.Save.DatabasePath);
        Assert.Single(reopened.World.SimulationRuns);
        Assert.Equal(11UL, reopened.World.SimulationRuns[0].Seed);
        Assert.Equal(Start.AddMinutes(5), reopened.Save.Metadata.LastPlayedAt);
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
