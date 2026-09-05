namespace SoccerSim.Core.Tactics;

/// <summary>
/// The job a slot does. Roles exist to change behaviour, not to be a taxonomy: each one is
/// defined entirely by <see cref="Roles.Behaviour"/>, and a role that moves no number would
/// be decoration.
/// </summary>
public enum PlayerRole
{
    Goalkeeper,
    CentreBack,
    FullBack,
    HoldingMidfielder,
    CentralMidfielder,
    AttackingMidfielder,
    Winger,
    Forward
}

/// <summary>
/// How far a slot travels between the two phases, in metres along its attacking direction.
/// <para>
/// <paramref name="AttackingPush"/> is added to the anchor when the team has the ball;
/// <paramref name="DefensiveDrop"/> is subtracted when it does not. A full-back pushing 12 m
/// and dropping 4 m is what "overlapping full-back" means to the simulation — there is no
/// separate role system behind the name.
/// </para>
/// </summary>
public readonly record struct RoleBehaviour(double AttackingPush, double DefensiveDrop, bool Presses);

public static class Roles
{
    public static RoleBehaviour Behaviour(this PlayerRole role) => role switch
    {
        // The keeper holds his line in both phases; his positioning is handled separately.
        PlayerRole.Goalkeeper => new(0.0, 0.0, false),

        PlayerRole.CentreBack => new(4.0, 6.0, false),
        PlayerRole.FullBack => new(12.0, 4.0, false),

        PlayerRole.HoldingMidfielder => new(5.0, 8.0, true),
        PlayerRole.CentralMidfielder => new(9.0, 7.0, true),
        PlayerRole.AttackingMidfielder => new(12.0, 10.0, true),

        PlayerRole.Winger => new(14.0, 12.0, true),
        PlayerRole.Forward => new(10.0, 14.0, false),

        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unmapped role.")
    };
}
