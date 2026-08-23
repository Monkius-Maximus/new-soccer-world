using SoccerSim.Core.Domain;
using SoccerSim.Core.Persistence;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Application;

public sealed class CareerApplication
{
    private readonly ICareerStore _careerStore;

    public CareerApplication(ICareerStore careerStore)
    {
        _careerStore = careerStore;
    }

    public ActiveCareer CreateCareer(
        string templateDatabasePath,
        string savesRoot,
        string saveId,
        ulong careerSeed,
        string gameVersion,
        string contentVersion,
        DateTimeOffset timestamp)
    {
        var save = _careerStore.CreateCareer(
            templateDatabasePath,
            savesRoot,
            saveId,
            careerSeed,
            gameVersion,
            contentVersion,
            timestamp);
        var world = _careerStore.LoadWorld(save.DatabasePath);
        return new ActiveCareer(save, world);
    }

    public ActiveCareer OpenCareer(string databasePath)
    {
        var save = _careerStore.OpenCareer(databasePath);
        var world = _careerStore.LoadWorld(databasePath);
        return new ActiveCareer(save, world);
    }

    public void Checkpoint(ActiveCareer career, CheckpointKind kind, DateTimeOffset timestamp)
    {
        var updated = _careerStore.Checkpoint(career.Save, career.World, kind, timestamp);
        career.ReplaceSaveMetadata(updated);
    }

    /// <summary>Manual save.</summary>
    public void Save(ActiveCareer career, DateTimeOffset timestamp) =>
        Checkpoint(career, CheckpointKind.Manual, timestamp);

    /// <summary>
    /// Autosave. Skips the write when nothing has been applied since the last checkpoint, so a
    /// periodic timer does not keep rewriting an unchanged career.
    /// </summary>
    /// <returns>True when a checkpoint was actually committed.</returns>
    public bool Autosave(ActiveCareer career, DateTimeOffset timestamp)
    {
        if (!career.World.HasUnsavedChanges)
        {
            return false;
        }

        Checkpoint(career, CheckpointKind.Autosave, timestamp);
        return true;
    }

    /// <summary>
    /// Save &amp; Exit: commit, then hand back the closed career metadata. The caller is expected
    /// to drop its <see cref="ActiveCareer"/> afterwards.
    /// </summary>
    public CareerSave SaveAndExit(ActiveCareer career, DateTimeOffset timestamp)
    {
        Checkpoint(career, CheckpointKind.SaveAndExit, timestamp);
        return career.Save;
    }

    /// <summary>
    /// Exit without saving. Nothing is written, so everything applied since the last checkpoint
    /// is gone; reopening the career yields the last committed state.
    /// </summary>
    public ActiveCareer DiscardAndReload(ActiveCareer career) => OpenCareer(career.Save.DatabasePath);

    /// <summary>
    /// Runs a match against an isolated snapshot and returns its outcome. Nothing is applied to
    /// the career here — see <see cref="ApplyOutcomes"/>.
    /// </summary>
    public MatchOutcome RunMatch(
        ActiveCareer career,
        int homeClubId,
        int awayClubId,
        ulong seed,
        int ticks)
    {
        var context = new MatchContext(
            homeClubId,
            awayClubId,
            seed,
            SimulationSettings.SimulationVersion,
            career.World.Snapshot());

        return new MatchOutcome(homeClubId, awayClubId, DeterministicSimulationProbe.Run(context, ticks));
    }

    /// <summary>
    /// Applies finished outcomes to the career world in a deterministic order.
    /// <para>
    /// Matches may be produced in any order — and, once matches run in parallel, in a
    /// nondeterministic one — so the apply phase sorts them itself rather than trusting arrival
    /// order. Sorting by (home, away, seed) is total for a round: the same set of outcomes
    /// always lands in the same sequence and therefore produces the same ordinals.
    /// </para>
    /// </summary>
    public IReadOnlyList<AppliedSimulationRun> ApplyOutcomes(
        ActiveCareer career,
        IEnumerable<MatchOutcome> outcomes)
    {
        var ordered = outcomes
            .OrderBy(outcome => outcome.HomeClubId)
            .ThenBy(outcome => outcome.AwayClubId)
            .ThenBy(outcome => outcome.Result.Seed)
            .ToArray();

        var applied = new List<AppliedSimulationRun>(ordered.Length);
        foreach (var outcome in ordered)
        {
            applied.Add(career.World.ApplySimulationRun(
                outcome.HomeClubId,
                outcome.AwayClubId,
                outcome.Result.Seed,
                outcome.Result.SimulationVersion,
                outcome.Result.Ticks,
                outcome.Result.Digest));
        }

        return applied;
    }

    public SimulationProbeResult RunFoundationSimulationProbe(
        ActiveCareer career,
        int homeClubId,
        int awayClubId,
        ulong seed,
        int ticks)
    {
        // The match gets an isolated snapshot. It never mutates the active career directly.
        var context = new MatchContext(
            homeClubId,
            awayClubId,
            seed,
            SimulationSettings.SimulationVersion,
            career.World.Snapshot());

        return DeterministicSimulationProbe.Run(context, ticks);
    }
}
