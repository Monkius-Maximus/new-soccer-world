namespace SoccerSim.Core.Domain;

public sealed class WorldState
{
    public WorldState(
        IEnumerable<Country> countries,
        IEnumerable<City> cities,
        IEnumerable<Stadium> stadiums,
        IEnumerable<Club> clubs,
        IEnumerable<Player> players,
        IEnumerable<Competition> competitions)
    {
        Countries = countries.OrderBy(x => x.Id).ToArray();
        Cities = cities.OrderBy(x => x.Id).ToArray();
        Stadiums = stadiums.OrderBy(x => x.Id).ToArray();
        Clubs = clubs.OrderBy(x => x.Id).ToArray();
        Players = players.OrderBy(x => x.Id).ToArray();
        Competitions = competitions.OrderBy(x => x.Id).ToArray();
    }

    public IReadOnlyList<Country> Countries { get; }
    public IReadOnlyList<City> Cities { get; }
    public IReadOnlyList<Stadium> Stadiums { get; }
    public IReadOnlyList<Club> Clubs { get; }
    public IReadOnlyList<Player> Players { get; }
    public IReadOnlyList<Competition> Competitions { get; }

    public Club GetClub(int clubId) => Clubs.Single(x => x.Id == clubId);

    public IReadOnlyList<Player> GetRoster(int clubId) =>
        Players.Where(x => x.ClubId == clubId).OrderBy(x => x.SquadNumber).ThenBy(x => x.Id).ToArray();

    public WorldState Snapshot() => new(Countries, Cities, Stadiums, Clubs, Players, Competitions);
}
