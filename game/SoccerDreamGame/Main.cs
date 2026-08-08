using Godot;

namespace SoccerDreamGame;

public partial class Main : Control
{
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
            return;
        }

        try
        {
            var world = CompositionRoot.LoadWorld(databasePath);
            var rosters = CompositionRoot.CreateRosterQuery().GetClubRosters(world);
            label.Text = string.Join(
                "\n\n",
                rosters.Select(club =>
                    $"{club.ClubName} ({club.ShortName})\n" +
                    string.Join("\n", club.Players.Select(player =>
                        $"#{player.SquadNumber:00}  {player.DisplayName}  [{player.PositionCode}]"))));
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            label.Text = $"Foundation smoke test failed:\n{exception.Message}";
        }
    }
}
