namespace SoccerSim.Core.Domain;

public sealed record Player(
    int Id,
    int CountryId,
    int ClubId,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    int SquadNumber,
    PlayerPosition Position,
    PlayerAttributes Attributes)
{
    public string DisplayName => $"{FirstName} {LastName}";

    /// <summary>The persisted short code for <see cref="Position"/>, e.g. "GK".</summary>
    public string PositionCode => Position.ToCode();

    public PitchLine Line => Position.Line();
}
