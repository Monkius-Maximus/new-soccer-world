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
