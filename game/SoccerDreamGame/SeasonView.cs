using Godot;
using SoccerSim.Application;

namespace SoccerDreamGame;

/// <summary>
/// COMP-00: plays the career's league season out and shows the table it produced.
/// <para>
/// The scene owns no competition rules. It asks the Application to advance a matchday and asks
/// it again for the standings, so what is on screen is the same table the career holds. CI
/// compares the rows this prints against the ones the console runner printed for the same
/// career, which is what stops the screen showing a table nobody played for.
/// </para>
/// </summary>
public partial class SeasonView : Control
{
    /// <summary>Stable tokens the headless test greps for.</summary>
    public const string SeasonMarker = "[SOCCER-SEASON]";
    public const string TableMarker = "[SOCCER-TABLE]";

    public override void _Ready()
    {
        var label = GetNode<Label>("Margin/SeasonLabel");
        var databasePath = OS.GetEnvironment("SOCCER_SAVE_DB");

        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            label.Text = """
                SoccerDreamGame — COMP-00

                Set SOCCER_SAVE_DB to a generated career world.db to play its league season
                and see the table.
                """;
            GD.Print($"{SeasonMarker} no-save");
            QuitIfHeadlessRun(0);
            return;
        }

        try
        {
            var application = CompositionRoot.CreateCareerApplication();
            var career = application.OpenCareer(databasePath);

            if (career.World.Season is null)
            {
                label.Text = "This career has no season in progress.";
                GD.Print($"{SeasonMarker} no-season");
                QuitIfHeadlessRun(1);
                return;
            }

            // Advancing is an Application use case; the scene never simulates anything itself.
            while (!career.World.Season!.IsComplete)
            {
                application.AdvanceMatchday(career);
            }

            var season = CompositionRoot.CreateSeasonQuery().GetSeason(career.World)!;
            label.Text = Render(season);
            GD.Print(label.Text);

            foreach (var standing in season.Standings)
            {
                GD.Print($"{TableMarker} {standing.ToRecordLine()}");
            }

            GD.Print(
                $"{SeasonMarker} competition={season.CompetitionId} " +
                $"matchdays={season.TotalMatchdays} played={season.Fixtures.Count(f => f.IsPlayed)}");

            // Nothing is checkpointed: this run reads a career and shows what a season does to
            // it, and the save on disk is left where the console runner left it.
            QuitIfHeadlessRun(0);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            label.Text = $"Season view failed:\n{exception.Message}";
            GD.Print($"{SeasonMarker} failed: {exception.Message}");
            QuitIfHeadlessRun(1);
        }
    }

    private static string Render(SeasonSummaryView season)
    {
        var lines = new List<string>
        {
            season.CompetitionName,
            $"Matchday {season.CurrentMatchday} of {season.TotalMatchdays}" +
                (season.IsComplete ? "  —  season complete" : string.Empty),
            string.Empty,
            "Pos Club                    Pl  W  D  L  GF  GA Pts"
        };

        foreach (var standing in season.Standings)
        {
            var row = standing.Row;
            lines.Add(
                $"{standing.Position,3} {standing.ClubName,-22} {row.Played,2} {row.Won,2} " +
                $"{row.Drawn,2} {row.Lost,2} {row.GoalsFor,3} {row.GoalsAgainst,3} {row.Points,3}");
        }

        lines.Add(string.Empty);
        lines.Add("Results");
        foreach (var fixture in season.Fixtures)
        {
            var score = fixture.IsPlayed ? $"{fixture.HomeScore}-{fixture.AwayScore}" : "  v  ";
            lines.Add($"MD{fixture.Matchday}  {fixture.Date:yyyy-MM-dd}  {fixture.HomeName,-22} {score} {fixture.AwayName}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>Lets CI run this scene as a one-shot check, like the other scenes.</summary>
    private void QuitIfHeadlessRun(int exitCode)
    {
        if (OS.GetEnvironment("SOCCER_SMOKE_EXIT") == "1")
        {
            GetTree().Quit(exitCode);
        }
    }
}
