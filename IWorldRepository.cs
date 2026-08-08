using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Persistence;

public interface IWorldRepository
{
    WorldState Load(string databasePath);
}
