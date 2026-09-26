using System.Globalization;
using Godot;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;

namespace SoccerDreamGame;

/// <summary>
/// Runs a match and shows it. Godot paces the simulation and draws it; it decides nothing about
/// football, and it never touches a database except through an Application use case.
/// </summary>
public partial class Main : Control
{
    /// <summary>Stable token the headless smoke test greps for.</summary>
    public const string SmokeTestMarker = "[SOCCER-SMOKE]";

    private const ulong DefaultMatchSeed = 123456789UL;

    private static readonly double SecondsPerTick =
        SimulationSettings.FixedTimeStepMilliseconds / 1000.0;

    /// <summary>
    /// Ceiling on catch-up within one frame. Without it a large speed — or one long stall —
    /// lets the backlog grow faster than a frame can clear it, and the window stops responding.
    /// </summary>
    private const int MaxTicksPerFrame = 4000;

    private PitchView _pitch = null!;
    private Label _status = null!;
    private MatchSimulation? _match;
    private string _homeName = "Home";
    private string _awayName = "Away";

    /// <summary>Match seconds simulated per real second. 1.0 watches the game at its own pace.</summary>
    private double _speed = 1.0;
    private double _pending;
    private bool _reported;

    public override void _Ready()
    {
        _pitch = GetNode<PitchView>("Pitch");
        _status = GetNode<Label>("Status");
        _speed = ReadDouble("SOCCER_MATCH_SPEED", 1.0);

        var databasePath = OS.GetEnvironment("SOCCER_SAVE_DB");
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            _status.Text =
                "Set SOCCER_SAVE_DB to a generated career world.db to watch a match.";
            GD.Print($"{SmokeTestMarker} no-save");
            QuitIfHeadlessSmokeRun(0);
            return;
        }

        try
        {
            // Presentation goes through an Application use case, never through a repository.
            var application = CompositionRoot.CreateCareerApplication();
            var career = application.OpenCareer(databasePath);
            var rosters = CompositionRoot.CreateRosterQuery().GetClubRosters(career.World);

            // Kept from the Foundation smoke test: it is still the cheapest proof that this
            // scene reached the Application layer and got real seeded content back.
            GD.Print(
                $"{SmokeTestMarker} clubs={rosters.Count} " +
                $"players={rosters.Sum(club => club.Players.Count)}");

            if (rosters.Count < 2)
            {
                throw new InvalidOperationException("A match needs at least two clubs in the save.");
            }

            _homeName = rosters[0].ShortName;
            _awayName = rosters[1].ShortName;
            _match = application.BeginMatch(
                career,
                rosters[0].ClubId,
                rosters[1].ClubId,
                ReadSeed("SOCCER_MATCH_SEED", DefaultMatchSeed));

            UpdateDisplay(_match.CurrentFrame());
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _status.Text = $"Match failed to start:\n{exception.Message}";
            GD.Print($"{SmokeTestMarker} failed: {exception.Message}");
            QuitIfHeadlessSmokeRun(1);
        }
    }

    public override void _Process(double delta)
    {
        if (_match is not { } match)
        {
            return;
        }

        if (match.IsFinished)
        {
            ReportOnce(match);
            return;
        }

        // A fixed-step accumulator, because the timestep is not the renderer's to choose.
        // ADR-0004 fixes it at 50 ms; a different tick length is a different simulation, so
        // speed changes how many of those steps a second buys, never how long one step is.
        _pending += delta * _speed;

        var budget = MaxTicksPerFrame;
        while (_pending >= SecondsPerTick && budget > 0 && match.Advance())
        {
            _pending -= SecondsPerTick;
            budget--;
        }

        if (budget == 0)
        {
            // The backlog cannot be cleared at this speed; drop it rather than accumulate a
            // debt the next frame inherits.
            _pending = 0.0;
        }

        UpdateDisplay(match.CurrentFrame());

        if (match.IsFinished)
        {
            ReportOnce(match);
        }
    }

    private void UpdateDisplay(MatchFrame frame)
    {
        _pitch.ShowFrame(frame);
        _status.Text =
            $"{_homeName} {frame.HomeScore} - {frame.AwayScore} {_awayName}" +
            $"    {frame.Minute:00}'    tick {frame.Tick}/{frame.TotalTicks}";
    }

    private void ReportOnce(MatchSimulation match)
    {
        if (_reported)
        {
            return;
        }
        _reported = true;

        var result = match.Result;

        // The digest is the point of printing this: the headless runner prints the same value
        // for the same seed, so CI can prove the rendered match is the same match rather than
        // a lookalike driven by a second code path.
        GD.Print(
            $"{SmokeTestMarker} match ticks={result.Ticks} " +
            $"score={result.HomeScore}-{result.AwayScore} digest={result.Digest:X16}");

        QuitIfHeadlessSmokeRun(0);
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

    private static double ReadDouble(string variable, double fallback) =>
        double.TryParse(
            OS.GetEnvironment(variable),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) && parsed > 0.0
            ? parsed
            : fallback;

    private static ulong ReadSeed(string variable, ulong fallback) =>
        ulong.TryParse(
            OS.GetEnvironment(variable),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
}
