using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;

namespace SoccerSim.Core.Tactics;

/// <summary>One position in a shape: which line it belongs to, what it does, and where it lives.</summary>
/// <param name="Anchor">Resting position for a team attacking towards increasing x.</param>
public sealed record FormationSlot(PitchLine Line, PlayerRole Role, Vec2 Anchor);

/// <summary>
/// A named shape. Formations are data rather than a constant, which is the whole point of
/// TACT-00: before it, every club in the world played the same 4-4-2 because the anchors were
/// hard-coded.
/// </summary>
public sealed record FormationShape
{
    public const int PlayersOnPitch = 11;

    public FormationShape(string name, IReadOnlyList<FormationSlot> slots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(slots);

        if (slots.Count != PlayersOnPitch)
        {
            throw new ArgumentException(
                $"A formation needs exactly {PlayersOnPitch} slots, but '{name}' has {slots.Count}.", nameof(slots));
        }

        var keepers = slots.Count(slot => slot.Line == PitchLine.Goalkeeper);
        if (keepers != 1)
        {
            throw new ArgumentException(
                $"A formation needs exactly one goalkeeper, but '{name}' has {keepers}.", nameof(slots));
        }

        if (slots[0].Line != PitchLine.Goalkeeper)
        {
            throw new ArgumentException($"Slot 0 must be the goalkeeper in '{name}'.", nameof(slots));
        }

        Name = name;
        Slots = slots;
    }

    public string Name { get; }
    public IReadOnlyList<FormationSlot> Slots { get; }

    public int CountOf(PitchLine line) => Slots.Count(slot => slot.Line == line);

    /// <summary>Resting anchor for a slot, mirrored for a team attacking towards decreasing x.</summary>
    public Vec2 Anchor(int slot, bool attackingPositiveX)
    {
        var anchor = Slots[slot].Anchor;
        return attackingPositiveX ? anchor : new Vec2(Pitch.Length - anchor.X, anchor.Y);
    }

    public override string ToString() => Name;
}
