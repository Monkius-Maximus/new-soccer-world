using Godot;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerDreamGame;

/// <summary>
/// MATCH-01: draws a running match, one simulated tick at a time.
/// <para>
/// This view owns no football rules. It advances <see cref="MatchSimulation"/> on the same fixed
/// timestep the simulation was built around and renders whatever the current tick contains, so
/// what is on screen is the simulation itself rather than a replay of a recording.
/// </para>
/// </summary>
public partial class MatchView : Node2D
{
    /// <summary>Stable token the headless test greps for.</summary>
    public const string MatchMarker = "[SOCCER-MATCH]";

    private static readonly Color PitchGreen = new("1f6b32");
    private static readonly Color PitchLine = new("ffffff", 0.55f);
    private static readonly Color HomeColour = new("3f7fe0");
    private static readonly Color AwayColour = new("d34a4a");
    private static readonly Color KeeperTrim = new("f5d020");
    private static readonly Color BallColour = new("ffffff");

    private MatchSimulation? _match;
    private Label _hud = null!;
    private double _accumulator;
    private double _speed = 30.0;
    private int _lastReportedGoals = -1;
    private string _screenshotPath = string.Empty;
    private int _screenshotAtTick = -1;
    private bool _screenshotTaken;

    /// <summary>Pixels per metre, and the top-left offset of the drawn pitch.</summary>
    private float _scale = 1.0f;
    private Vector2 _origin = Vector2.Zero;

