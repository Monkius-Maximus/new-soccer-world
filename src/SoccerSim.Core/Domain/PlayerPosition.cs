namespace SoccerSim.Core.Domain;

/// <summary>
/// The ten positions the Foundation seed uses. A closed set rather than a free string, so a
/// typo becomes a load failure instead of a player the match simulation silently ignores.
/// </summary>
public enum PlayerPosition
{
    Goalkeeper,
    RightBack,
    CentreBack,
    LeftBack,
    DefensiveMidfielder,
    CentralMidfielder,
    AttackingMidfielder,
    RightWinger,
    LeftWinger,
    Striker
}

/// <summary>
/// The band of the pitch a position belongs to. MATCH needs this coarse grouping long before
/// it needs formations or roles; TACT will layer those on top rather than replace it.
/// </summary>
public enum PitchLine
{
    Goalkeeper,
    Defence,
    Midfield,
    Attack
}

public static class PlayerPositions
{
    /// <summary>The persisted short code, e.g. "GK". This is the only spelling storage uses.</summary>
    public static string ToCode(this PlayerPosition position) => position switch
    {
        PlayerPosition.Goalkeeper => "GK",
        PlayerPosition.RightBack => "RB",
        PlayerPosition.CentreBack => "CB",
        PlayerPosition.LeftBack => "LB",
        PlayerPosition.DefensiveMidfielder => "DM",
        PlayerPosition.CentralMidfielder => "CM",
        PlayerPosition.AttackingMidfielder => "AM",
        PlayerPosition.RightWinger => "RW",
        PlayerPosition.LeftWinger => "LW",
        PlayerPosition.Striker => "ST",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unmapped position.")
    };

    public static PitchLine Line(this PlayerPosition position) => position switch
    {
        PlayerPosition.Goalkeeper => PitchLine.Goalkeeper,
        PlayerPosition.RightBack or PlayerPosition.CentreBack or PlayerPosition.LeftBack => PitchLine.Defence,
        PlayerPosition.DefensiveMidfielder or PlayerPosition.CentralMidfielder or PlayerPosition.AttackingMidfielder
            => PitchLine.Midfield,
        PlayerPosition.RightWinger or PlayerPosition.LeftWinger or PlayerPosition.Striker => PitchLine.Attack,
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unmapped position.")
    };

    public static PlayerPosition Parse(string code) => code switch
    {
        "GK" => PlayerPosition.Goalkeeper,
        "RB" => PlayerPosition.RightBack,
        "CB" => PlayerPosition.CentreBack,
        "LB" => PlayerPosition.LeftBack,
        "DM" => PlayerPosition.DefensiveMidfielder,
        "CM" => PlayerPosition.CentralMidfielder,
        "AM" => PlayerPosition.AttackingMidfielder,
        "RW" => PlayerPosition.RightWinger,
        "LW" => PlayerPosition.LeftWinger,
        "ST" => PlayerPosition.Striker,
        _ => throw new ArgumentException($"Unknown position code '{code}'.", nameof(code))
    };
}
