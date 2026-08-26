using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Match;

/// <summary>
/// A 4-4-2 shape, expressed as the anchor each selected player returns towards when it is not
/// chasing the ball. One formation is enough for MATCH-00; TACT-00 owns making this a choice.
/// </summary>
public static class Formation
{
    /// <summary>Slots in selection order: 1 goalkeeper, 4 defenders, 4 midfielders, 2 attackers.</summary>
    public static readonly IReadOnlyList<PitchLine> Slots =
    [
        PitchLine.Goalkeeper,
        PitchLine.Defence, PitchLine.Defence, PitchLine.Defence, PitchLine.Defence,
        PitchLine.Midfield, PitchLine.Midfield, PitchLine.Midfield, PitchLine.Midfield,
        PitchLine.Attack, PitchLine.Attack
    ];

    // Anchors for a team attacking towards increasing x, mirrored for the other direction.
    private static readonly Vec2[] AnchorsAttackingPositiveX =
    [
        new(5.25, Pitch.CentreY),

        new(21.0, 12.2), new(21.0, 26.5), new(21.0, 41.5), new(21.0, 55.8),

        new(47.25, 12.2), new(47.25, 26.5), new(47.25, 41.5), new(47.25, 55.8),

        new(71.4, 25.8), new(71.4, 42.2)
    ];

    public static Vec2 Anchor(int slot, bool attackingPositiveX)
    {
        var anchor = AnchorsAttackingPositiveX[slot];
        return attackingPositiveX ? anchor : new Vec2(Pitch.Length - anchor.X, anchor.Y);
    }

    /// <summary>Kick-off shape: the anchor, pulled back into the team's own half.</summary>
    public static Vec2 KickOffPosition(int slot, bool attackingPositiveX)
    {
        var anchor = Anchor(slot, attackingPositiveX);
        var ownHalfLimit = attackingPositiveX ? Pitch.Length / 2.0 : Pitch.Length / 2.0;

        var x = attackingPositiveX
            ? Math.Min(anchor.X, ownHalfLimit - 1.0)
            : Math.Max(anchor.X, ownHalfLimit + 1.0);

        return new Vec2(x, anchor.Y);
    }
}