    public override void _Ready()
    {
        _hud = GetNode<Label>("Hud");

        var speed = OS.GetEnvironment("SOCCER_MATCH_SPEED");
        if (!string.IsNullOrWhiteSpace(speed) &&
            double.TryParse(speed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0.0)
        {
            _speed = parsed;
        }

        // Optional: capture one frame to a PNG, so a headless run can produce visual evidence.
        _screenshotPath = OS.GetEnvironment("SOCCER_MATCH_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(_screenshotPath))
        {
            _screenshotAtTick = int.TryParse(OS.GetEnvironment("SOCCER_MATCH_SCREENSHOT_TICK"), out var at)
                ? at
                : 600;
        }

        var databasePath = OS.GetEnvironment("SOCCER_SAVE_DB");
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            _hud.Text = "Set SOCCER_SAVE_DB to a generated career world.db to watch a match.";
            GD.Print($"{MatchMarker} no-save");
            QuitIfHeadless(0);
            return;
        }

        try
        {
            // Presentation asks the Application for a match; it never builds one itself.
            var career = CompositionRoot.CreateCareerApplication().OpenCareer(databasePath);
            var clubs = career.World.Clubs;
            var seed = ParseSeed(OS.GetEnvironment("SOCCER_MATCH_SEED"));

            _match = CompositionRoot.CreateCareerApplication()
                .StartMatch(career, clubs[0].Id, clubs[1].Id, seed);

            GD.Print($"{MatchMarker} kickoff {clubs[0].Name} vs {clubs[1].Name} seed={seed}");
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _hud.Text = $"Match failed to start:\n{exception.Message}";
            GD.Print($"{MatchMarker} failed: {exception.Message}");
            QuitIfHeadless(1);
        }
    }

    public override void _Process(double delta)
    {
        if (_match is null || _match.IsFinished)
        {
            return;
        }

        // Step on the simulation's own clock. Wall-clock time only decides how many ticks to
        // run this frame; it never reaches the simulation, so the result does not depend on
        // frame rate.
        var secondsPerTick = SimulationSettings.FixedTimeStepMilliseconds / 1000.0;
        _accumulator += delta * _speed;

        var budget = 0;
        while (_accumulator >= secondsPerTick && !_match.IsFinished && budget < 20000)
        {
            _accumulator -= secondsPerTick;
            _match.Step();
            budget++;
        }

        QueueRedraw();

        var snapshot = _match.Snapshot();
        var goals = snapshot.HomeScore + snapshot.AwayScore;
        if (goals != _lastReportedGoals)
        {
            _lastReportedGoals = goals;
            GD.Print($"{MatchMarker} {snapshot.Minute:D2}' {snapshot.HomeScore}-{snapshot.AwayScore}");
        }

        if (!_screenshotTaken && _screenshotAtTick >= 0 && snapshot.Tick >= _screenshotAtTick)
        {
            _screenshotTaken = true;
            CallDeferred(nameof(CaptureFrame));
        }

        if (_match.IsFinished)
        {
            var result = _match.Result!;
            GD.Print($"{MatchMarker} fulltime {result.HomeScore}-{result.AwayScore} digest={result.Digest:X16}");
            QuitIfHeadless(0);
        }
    }

    public override void _Draw()
    {
        var viewport = GetViewportRect().Size;
        const float margin = 24.0f;

        _scale = Mathf.Min(
            (viewport.X - (2 * margin)) / (float)Pitch.Length,
            (viewport.Y - (2 * margin) - 40.0f) / (float)Pitch.Width);
        _origin = new Vector2(
            (viewport.X - ((float)Pitch.Length * _scale)) / 2.0f,
            ((viewport.Y + 40.0f) - ((float)Pitch.Width * _scale)) / 2.0f);

        DrawPitch();

        if (_match is null)
        {
            return;
        }

        var snapshot = _match.Snapshot();

        foreach (var player in snapshot.Players)
        {
            var colour = player.ClubId == snapshot.HomeClubId ? HomeColour : AwayColour;
            var at = ToScreen(player.Location);

            DrawCircle(at, 5.0f * Mathf.Max(_scale / 8.0f, 0.6f), colour);
            if (player.IsGoalkeeper)
            {
                DrawArc(at, 7.0f, 0.0f, Mathf.Tau, 16, KeeperTrim, 2.0f);
            }
        }

        DrawCircle(ToScreen(snapshot.Ball), 3.5f, BallColour);

        _hud.Text =
            $"{snapshot.Minute:D2}'   {snapshot.HomeScore} - {snapshot.AwayScore}" +
            $"   (tick {snapshot.Tick}, {_speed:0.#}x)";
    }

    private void DrawPitch()
    {
        var size = new Vector2((float)Pitch.Length, (float)Pitch.Width) * _scale;
        DrawRect(new Rect2(_origin, size), PitchGreen);
        DrawRect(new Rect2(_origin, size), PitchLine, filled: false, width: 2.0f);

        // Halfway line and centre circle.
        DrawLine(
            ToScreen(new Vec2(Pitch.Length / 2.0, 0.0)),
            ToScreen(new Vec2(Pitch.Length / 2.0, Pitch.Width)),
            PitchLine, 2.0f);
        DrawArc(ToScreen(Pitch.Centre), 9.15f * _scale, 0.0f, Mathf.Tau, 48, PitchLine, 2.0f);

        // Goals, drawn at the width the simulation actually uses.
        foreach (var goalX in new[] { 0.0, Pitch.Length })
        {
            DrawLine(
                ToScreen(new Vec2(goalX, Pitch.GoalMinY)),
                ToScreen(new Vec2(goalX, Pitch.GoalMaxY)),
                KeeperTrim, 4.0f);
        }
    }

    private void CaptureFrame()
    {
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(_screenshotPath);

        GD.Print(error == Error.Ok
            ? $"{MatchMarker} screenshot {_screenshotPath}"
            : $"{MatchMarker} screenshot-failed {error}");
    }

    private Vector2 ToScreen(Vec2 metres) =>
        _origin + new Vector2((float)metres.X * _scale, (float)metres.Y * _scale);

    private static ulong ParseSeed(string raw) =>
        ulong.TryParse(raw, out var seed) ? seed : 123456789UL;

    private void QuitIfHeadless(int exitCode)
    {
        if (OS.GetEnvironment("SOCCER_SMOKE_EXIT") == "1")
        {
            GetTree().Quit(exitCode);
        }
    }
}
