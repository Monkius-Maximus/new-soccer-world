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
    PlayerAttributes Attributes,
    PlayerName? Name = null)
{
    // Direct construction remains source-compatible for tests and editor tools. Production
    // persistence refuses a player without PlayerName instead of silently regenerating it.
    public string DisplayName => Name?.CommonName ?? $"{FirstName} {LastName}";

    public string FullName => Name?.FullName ?? $"{FirstName} {LastName}";

    /// <summary>The persisted short code for <see cref="Position"/>, e.g. "GK".</summary>
    public string PositionCode => Position.ToCode();

    public PitchLine Line => Position.Line();
}
