namespace SoccerSim.Core.Tactics;

/// <summary>
/// How a club sets up. Like <see cref="Domain.PlayerAttributes"/>, every instruction here is on
/// a validated 1..20 scale and earns its place by changing a decision the match already makes:
/// <list type="table">
///   <item><term>DefensiveLineHeight</term><description>where the block sits without the ball</description></item>
///   <item><term>PressingIntensity</term><description>how many players chase, and from how far</description></item>
///   <item><term>Directness</term><description>how readily the ball goes forward or at goal</description></item>
/// </list>
/// Tempo, width, offside traps and set-piece routines are not here: nothing in the simulation
/// would read them yet, and an instruction that changes nothing is a lie in the UI.
/// </summary>
public sealed record TeamTactics
{
    public const int Minimum = 1;
    public const int Maximum = 20;

    public TeamTactics(
        FormationShape formation,
        int defensiveLineHeight,
        int pressingIntensity,
        int directness)
    {
        ArgumentNullException.ThrowIfNull(formation);

        Formation = formation;
        DefensiveLineHeight = Validated(defensiveLineHeight, nameof(defensiveLineHeight));
        PressingIntensity = Validated(pressingIntensity, nameof(pressingIntensity));
        Directness = Validated(directness, nameof(directness));
    }

    public FormationShape Formation { get; }
    public int DefensiveLineHeight { get; }
    public int PressingIntensity { get; }
    public int Directness { get; }

    /// <summary>A balanced 4-4-2, used when a club has no stored tactics of its own.</summary>
    public static TeamTactics Default { get; } = new(Formations.FourFourTwo, 10, 10, 10);

    private static int Validated(int value, string name) =>
        value is >= Minimum and <= Maximum
            ? value
            : throw new ArgumentOutOfRangeException(
                name, value, $"Tactical instructions must be between {Minimum} and {Maximum}.");

    public override string ToString() =>
        $"{Formation} (line {DefensiveLineHeight}, press {PressingIntensity}, direct {Directness})";
}
