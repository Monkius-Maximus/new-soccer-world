using SoccerSim.Core.Tactics;

namespace SoccerSim.Core.Domain;

/// <summary>
/// The operational authority while a career is open.
/// <para>
/// Reference data (countries, cities, clubs, players…) is immutable for the whole session.
/// Career progress accumulates through <see cref="ApplySimulationRun"/> and is written to
/// disk only at an explicit checkpoint, so abandoning a session discards everything applied
/// since the last one.
/// </para>
/// </summary>
public sealed class WorldState
{
    private readonly List<AppliedSimulationRun> _simulationRuns;
    private bool _hasUnsavedChanges;

    public WorldState(
        IEnumerable<Country> countries,
        IEnumerable<City> cities,
        IEnumerable<Stadium> stadiums,
        IEnumerable<Club> clubs,
        IEnumerable<Player> players,
        IEnumerable<Competition> competitions)
        : this(countries, cities, stadiums, clubs, players, competitions, [], [])
    {
    }

    public WorldState(
        IEnumerable<Country> countries,
        IEnumerable<City> cities,
        IEnumerable<Stadium> stadiums,
        IEnumerable<Club> clubs,
        IEnumerable<Player> players,
        IEnumerable<Competition> competitions,
        IEnumerable<AppliedSimulationRun> simulationRuns)
        : this(countries, cities, stadiums, clubs, players, competitions, simulationRuns, [])
    {
    }

    public WorldState(
        IEnumerable<Country> countries,
        IEnumerable<City> cities,
        IEnumerable<Stadium> stadiums,
        IEnumerable<Club> clubs,
        IEnumerable<Player> players,
        IEnumerable<Competition> competitions,
        IEnumerable<AppliedSimulationRun> simulationRuns,
        IEnumerable<ClubTacticSetup> clubTactics)
    {
        ClubTactics = clubTactics.OrderBy(x => x.ClubId).ToArray();
        Countries = countries.OrderBy(x => x.Id).ToArray();
        Cities = cities.OrderBy(x => x.Id).ToArray();
        Stadiums = stadiums.OrderBy(x => x.Id).ToArray();
        Clubs = clubs.OrderBy(x => x.Id).ToArray();
        Players = players.OrderBy(x => x.Id).ToArray();
        Competitions = competitions.OrderBy(x => x.Id).ToArray();
        _simulationRuns = simulationRuns.OrderBy(x => x.Ordinal).ToList();
    }

    public IReadOnlyList<Country> Countries { get; }
    public IReadOnlyList<City> Cities { get; }
    public IReadOnlyList<Stadium> Stadiums { get; }
    public IReadOnlyList<Club> Clubs { get; }
    public IReadOnlyList<Player> Players { get; }
    public IReadOnlyList<Competition> Competitions { get; }

    /// <summary>Each club's chosen setup, ordered by club.</summary>
    public IReadOnlyList<ClubTacticSetup> ClubTactics { get; }

    /// <summary>Applied results, in career apply order.</summary>
    public IReadOnlyList<AppliedSimulationRun> SimulationRuns => _simulationRuns;

    /// <summary>True when progress has been applied but not yet written to a checkpoint.</summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public Club GetClub(int clubId) => Clubs.Single(x => x.Id == clubId);

    /// <summary>
    /// The club's setup, or a balanced default. A club without stored tactics still has to be
    /// able to play, so this never throws.
    /// </summary>
    public TeamTactics TacticsFor(int clubId) =>
        ClubTactics.FirstOrDefault(x => x.ClubId == clubId)?.Tactics ?? TeamTactics.Default;

    public IReadOnlyList<Player> GetRoster(int clubId) =>
        Players.Where(x => x.ClubId == clubId).OrderBy(x => x.SquadNumber).ThenBy(x => x.Id).ToArray();

    /// <summary>
    /// Appends a result at the next career ordinal. Only the Application calls this, and only
    /// after a match has finished; a running match never reaches the world it came from.
    /// </summary>
    public AppliedSimulationRun ApplySimulationRun(
        int homeClubId,
        int awayClubId,
        int homeScore,
        int awayScore,
        ulong seed,
        int simulationVersion,
        int ticks,
        ulong digest)
    {
        var applied = new AppliedSimulationRun(
            _simulationRuns.Count + 1,
            homeClubId,
            awayClubId,
            homeScore,
            awayScore,
            seed,
            simulationVersion,
            ticks,
            digest);

        _simulationRuns.Add(applied);
        _hasUnsavedChanges = true;
        return applied;
    }

    /// <summary>Called by the persistence layer once a checkpoint has committed.</summary>
    public void MarkPersisted() => _hasUnsavedChanges = false;

    /// <summary>
    /// An isolated copy for a match to read. The copy carries its own run list, so anything
    /// the match does cannot reach this instance.
    /// </summary>
    public WorldState Snapshot() =>
        new(Countries, Cities, Stadiums, Clubs, Players, Competitions, _simulationRuns, ClubTactics);
}
