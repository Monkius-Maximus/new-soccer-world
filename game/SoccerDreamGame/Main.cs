using Godot;

namespace SoccerDreamGame;

public partial class Main : Control
{
    /// <summary>Stable token the headless smoke test greps for.</summary>
    public const string SmokeTestMarker = "[SOCCER-SMOKE]";

    public override void _Ready()
    {
        var label = GetNode<Label>("Margin/RosterLabel");
        var databasePath = OS.GetEnvironment("SOCCER_SAVE_DB");

        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            label.Text = """
                SoccerDreamGame — FOUNDATION-00

                Godot .NET smoke test loaded successfully.

                To display the seeded clubs, set SOCCER_SAVE_DB to a generated career world.db.
                Godot calls the Application layer for presentation data and never executes SQL directly.
                """;
            GD.Print($"{SmokeTestMarker} no-save");
            QuitIfHeadlessSmokeRun(0);
            return;
        }

        try
        {
            // Presentation goes through an Application use case, never through a repository.
            var career = CompositionRoot.CreateCareerApplication().OpenCareer(databasePath);
            var rosters = CompositionRoot.CreateRosterQuery().GetClubRosters(career.World);
            var rendered = string.Join(
                "\n\n",
                rosters.Select(club =>
                    $"{club.ClubName} ({club.ShortName})\n" +
                    string.Join("\n", club.Players.Select(player =>
                        $"#{player.SquadNumber:00}  {player.DisplayName}  [{player.PositionCode}]"))));

            label.Text = rendered;

            // Mirrored to stdout so `godot --headless` can assert this scene really reached
            // the Application layer. Without it the smoke test is only verifiable by eye.
            GD.Print(rendered);
            GD.Print($"{SmokeTestMarker} clubs={rosters.Count} players={rosters.Sum(club => club.Players.Count)}");
            QuitIfHeadlessSmokeRun(0);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            label.Text = $"Foundation smoke test failed:\n{exception.Message}";
            GD.Print($"{SmokeTestMarker} failed: {exception.Message}");
            QuitIfHeadlessSmokeRun(1);
        }
    }

    /// <summary>
    /// Lets CI run this scene as a one-shot check. Interactive runs ignore it and stay open.
    /// </summary>
    private void QuitIfHeadlessSmokeRun(int exitCode)
    {
        if (OS.GetEnvironment("SOCCER_SMOKE_EXIT") == "1")
        {
            GetTree().Quit(exitCode);
        }
    }
}
