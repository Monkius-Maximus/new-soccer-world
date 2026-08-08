namespace SoccerSim.Core.Simulation;

public interface IRandomSource
{
    uint NextUInt32();
    int NextInt(int maxExclusive);
    ulong State { get; }
}
