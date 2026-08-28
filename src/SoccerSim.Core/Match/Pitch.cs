namespace SoccerSim.Core.Match;

/// <summary>
/// Pitch geometry in metres, using a corner origin: x runs 0..105 along the length, y runs
/// 0..68 across it. The home goal sits at x = 0 and the away goal at x = 105.
/// </summary>
public static class Pitch
{
    public const double Length = 105.0;
    public const double Width = 68.0;

    public const double GoalWidth = 7.32;
    public const double CentreY = Width / 2.0;
    public const double GoalMinY = CentreY - (GoalWidth / 2.0);
    public const double GoalMaxY = CentreY + (GoalWidth / 2.0);

    public static readonly Vec2 Centre = new(Length / 2.0, CentreY);

    /// <summary>The goal a team attacks when it is playing towards increasing x.</summary>
    public static Vec2 GoalCentre(bool attackingPositiveX) =>
        new(attackingPositiveX ? Length : 0.0, CentreY);

    public static bool IsInsidePlay(Vec2 position) =>
        position.X >= 0.0 && position.X <= Length && position.Y >= 0.0 && position.Y <= Width;

    /// <summary>Clamps a position back onto the pitch, used to keep players in bounds.</summary>
    public static Vec2 Clamp(Vec2 position) => new(
        Math.Clamp(position.X, 0.0, Length),
        Math.Clamp(position.Y, 0.0, Width));

    /// <summary>
    /// Whether the segment from <paramref name="from"/> to <paramref name="to"/> crosses a goal
    /// line between the posts.
    /// <para>
    /// Tested as a segment rather than by sampling the ball's position each tick. A shot travels
    /// several metres per tick, so point sampling would let fast shots tunnel straight through
    /// the goal line — a bug that gets worse the harder the shot.
    /// </para>
    /// </summary>
    public static bool CrossesGoalLine(Vec2 from, Vec2 to, bool positiveXGoal)
    {
        var line = positiveXGoal ? Length : 0.0;

        var crossing = positiveXGoal
            ? from.X < line && to.X >= line
            : from.X > line && to.X <= line;

        if (!crossing)
        {
            return false;
        }

        var dx = to.X - from.X;
        if (dx == 0.0)
        {
            return false;
        }

        // Linear interpolation to the exact crossing point: division only, no trigonometry.
        var t = (line - from.X) / dx;
        var y = from.Y + ((to.Y - from.Y) * t);

        return y >= GoalMinY && y <= GoalMaxY;
    }
}
