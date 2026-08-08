using SoccerSim.Core.Domain;
using SoccerSim.Core.Persistence;

namespace SoccerSim.Application;

public sealed class ActiveCareer
{
    public ActiveCareer(CareerSave save, WorldState world)
    {
        Save = save;
        World = world;
    }

    public CareerSave Save { get; private set; }
    public WorldState World { get; }

    internal void ReplaceSaveMetadata(CareerSave save) => Save = save;
}
