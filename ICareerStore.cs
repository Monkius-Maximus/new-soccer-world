using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Persistence;

public interface ICareerStore
{
    CareerSave CreateCareer(
        string templateDatabasePath,
        string savesRoot,
        string saveId,
        ulong careerSeed,
        string gameVersion,
        string contentVersion,
        DateTimeOffset timestamp);

    CareerSave OpenCareer(string databasePath);

    WorldState LoadWorld(string databasePath);

    CareerSave Checkpoint(
        CareerSave save,
        WorldState world,
        CheckpointKind kind,
        DateTimeOffset timestamp);
}
