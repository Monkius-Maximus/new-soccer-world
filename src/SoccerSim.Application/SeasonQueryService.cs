using SoccerSim.Core.Competitions;
using SoccerSim.Core.Domain;

namespace SoccerSim.Application;

/// <summary>
/// Presentation-facing view of the season. The table is recomputed from applied results here
/// exactly as it is everywhere else, so a screen can never show standings that disagree with
/// the career they came from.
/// </summary>
public sealed class SeasonQueryService
{
    /// <summary>Null when the career has no season in progress.</summary>
    public SeasonSummaryView? GetSeason(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.Season is not { } season)
        {
            return null;
        }

        var competition = world.Competitions.SingleOrDefault(x => x.Id == season.CompetitionId);
        var results = world.SimulationRuns
            .Where(run => run.CompetitionId == season.CompetitionId)
            .ToArray();

        var standings = LeagueTable.Build(season.ClubIds, results, season.CompetitionId)
            .Select(row =>
            {
                var club = world.GetClub(row.ClubId);
                return new StandingView(row.Position, row.ClubId, club.Name, club.ShortName, row);
            })
            .ToArray();

        var fixtures = season.Fixtures
            .Select(fixture =>
            {
                var played = results.FirstOrDefault(run => run.FixtureOrdinal == fixture.Ordinal);
                return new FixtureView(
                    fixture.Ordinal,
                    fixture.Matchday,
                    fixture.Date,
                    world.GetClub(fixture.HomeClubId).Name,
                    world.GetClub(fixture.AwayClubId).Name,
                    played?.HomeScore,
                    played?.AwayScore);
            })
            .ToArray();

        return new SeasonSummaryView(
            season.CompetitionId,
            competition?.Name ?? $"Competition {season.CompetitionId}",
            season.CurrentMatchday,
            season.TotalMatchdays,
            season.IsComplete,
            standings,
            fixtures);
    }
}
