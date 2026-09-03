namespace SoccerSim.Core.Domain;

/// <summary>
/// The complete persisted result of person-name generation. Loading a career never regenerates
/// these values from a newer content pack.
/// </summary>
public sealed record PlayerName(
    string GivenName,
    string? AdditionalGivenName,
    string FamilyName,
    string? AdditionalFamilyName,
    string FullName,
    string CommonName,
    string ShirtName,
    string ScoreboardName,
    string CultureId,
    string PackVersion,
    string AlgorithmVersion);
