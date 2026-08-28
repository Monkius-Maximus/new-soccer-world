using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Match;

/// <summary>
/// A player's state inside one running match. Mutable, and owned exclusively by the
/// <see cref="MatchSimulation"/> that created it — nothing here is shared between matches, which
/// is what allows two matches to run at once without touching each other.
/// </summary>
public sealed class MatchPlayer
{
    public MatchPlayer(Player source, int clubId, int slot)
    {
        PlayerId = source.Id;
        ClubId = clubId;
        Slot = slot;
        Position = source.Position;
        Attributes = source.Attributes;
        IsGoalkeeper = slot == 0;
        Location = Vec2.Zero;
        Stamina = 1.0;
    }

    public int PlayerId { get; }
    public int ClubId { get; }
    public int Slot { get; }
    public PlayerPosition Position { get; }
    public PlayerAttributes Attributes { get; }
    public bool IsGoalkeeper { get; }

    public Vec2 Location { get; set; }

    /// <summary>Remaining condition, 1.0 at kick-off, scaling effective speed as it falls.</summary>
    public double Stamina { get; private set; }

    /// <summary>Metres per second at full condition, from Pace on the 1..20 scale.</summary>
    public double TopSpeed => 4.0 + (Attributes.Pace * 0.2);

    /// <summary>Speed after condition loss. Never drops below half, so nobody stops running.</summary>
    public double CurrentSpeed => TopSpeed * (0.5 + (0.5 * Stamina));

    /// <summary>
    /// Spends condition proportional to distance covered. A high Stamina rating drains slower,
    /// so the attribute shows up as a player still moving in the closing minutes.
    /// </summary>
    public void SpendStamina(double metresMoved)
    {
        if (metresMoved <= 0.0)
        {
            return;
        }

        var resistance = 1.0 + (Attributes.Stamina * 0.35);
        Stamina -= metresMoved / (9000.0 * resistance);

        if (Stamina < 0.0)
        {
            Stamina = 0.0;
        }
    }

    public void MoveTowards(Vec2 target, double seconds)
    {
        var before = Location;
        var moved = Location.MovedTowards(target, CurrentSpeed * seconds);
        Location = Pitch.Clamp(moved);
        SpendStamina(Location.DistanceTo(before));
    }
}
