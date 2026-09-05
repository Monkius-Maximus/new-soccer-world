using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Competitions;

public sealed record LeagueRow(
    int Position,
    int ClubId,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst)
{
    public int GoalDifference => GoalsFor - GoalsAgainst;

    /// <summary>Three for a win, one for a draw. Not configurable until a competition needs it.</summary>
    public int Points => (Won * 3) + Drawn;

    public override string ToString() =>
        $"{Position,2}. club {ClubId}  {Played,2} {Won,2} {Drawn,2} {Lost,2}  {GoalsFor,3}:{GoalsAgainst,-3} {Points,3}";
}

/// <summary>
/// The standings, derived from results rather than stored.
/// <para>
/// A stored table would be a second source of truth the moment a result changed, so it is
/// recomputed instead — the same reasoning that kept an overall rating out of PLYR-00.
/// </para>
/// </summary>
public static class LeagueTable
{
    public static IReadOnlyList<LeagueRow> Build(
        IReadOnlyList<int> clubIds,
        IEnumerable<AppliedSimulationRun> results,
        int competitionId)
    {
        ArgumentNullException.ThrowIfNull(clubIds);
        ArgumentNullException.ThrowIfNull(results);

        var played = results.Where(run => run.CompetitionId == competitionId).ToArray();

        var rows = clubIds
            .OrderBy(id => id)
            .Select(clubId => Summarise(clubId, played))
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.GoalDifference)
            .ThenByDescending(row => row.GoalsFor)
            // Club id last so two identical records still order the same way every time.
            .ThenBy(row => row.ClubId)
            .ToArray();

        return [.. rows.Select((row, index) => row with { Position = index + 1 })];
    }

    private static LeagueRow Summarise(int clubId, IReadOnlyList<AppliedSimulationRun> results)
    {
        int played = 0, won = 0, drawn = 0, lost = 0, goalsFor = 0, goalsAgainst = 0;

        for (var i = 0; i < results.Count; i++)
        {
            var run = results[i];
            int scored, conceded;

            if (run.HomeClubId == clubId)
            {
                (scored, conceded) = (run.HomeScore, run.AwayScore);
            }
            else if (run.AwayClubId == clubId)
            {
                (scored, conceded) = (run.AwayScore, run.HomeScore);
            }
            else
            {
                continue;
            }

            played++;
            goalsFor += scored;
            goalsAgainst += conceded;

            if (scored > conceded) won++;
            else if (scored == conceded) drawn++;
            else lost++;
        }

        return new LeagueRow(0, clubId, played, won, drawn, lost, goalsFor, goalsAgainst);
    }
}
