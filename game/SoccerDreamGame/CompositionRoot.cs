using SoccerSim.Application;
using SoccerSim.Infrastructure.Sqlite;

namespace SoccerDreamGame;

/// <summary>
/// The single place in the presentation layer that is allowed to know which Infrastructure
/// adapter implements the Core ports. Scenes depend on Application use cases only, so no
/// Godot script ever touches a repository, a connection or SQL.
/// </summary>
internal static class CompositionRoot
{
    public static CareerApplication CreateCareerApplication() => new(new SqliteCareerStore());

    public static ClubRosterQueryService CreateRosterQuery() => new();

    public static SeasonQueryService CreateSeasonQuery() => new();
}
