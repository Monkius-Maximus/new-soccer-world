namespace SoccerSim.Core.Names;

public enum NameGender
{
    Male,
    Female,
    Neutral
}

public sealed record WeightedNameValue(string Value, ulong Weight = 1UL);

public sealed record NameDisplayRules(
    IReadOnlyList<WeightedNameValue> CommonName,
    IReadOnlyList<WeightedNameValue> ShirtName,
    IReadOnlyList<WeightedNameValue> ScoreboardName);

public sealed record NameLengthLimits(
    int FullNameMax,
    int CommonNameMax,
    int ShirtNameMax,
    int ScoreboardNameMax);

public sealed record NameCulturePack(
    string CultureId,
    string PackVersion,
    IReadOnlyList<WeightedNameValue> MaleGivenNames,
    IReadOnlyList<WeightedNameValue> FemaleGivenNames,
    IReadOnlyList<WeightedNameValue> NeutralGivenNames,
    IReadOnlyList<WeightedNameValue> FamilyNames,
    IReadOnlyList<WeightedNameValue> FullNamePatterns,
    NameDisplayRules DisplayRules,
    NameLengthLimits Limits)
{
    public IReadOnlyList<WeightedNameValue> GetGivenNames(NameGender gender) => gender switch
    {
        NameGender.Male => MaleGivenNames,
        NameGender.Female => FemaleGivenNames,
        NameGender.Neutral => NeutralGivenNames,
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null)
    };
}

public readonly record struct NameGenerationRequest(
    ulong WorldSeed,
    ulong PersonId,
    NameGender Gender);

public sealed class NameGenerationException(string message) : InvalidOperationException(message);
