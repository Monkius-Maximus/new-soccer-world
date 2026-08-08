using SoccerSim.Core.Domain;

namespace SoccerSim.Application;

public sealed class ClubRosterQueryService
{
    public IReadOnlyList<ClubRosterView> GetClubRosters(WorldState world) =>
        world.Clubs
            .OrderBy(x => x.Id)
            .Select(club => new ClubRosterView(
                club.Id,
                club.Name,
                club.ShortName,
                world.GetRoster(club.Id)
                    .Select(player => new PlayerView(
                        player.Id,
                        player.SquadNumber,
                        $"{player.FirstName} {player.LastName}",
                        player.PositionCode))
                    .ToArray()))
            .ToArray();
}
