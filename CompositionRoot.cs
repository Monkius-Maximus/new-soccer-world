using SoccerSim.Application;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerDreamGame;

internal static class CompositionRoot
{
    public static ClubRosterQueryService CreateRosterQuery() => new();

    public static SoccerSim.Core.Domain.WorldState LoadWorld(string databasePath) =>
        new SqliteWorldRepository().Load(databasePath);
}
