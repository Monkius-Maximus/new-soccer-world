namespace SoccerSim.Core.Match;

/// <summary>
/// Every number the match balance depends on, in one place.
/// <para>
/// These are tuned against observed output, not guessed. Measured over 300 matches between two
/// identically-rated squads:
/// <code>
/// goals/match  3.14   (home 1.53, away 1.61)
/// shots/match 28.3    of which 9.0 off target
/// saves/match 16.0
/// passes/match 928, interceptions 74, tackles 203
/// </code>
/// Real top-flight football sits near 2.7 goals and 25 shots, so this is in range without
/// claiming to be final. Changing any constant changes results for a given seed, so a change
/// here is a <see cref="Simulation.SimulationSettings.SimulationVersion"/> bump once results ship.
/// </para>
/// </summary>
public static class MatchTuning
{
    public const int MatchMinutes = 90;

    /// <summary>How often the player on the ball reconsiders. Between decisions they drive forward.</summary>
    public const double DecisionIntervalSeconds = 0.5;

    /// <summary>Distance within which a loose ball can be brought under control.</summary>
    public const double ControlRadius = 1.2;

    /// <summary>A goalkeeper's reach is wider, and scales with the Goalkeeping attribute.</summary>
    public const double KeeperBaseReach = 1.6;
    public const double KeeperReachPerPoint = 0.09;

    /// <summary>Odds a keeper who reaches a shot actually stops it, rolled once per shot.</summary>
    public const int KeeperSaveBaseChance = 355;
    public const int KeeperSavePerPoint = 26;
    public const int KeeperSavePenaltyPerShotPoint = 10;

    /// <summary>
    /// How close two players' distances to the ball must be to count as a genuine fifty-fifty,
    /// in squared metres. Players stop exactly on the ball, so exact ties are common.
    /// </summary>
    public const double ContestedBallToleranceSquared = 0.0025;

    /// <summary>An opponent this close to the carrier is applying pressure and may challenge.</summary>
    public const double PressureRadius = 2.2;

    /// <summary>Shots are only attempted from inside this range.</summary>
    public const double ShootingRange = 25.0;

    public const double ShotBaseSpeed = 20.0;
    public const double ShotSpeedPerPoint = 0.45;
    public const double PassBaseSpeed = 11.0;
    public const double PassSpeedPerPoint = 0.30;

    /// <summary>Ball speed lost per second while running loose.</summary>
    public const double BallDragPerSecond = 0.42;

    // Probabilities are expressed as "n in 1000" so every roll stays integer — no floating
    // point enters the random path, which keeps replay exactness independent of rounding.
    public const int ShotChancePerDecision = 3;
    public const int PassChancePerDecision = 95;
    public const int TackleBaseChance = 20;
    public const int InterceptionBaseChance = 40;

    /// <summary>How far a shot can stray, in metres, at the worst finishing rating.</summary>
    public const double ShotMaxSpray = 14.0;
}
