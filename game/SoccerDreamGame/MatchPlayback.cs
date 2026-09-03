using Godot;
using SoccerSim.Core.Match;

namespace SoccerDreamGame;

/// <summary>
/// MATCH-01's first visual consumer. Football state comes exclusively from MATCH-00 frames;
/// this scene only maps coordinates, interpolates and presents.
/// </summary>
public partial class MatchPlayback : Node3D
{
    public const string SmokeTestMarker = "[SOCCER-MATCH-SMOKE]";
    private const double SimulatedSecondsPerRealSecond = 7.5;

    private readonly Dictionary<int, MeshInstance3D> _actors = [];
    private IReadOnlyList<MatchFrame> _frames = [];
    private MeshInstance3D? _ball;
    private Label? _score;
    private Label? _clock;
    private int _frameIndex;
    private double _presentationSeconds;
    private double _speed = 1.0;
    private bool _paused;

    public override void _Ready()
    {
        BuildPitch();
        BuildLightingAndCamera();
        BuildHud();

        var databasePath = OS.GetEnvironment("SOCCER_SAVE_DB");
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            GD.Print($"{SmokeTestMarker} no-save");
            QuitIfSmokeRun(0);
            return;
        }

        try
        {
            var application = CompositionRoot.CreateCareerApplication();
            var career = application.OpenCareer(databasePath);
            var clubs = career.World.Clubs;
            if (clubs.Count < 2)
            {
                throw new InvalidOperationException("MATCH-01 requires at least two seeded clubs.");
            }

            var seedText = OS.GetEnvironment("SOCCER_MATCH_SEED");
            var seed = ulong.TryParse(seedText, out var parsed) ? parsed : 123456789UL;
            var outcome = application.RunMatchForPlayback(career, clubs[0].Id, clubs[1].Id, seed);
            _frames = outcome.Result.Frames;

            if (_frames.Count == 0)
            {
                throw new InvalidOperationException("Playback match returned no spatial frames.");
            }

            BuildActors(_frames[0]);
            Present(_frames[0], _frames[0], 0.0);

            GD.Print(
                $"{SmokeTestMarker} frames={_frames.Count} players={_actors.Count} " +
                $"score={outcome.Result.HomeScore}-{outcome.Result.AwayScore} seed={seed}");
            QuitIfSmokeRun(0);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GD.Print($"{SmokeTestMarker} failed: {exception.Message}");
            QuitIfSmokeRun(1);
        }
    }

    public override void _Process(double delta)
    {
        if (_paused || _frames.Count < 2)
        {
            return;
        }

        _presentationSeconds += delta * _speed;
        var simulatedTick = _presentationSeconds
            * SimulatedSecondsPerRealSecond
            * 1000.0
            / SoccerSim.Core.Simulation.SimulationSettings.FixedTimeStepMilliseconds;

        while (_frameIndex + 1 < _frames.Count && _frames[_frameIndex + 1].Tick <= simulatedTick)
        {
            _frameIndex++;
        }

        if (_frameIndex + 1 >= _frames.Count)
        {
            Present(_frames[^1], _frames[^1], 0.0);
            _paused = true;
            return;
        }

        var current = _frames[_frameIndex];
        var next = _frames[_frameIndex + 1];
        var span = next.Tick - current.Tick;
        var alpha = span <= 0 ? 0.0 : (simulatedTick - current.Tick) / span;
        Present(current, next, Math.Clamp(alpha, 0.0, 1.0));
    }

    private void BuildPitch()
    {
        var pitch = new MeshInstance3D
        {
            Name = "Pitch",
            Mesh = new BoxMesh { Size = new Vector3(105.0f, 0.2f, 68.0f) },
            Position = new Vector3(0.0f, -0.1f, 0.0f),
            MaterialOverride = Material(new Color("2d7d46"))
        };
        AddChild(pitch);

        AddBoundary(new Vector3(0.0f, 0.015f, -34.0f), new Vector3(105.0f, 0.03f, 0.12f));
        AddBoundary(new Vector3(0.0f, 0.015f, 34.0f), new Vector3(105.0f, 0.03f, 0.12f));
        AddBoundary(new Vector3(-52.5f, 0.015f, 0.0f), new Vector3(0.12f, 0.03f, 68.0f));
        AddBoundary(new Vector3(52.5f, 0.015f, 0.0f), new Vector3(0.12f, 0.03f, 68.0f));
        AddBoundary(new Vector3(0.0f, 0.015f, 0.0f), new Vector3(0.12f, 0.03f, 68.0f));
    }

    private void AddBoundary(Vector3 position, Vector3 size)
    {
        var line = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = Material(Colors.White)
        };
        AddChild(line);
    }

    private void BuildLightingAndCamera()
    {
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55.0f, -25.0f, 0.0f),
            ShadowEnabled = true
        };
        AddChild(light);

        var camera = new Camera3D
        {
            Position = new Vector3(0.0f, 72.0f, 62.0f),
            Current = true,
            Fov = 48.0f
        };
        AddChild(camera);
        camera.LookAt(Vector3.Zero, Vector3.Up);
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var panel = new HBoxContainer
        {
            OffsetLeft = 24.0f,
            OffsetTop = 20.0f,
            OffsetRight = 560.0f,
            OffsetBottom = 64.0f
        };
        layer.AddChild(panel);

        _score = new Label { Text = "0 - 0", CustomMinimumSize = new Vector2(90.0f, 36.0f) };
        _clock = new Label { Text = "00:00", CustomMinimumSize = new Vector2(80.0f, 36.0f) };
        panel.AddChild(_score);
        panel.AddChild(_clock);
        panel.AddChild(Button("Pause", () => _paused = !_paused));
        panel.AddChild(Button("1x", () => _speed = 1.0));
        panel.AddChild(Button("2x", () => _speed = 2.0));
        panel.AddChild(Button("4x", () => _speed = 4.0));
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }

    private void BuildActors(MatchFrame frame)
    {
        var homeClubId = frame.Players[0].ClubId;
        foreach (var player in frame.Players)
        {
            var actor = new MeshInstance3D
            {
                Name = $"Player_{player.PlayerId}",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.42f,
                    BottomRadius = 0.42f,
                    Height = 1.7f
                },
                MaterialOverride = Material(
                    player.ClubId == homeClubId ? new Color("3d7dff") : new Color("ef3f4f"))
            };
            AddChild(actor);
            _actors.Add(player.PlayerId, actor);
        }

        _ball = new MeshInstance3D
        {
            Name = "Ball",
            Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f },
            MaterialOverride = Material(Colors.White)
        };
        AddChild(_ball);
    }

    private void Present(MatchFrame current, MatchFrame next, double alpha)
    {
        var nextByPlayer = new Dictionary<int, MatchPlayerSample>(next.Players.Count);
        foreach (var player in next.Players)
        {
            nextByPlayer.Add(player.PlayerId, player);
        }

        foreach (var player in current.Players)
        {
            var target = nextByPlayer[player.PlayerId];
            var x = Lerp(player.Location.X, target.Location.X, alpha);
            var y = Lerp(player.Location.Y, target.Location.Y, alpha);
            _actors[player.PlayerId].Position = Map(x, y, 0.85);
        }

        if (_ball is not null)
        {
            var ballX = Lerp(current.BallLocation.X, next.BallLocation.X, alpha);
            var ballY = Lerp(current.BallLocation.Y, next.BallLocation.Y, alpha);
            _ball.Position = Map(ballX, ballY, 0.3);
        }

        if (_score is not null)
        {
            _score.Text = $"{current.HomeScore} - {current.AwayScore}";
        }

        if (_clock is not null)
        {
            var tick = Lerp(current.Tick, next.Tick, alpha);
            var seconds = tick
                * SoccerSim.Core.Simulation.SimulationSettings.FixedTimeStepMilliseconds
                / 1000.0;
            _clock.Text = $"{(int)(seconds / 60.0):00}:{(int)(seconds % 60.0):00}";
        }
    }

    private static Vector3 Map(double x, double y, double height) =>
        new((float)x, (float)height, (float)y);

    private static double Lerp(double from, double to, double weight) =>
        from + ((to - from) * weight);

    private static StandardMaterial3D Material(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = 0.9f
    };

    private void QuitIfSmokeRun(int exitCode)
    {
        if (OS.GetEnvironment("SOCCER_MATCH_SMOKE_EXIT") == "1")
        {
            GetTree().Quit(exitCode);
        }
    }
}
