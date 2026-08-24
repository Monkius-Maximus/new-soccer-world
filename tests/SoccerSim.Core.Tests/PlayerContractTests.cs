using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Tests;

/// <summary>
/// PLYR-00: the position and attribute contract MATCH will consume. These tests pin the parts
/// a match simulation is entitled to rely on.
/// </summary>
public sealed class PlayerContractTests
{
    private static readonly PlayerPosition[] AllPositions = Enum.GetValues<PlayerPosition>();

    [Fact]
    public void Every_position_maps_to_a_code_and_back()
    {
        foreach (var position in AllPositions)
        {
            var code = position.ToCode();
            Assert.Equal(2, code.Length);
            Assert.Equal(position, PlayerPositions.Parse(code));
        }
    }

    [Fact]
    public void Position_codes_are_unique()
    {
        var codes = AllPositions.Select(position => position.ToCode()).ToArray();
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_position_belongs_to_exactly_one_line()
    {
        // Total mapping: adding a position without classifying it must not compile past this.
        foreach (var position in AllPositions)
        {
            Assert.Contains(position.Line(), Enum.GetValues<PitchLine>());
        }

        Assert.Equal(PitchLine.Goalkeeper, PlayerPosition.Goalkeeper.Line());
        Assert.Equal(PitchLine.Defence, PlayerPosition.CentreBack.Line());
        Assert.Equal(PitchLine.Midfield, PlayerPosition.CentralMidfielder.Line());
        Assert.Equal(PitchLine.Attack, PlayerPosition.Striker.Line());
    }

    [Fact]
    public void Only_the_goalkeeper_position_sits_on_the_goalkeeper_line()
    {
        var keepers = AllPositions.Where(position => position.Line() == PitchLine.Goalkeeper).ToArray();
        Assert.Equal([PlayerPosition.Goalkeeper], keepers);
    }

    [Fact]
    public void An_unknown_position_code_is_rejected_rather_than_guessed()
    {
        Assert.Throws<ArgumentException>(() => PlayerPositions.Parse("XX"));
        Assert.Throws<ArgumentException>(() => PlayerPositions.Parse("gk"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Attributes_outside_the_scale_are_rejected(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlayerAttributes(value, 10, 10, 10, 10, 10, 10, 10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlayerAttributes(10, 10, 10, 10, 10, 10, 10, 10, value));
    }

    [Fact]
    public void The_scale_bounds_are_accepted()
    {
        var floor = PlayerAttributes.Minimum;
        var ceiling = PlayerAttributes.Maximum;

        _ = new PlayerAttributes(floor, floor, floor, floor, floor, floor, floor, floor, floor);
        _ = new PlayerAttributes(ceiling, ceiling, ceiling, ceiling, ceiling, ceiling, ceiling, ceiling, ceiling);
    }

    [Fact]
    public void Each_attribute_is_bound_to_its_own_slot()
    {
        // Guards against a constructor that silently reorders arguments.
        var attributes = new PlayerAttributes(1, 2, 3, 4, 5, 6, 7, 8, 9);

        Assert.Equal(1, attributes.Pace);
        Assert.Equal(2, attributes.Stamina);
        Assert.Equal(3, attributes.Strength);
        Assert.Equal(4, attributes.Passing);
        Assert.Equal(5, attributes.Shooting);
        Assert.Equal(6, attributes.Tackling);
        Assert.Equal(7, attributes.Dribbling);
        Assert.Equal(8, attributes.Positioning);
        Assert.Equal(9, attributes.Goalkeeping);
    }

    [Fact]
    public void A_player_reports_its_own_position_code_and_line()
    {
        var player = new Player(
            1, 1, 1, "Caio", "Alencar", new DateOnly(2000, 1, 1), 1,
            PlayerPosition.CentreBack,
            new PlayerAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10));

        Assert.Equal("CB", player.PositionCode);
        Assert.Equal(PitchLine.Defence, player.Line);
        Assert.Equal("Caio Alencar", player.DisplayName);
    }
}
