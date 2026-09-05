using SoccerSim.Core.Competitions;

namespace SoccerSim.Application;

/// <summary>One row of a rendered table: the standing plus the names a screen needs.</summary>
public sealed record StandingView(
    int Position,
    int ClubId,
    string ClubName,
    string ShortName,
    LeagueRow Row)
{
    /// <summary>
    /// A single stable line per row. The console runner and the presentation layer both emit
    /// this and CI compares them, so a table that is right in memory and wrong on screen fails
    /// the build.
    /// </summary>
    public string ToRecordLine() =>
        $"{Position}|{ShortName}|{Row.Played}|{Row.Won}|{Row.Drawn}|{Row.Lost}|" +
        $"{Row.GoalsFor}|{Row.GoalsAgainst}|{Row.Points}";
}

/// <summary>A fixture with both clubs named, and the result if it has been played.</summary>
public sealed record FixtureView(
    int Ordinal,
    int Matchday,
    DateOnly Date,
    string HomeName,
    string AwayName,
    int? HomeScore,
    int? AwayScore)
{
    public bool IsPlayed => HomeScore.HasValue;
}

/// <summary>The competition in progress as a screen sees it.</summary>
public sealed record SeasonSummaryView(
    int CompetitionId,
    string CompetitionName,
    int CurrentMatchday,
    int TotalMatchdays,
    bool IsComplete,
    IReadOnlyList<StandingView> Standings,
    IReadOnlyList<FixtureView> Fixtures);
