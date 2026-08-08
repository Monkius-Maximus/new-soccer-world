namespace SoccerSim.Core.Domain;

public sealed record Player(
    int Id,
    int CountryId,
    int ClubId,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    int SquadNumber,
    string PositionCode);
