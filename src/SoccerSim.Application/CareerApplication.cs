using SoccerSim.Core.Competitions;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;
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
    public MatchOutcome RunMatch(ActiveCareer career, int homeClubId, int awayClubId, ulong seed)
    {
        var context = new MatchContext(
            homeClubId,
            awayClubId,
            seed,
            SimulationSettings.SimulationVersion,
            career.World.Snapshot());

        return new MatchOutcome(homeClubId, awayClubId, MatchSimulation.Run(context));
    }

    /// <summary>
    /// Starts a match and hands back the running simulation so a caller can advance it one
    /// fixed timestep at a time.
    /// <para>
    /// This is what the visual match view uses: it draws each tick rather than replaying a
    /// recorded one, so there is only ever one set of football rules. The returned simulation
    /// still owns its own state and randomness, and applying its result goes through
    /// <see cref="ApplyOutcomes"/> exactly as a batch match does.
    /// </para>
    /// </summary>
    public MatchSimulation StartMatch(ActiveCareer career, int homeClubId, int awayClubId, ulong seed) =>
        MatchSimulation.Start(new MatchContext(
            homeClubId,
            awayClubId,
            seed,
            SimulationSettings.SimulationVersion,
            career.World.Snapshot()));

    /// <summary>
    /// Plays every fixture of the next matchday and applies the results, then moves the season
    /// on. The round is advanced only after all of its results have landed, so an interrupted
    /// call can be retried without half a matchday going missing.
    /// </summary>
    /// <returns>The results that were applied, in the order they landed.</returns>
    public IReadOnlyList<AppliedSimulationRun> AdvanceMatchday(ActiveCareer career)
    {
        var season = career.World.Season
            ?? throw new InvalidOperationException("The career has no season in progress.");

        if (season.IsComplete)
        {
            throw new InvalidOperationException("The season is already complete.");
        }

        var fixtures = season.NextFixtures();
        var careerSeed = career.Save.Metadata.CareerSeed;

        // Each fixture gets a seed derived from the career, not from play order, so the round
        // is identical however it is reached.
        var outcomes = fixtures
            .Select(fixture => new MatchOutcome(
                fixture.HomeClubId,
                fixture.AwayClubId,
                MatchSimulation.Run(new MatchContext(
                    fixture.HomeClubId,
                    fixture.AwayClubId,
                    MatchSeeds.For(careerSeed, fixture.CompetitionId, fixture.Ordinal),
                    SimulationSettings.SimulationVersion,
                    career.World.Snapshot())),
                fixture.CompetitionId,
                fixture.Ordinal))
            .ToArray();

        var applied = ApplyOutcomes(career, outcomes);
        career.World.AdvanceMatchday();
        return applied;
    }

    /// <summary>The standings for the career's competition, recomputed from applied results.</summary>
    public IReadOnlyList<LeagueRow> LeagueTableFor(ActiveCareer career, int competitionId) =>
        LeagueTable.Build(
            [.. career.World.Clubs.Select(club => club.Id)],
            career.World.SimulationRuns,
            competitionId);

    /// <summary>Wraps a finished simulation as an outcome ready for <see cref="ApplyOutcomes"/>.</summary>
    public static MatchOutcome OutcomeOf(MatchSimulation simulation)
    {
        var result = simulation.Result
            ?? throw new InvalidOperationException("The match has not finished yet.");

        return new MatchOutcome(result.HomeClubId, result.AwayClubId, result);
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
        // Fixture order first: within a competition it is the natural sequence, and it stays
        // total for friendlies, which carry ordinal zero and fall back to the club/seed keys.
        var ordered = outcomes
            .OrderBy(outcome => outcome.CompetitionId)
            .ThenBy(outcome => outcome.FixtureOrdinal)
            .ThenBy(outcome => outcome.HomeClubId)
            .ThenBy(outcome => outcome.AwayClubId)
            .ThenBy(outcome => outcome.Result.Seed)
            .ToArray();

        var applied = new List<AppliedSimulationRun>(ordered.Length);
        foreach (var outcome in ordered)
        {
            applied.Add(career.World.ApplySimulationRun(
                outcome.CompetitionId,
                outcome.FixtureOrdinal,
                outcome.HomeClubId,
                outcome.AwayClubId,
                outcome.Result.HomeScore,
                outcome.Result.AwayScore,
                outcome.Result.Seed,
                outcome.Result.SimulationVersion,
                outcome.Result.Ticks,
                outcome.Result.Digest));
        }

        return applied;
    }

}
