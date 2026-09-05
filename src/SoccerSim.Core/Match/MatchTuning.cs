namespace SoccerSim.Core.Match;

/// <summary>
/// Every number the match balance depends on, in one place.
/// <para>
/// These are tuned against observed output, not guessed. Measured over 200 matches per setup,
/// between identically-rated squads, so any difference is tactical rather than ability:
/// <code>
///                             goals  shots  off  blocked  saved
/// balanced vs balanced         3.29   29.4  9.7      0.1   16.3
/// 4-3-3 press vs 5-3-2 block   3.14   34.5 10.5      2.2   16.7   (2.29 - 0.84)
/// high press vs low block      3.39   29.4  9.5      0.1   16.3   (2.15 - 1.24)
/// </code>
/// Two things worth reading off that table: the settings decide matches between identical
/// squads, and the shot column accounts for itself — every shot is off target, blocked, saved
/// or a goal.
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

    /// <summary>How far the shape slides towards the ball, and the cap on that slide.</summary>
    public const double ShapeDriftFactor = 0.30;
    public const double MaxShapeDrift = 11.0;

    /// <summary>Outfielders hold shape no closer than this to a goal line.</summary>
    public const double OutfieldGoalLineMargin = 5.0;

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

    /// <summary>How far a pressing side will send a second chaser after the ball.</summary>
    public const double PressRadiusBase = 8.0;
    public const double PressRadiusPerIntensity = 0.9;

    /// <summary>Pressing intensity at or above which a second player leaves the shape.</summary>
    public const int SecondPresserThreshold = 12;

    /// <summary>How much a point of directness widens shooting range and willingness.</summary>
    public const double ShootingRangePerDirectness = 0.7;
    public const int ShotChancePerDirectness = 1;

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
