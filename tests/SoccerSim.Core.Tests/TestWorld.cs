using SoccerSim.Core.Domain;
using SoccerSim.Core.Tactics;

namespace SoccerSim.Core.Tests;

/// <summary>Builds a synthetic two-club world large enough to field two elevens.</summary>
internal static class TestWorld
{
    public const int HomeClubId = 1;
    public const int AwayClubId = 2;

    private static readonly PlayerPosition[] SquadShape =
    [
        PlayerPosition.Goalkeeper, PlayerPosition.Goalkeeper,
        PlayerPosition.RightBack, PlayerPosition.CentreBack, PlayerPosition.CentreBack,
        PlayerPosition.LeftBack, PlayerPosition.CentreBack,
        PlayerPosition.DefensiveMidfielder, PlayerPosition.CentralMidfielder,
        PlayerPosition.CentralMidfielder, PlayerPosition.AttackingMidfielder,
        PlayerPosition.RightWinger, PlayerPosition.LeftWinger,
        PlayerPosition.Striker, PlayerPosition.Striker
    ];

    /// <summary>An eleven of ordinary players, for exercising shape without running a match.</summary>
    public static IReadOnlyList<Player> Eleven() => [.. Squad(HomeClubId, 100, 0).Take(11)];

    /// <param name="homeQuality">Shifts every home attribute, for testing that ability matters.</param>
    public static WorldState Build(
        int homeQuality = 0,
        int awayQuality = 0,
        TeamTactics? homeTactics = null,
        TeamTactics? awayTactics = null) => new(
        [new Country(1, "BRA", "Brasil")],
        [new City(1, 1, "Recife")],
        [new Stadium(1, 1, "Estadio das Pontes", 18000)],
        [new Club(HomeClubId, 1, 1, "Recife Azul", "RAZ"), new Club(AwayClubId, 1, 1, "Recife Vermelho", "RVM")],
        [.. Squad(HomeClubId, 100, homeQuality), .. Squad(AwayClubId, 200, awayQuality)],
        [new Competition(1, 1, "Amistoso da Fundacao", "friendly")],
        [],
        [
            .. homeTactics is null ? Array.Empty<ClubTacticSetup>() : [new ClubTacticSetup(HomeClubId, homeTactics)],
            .. awayTactics is null ? Array.Empty<ClubTacticSetup>() : [new ClubTacticSetup(AwayClubId, awayTactics)]
        ]);

    private static IEnumerable<Player> Squad(int clubId, int idBase, int quality) =>
        SquadShape.Select((position, index) => new Player(
            idBase + index,
            1,
            clubId,
            $"Player{idBase + index}",
            $"Club{clubId}",
            new DateOnly(2000, 1, 1),
            index + 1,
            position,
            Attributes(position, quality)));

    private static PlayerAttributes Attributes(PlayerPosition position, int quality)
    {
        var keeper = position == PlayerPosition.Goalkeeper;
        int Rate(int baseValue) => Math.Clamp(baseValue + quality, PlayerAttributes.Minimum, PlayerAttributes.Maximum);

        return new PlayerAttributes(
            Rate(12), Rate(12), Rate(12), Rate(12),
            Rate(keeper ? 3 : 12),
            Rate(12), Rate(12), Rate(12),
            Rate(keeper ? 16 : 2));
    }
}
