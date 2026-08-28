namespace SoccerSim.Core.Match;

/// <summary>
/// A 2D point on the pitch, in metres.
/// <para>
/// Every operation here is restricted to +, -, *, / and sqrt, which IEEE-754 requires to be
/// correctly rounded. Transcendental functions (Sin, Cos, Atan2, Pow, Exp) are not covered by
/// that guarantee and can differ between platforms and runtime versions, so the simulation
/// avoids them entirely: direction comes from normalising a difference, and proximity from
/// comparing squared lengths. <c>DeterminismGuardTests</c> enforces it.
/// </para>
/// </summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0.0, 0.0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vec2 operator *(Vec2 a, double scalar) => new(a.X * scalar, a.Y * scalar);

    /// <summary>Squared length. Prefer this for comparisons — it needs no sqrt at all.</summary>
    public double LengthSquared => (X * X) + (Y * Y);

    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>
    /// Unit vector in the same direction, or <see cref="Zero"/> for a zero-length vector.
    /// Returning zero rather than NaN keeps a degenerate case from poisoning the whole tick.
    /// </summary>
    public Vec2 Normalised()
    {
        var lengthSquared = LengthSquared;
        if (lengthSquared <= 0.0)
        {
            return Zero;
        }

        var length = Math.Sqrt(lengthSquared);
        return new Vec2(X / length, Y / length);
    }

    public double DistanceSquaredTo(Vec2 other) => (this - other).LengthSquared;

    public double DistanceTo(Vec2 other) => (this - other).Length;

    /// <summary>Moves at most <paramref name="maxDistance"/> metres towards <paramref name="target"/>.</summary>
    public Vec2 MovedTowards(Vec2 target, double maxDistance)
    {
        var delta = target - this;
        var distanceSquared = delta.LengthSquared;
        if (distanceSquared <= maxDistance * maxDistance)
        {
            return target;
        }

        return this + (delta.Normalised() * maxDistance);
    }

    public override string ToString() => $"({X:F2}, {Y:F2})";
}
