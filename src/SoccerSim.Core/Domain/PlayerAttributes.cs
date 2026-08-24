namespace SoccerSim.Core.Domain;

/// <summary>
/// The minimum a player needs for a match to be simulable.
/// <para>
/// Every attribute here earns its place by feeding a decision MATCH-00 has to make. Nothing is
/// included because a football game "usually has it": no overall rating, no form, no morale, no
/// hidden mentals. Those belong to milestones that exist, and a derived rating in particular is
/// a presentation concern, not stored state.
/// </para>
/// <list type="table">
///   <item><term>Pace</term><description>who reaches a loose ball first</description></item>
///   <item><term>Stamina</term><description>how much of the above survives to the 90th minute</description></item>
///   <item><term>Strength</term><description>physical duels and holding the ball up</description></item>
///   <item><term>Passing</term><description>whether an attempted pass finds its target</description></item>
///   <item><term>Shooting</term><description>whether a shot troubles the goal</description></item>
///   <item><term>Tackling</term><description>the defending half of a challenge</description></item>
///   <item><term>Dribbling</term><description>the attacking half of a take-on</description></item>
///   <item><term>Positioning</term><description>off-ball decision quality, both phases</description></item>
///   <item><term>Goalkeeping</term><description>shot stopping; outfielders simply rate low</description></item>
/// </list>
/// </summary>
public sealed record PlayerAttributes
{
    /// <summary>Inclusive lower bound of the attribute scale.</summary>
    public const int Minimum = 1;

    /// <summary>Inclusive upper bound of the attribute scale.</summary>
    public const int Maximum = 20;

    public PlayerAttributes(
        int pace,
        int stamina,
        int strength,
        int passing,
        int shooting,
        int tackling,
        int dribbling,
        int positioning,
        int goalkeeping)
    {
        Pace = Validated(pace, nameof(pace));
        Stamina = Validated(stamina, nameof(stamina));
        Strength = Validated(strength, nameof(strength));
        Passing = Validated(passing, nameof(passing));
        Shooting = Validated(shooting, nameof(shooting));
        Tackling = Validated(tackling, nameof(tackling));
        Dribbling = Validated(dribbling, nameof(dribbling));
        Positioning = Validated(positioning, nameof(positioning));
        Goalkeeping = Validated(goalkeeping, nameof(goalkeeping));
    }

    public int Pace { get; }
    public int Stamina { get; }
    public int Strength { get; }
    public int Passing { get; }
    public int Shooting { get; }
    public int Tackling { get; }
    public int Dribbling { get; }
    public int Positioning { get; }
    public int Goalkeeping { get; }

    private static int Validated(int value, string name) =>
        value is >= Minimum and <= Maximum
            ? value
            : throw new ArgumentOutOfRangeException(
                name, value, $"Attributes must be between {Minimum} and {Maximum}.");
}
