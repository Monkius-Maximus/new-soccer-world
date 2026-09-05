using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;

namespace SoccerSim.Core.Tactics;

/// <summary>
/// The shapes a club can pick. Three real formations, enough for clubs to play differently and
/// for the simulation to show it; adding a fourth is data, not code.
/// </summary>
public static class Formations
{
    private static Vec2 At(double x, double y) => new(x, y);

    public static readonly FormationShape FourFourTwo = new("4-4-2",
    [
        new(PitchLine.Goalkeeper, PlayerRole.Goalkeeper, At(5.25, 34.0)),

        new(PitchLine.Defence, PlayerRole.FullBack, At(21.0, 12.2)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(19.0, 26.5)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(19.0, 41.5)),
        new(PitchLine.Defence, PlayerRole.FullBack, At(21.0, 55.8)),

        new(PitchLine.Midfield, PlayerRole.Winger, At(47.25, 12.2)),
        new(PitchLine.Midfield, PlayerRole.CentralMidfielder, At(45.0, 26.5)),
        new(PitchLine.Midfield, PlayerRole.HoldingMidfielder, At(43.0, 41.5)),
        new(PitchLine.Midfield, PlayerRole.Winger, At(47.25, 55.8)),

        new(PitchLine.Attack, PlayerRole.Forward, At(71.4, 28.0)),
        new(PitchLine.Attack, PlayerRole.Forward, At(71.4, 40.0))
    ]);

    public static readonly FormationShape FourThreeThree = new("4-3-3",
    [
        new(PitchLine.Goalkeeper, PlayerRole.Goalkeeper, At(5.25, 34.0)),

        new(PitchLine.Defence, PlayerRole.FullBack, At(22.0, 11.0)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(19.0, 27.0)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(19.0, 41.0)),
        new(PitchLine.Defence, PlayerRole.FullBack, At(22.0, 57.0)),

        new(PitchLine.Midfield, PlayerRole.HoldingMidfielder, At(40.0, 34.0)),
        new(PitchLine.Midfield, PlayerRole.CentralMidfielder, At(48.0, 22.0)),
        new(PitchLine.Midfield, PlayerRole.AttackingMidfielder, At(52.0, 46.0)),

        new(PitchLine.Attack, PlayerRole.Winger, At(74.0, 12.0)),
        new(PitchLine.Attack, PlayerRole.Forward, At(78.0, 34.0)),
        new(PitchLine.Attack, PlayerRole.Winger, At(74.0, 56.0))
    ]);

    public static readonly FormationShape FiveThreeTwo = new("5-3-2",
    [
        new(PitchLine.Goalkeeper, PlayerRole.Goalkeeper, At(5.25, 34.0)),

        new(PitchLine.Defence, PlayerRole.FullBack, At(24.0, 9.0)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(17.0, 22.0)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(16.0, 34.0)),
        new(PitchLine.Defence, PlayerRole.CentreBack, At(17.0, 46.0)),
        new(PitchLine.Defence, PlayerRole.FullBack, At(24.0, 59.0)),

        new(PitchLine.Midfield, PlayerRole.CentralMidfielder, At(45.0, 20.0)),
        new(PitchLine.Midfield, PlayerRole.HoldingMidfielder, At(41.0, 34.0)),
        new(PitchLine.Midfield, PlayerRole.CentralMidfielder, At(45.0, 48.0)),

        new(PitchLine.Attack, PlayerRole.Forward, At(70.0, 28.0)),
        new(PitchLine.Attack, PlayerRole.Forward, At(70.0, 40.0))
    ]);

    public static readonly IReadOnlyList<FormationShape> All = [FourFourTwo, FourThreeThree, FiveThreeTwo];

    public static FormationShape Parse(string name) =>
        All.FirstOrDefault(shape => string.Equals(shape.Name, name, StringComparison.Ordinal))
        ?? throw new ArgumentException($"Unknown formation '{name}'.", nameof(name));
}
