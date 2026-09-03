using SoccerSim.Core.Names;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Tests;

public sealed class NameGeneratorTests
{
    [Fact]
    public void Named_stream_derivation_matches_the_language_neutral_golden_vector()
    {
        var parameters = NamedRandomStreams.Derive(0UL, 0UL, "Names.Given.Primary.v1");

        Assert.Equal(0x00ccd0f38e7db4dfUL, parameters.Seed);
        Assert.Equal(0xde7a50ea43bd1f6bUL, parameters.Sequence);

        var random = new Pcg32Random(parameters.Seed, parameters.Sequence);
        Assert.Equal(
            new uint[] { 0xe4ad9c35U, 0x6d9fd34fU, 0xbf08b224U, 0xd4391456U, 0x4741e13fU },
            Enumerable.Range(0, 5).Select(_ => random.NextUInt32()));
    }

    [Fact]
    public void Minimal_pack_generation_matches_the_language_neutral_golden_vector()
    {
        var result = new NameGenerator(MinimalPack()).Generate(
            new NameGenerationRequest(20260903UL, 1UL, NameGender.Male));

        Assert.Equal("Éder", result.GivenName);
        Assert.Equal("Caio", result.AdditionalGivenName);
        Assert.Equal("Alencar", result.FamilyName);
        Assert.Null(result.AdditionalFamilyName);
        Assert.Equal("Éder Caio Alencar", result.FullName);
        Assert.Equal("Éder", result.CommonName);
        Assert.Equal("ÉDER", result.ShirtName);
        Assert.Equal("ÉDER", result.ScoreboardName);
        Assert.Equal("pt-BR", result.CultureId);
        Assert.Equal("0.0.0", result.PackVersion);
        Assert.Equal(NameGenerator.AlgorithmVersion, result.AlgorithmVersion);
    }

    [Fact]
    public void A_pattern_change_does_not_shift_primary_given_or_family_streams()
    {
        var original = MinimalPack();
        var changed = original with
        {
            FullNamePatterns =
            [
                .. original.FullNamePatterns,
                new WeightedNameValue("{given} {given2} {family} {family2}", 999UL)
            ]
        };
        var request = new NameGenerationRequest(20260903UL, 42UL, NameGender.Male);

        var before = new NameGenerator(original).Generate(request);
        var after = new NameGenerator(changed).Generate(request);

        Assert.Equal(before.GivenName, after.GivenName);
        Assert.Equal(before.FamilyName, after.FamilyName);
    }

    [Fact]
    public void An_empty_requested_pool_fails_instead_of_borrowing_from_another_pool()
    {
        var failure = Assert.Throws<NameGenerationException>(() =>
            new NameGenerator(MinimalPack()).Generate(
                new NameGenerationRequest(1UL, 1UL, NameGender.Neutral)));

        Assert.Contains("no fallback", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presentation_fields_fail_instead_of_truncating()
    {
        var pack = MinimalPack() with
        {
            MaleGivenNames = [new WeightedNameValue("NomeMuitoLongo")],
            FamilyNames = [new WeightedNameValue("SobrenomeMuitoLongo")],
            Limits = new NameLengthLimits(80, 24, 3, 18)
        };

        var failure = Assert.Throws<NameGenerationException>(() =>
            new NameGenerator(pack).Generate(
                new NameGenerationRequest(1UL, 1UL, NameGender.Male)));

        Assert.Contains("without truncation", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shirt_collisions_are_resolved_inside_the_roster_and_are_stable()
    {
        var first = Name("Caio", "Silva", "SILVA");
        var second = Name("Davi", "Silva", "SILVA");
        var roster = new[] { new RosterNameEntry(20UL, second), new RosterNameEntry(10UL, first) };

        var resolved = RosterNameDisambiguator.ResolveForPersistence(roster);
        var repeated = RosterNameDisambiguator.ResolveForPersistence(roster);

        Assert.Equal("C SILVA", resolved[10UL].ShirtName);
        Assert.Equal("D SILVA", resolved[20UL].ShirtName);
        Assert.Equal(resolved.OrderBy(pair => pair.Key), repeated.OrderBy(pair => pair.Key));
        Assert.Equal(2, resolved.Values.Select(name => name.ShirtName).Distinct().Count());
    }

    [Fact]
    public void An_unresolvable_roster_collision_requires_an_editorial_override()
    {
        var identical = Enumerable.Range(1, 8)
            .Select(id => new RosterNameEntry((ulong)id, Name("Caio", "Silva", "SILVA")))
            .ToArray();

        var failure = Assert.Throws<NameGenerationException>(() =>
            RosterNameDisambiguator.ResolveForPersistence(identical));

        Assert.Contains("editorial override", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SoccerSim.Core.Domain.PlayerName Name(string given, string family, string shirt) =>
        new(
            given,
            null,
            family,
            null,
            $"{given} {family}",
            family,
            shirt,
            shirt,
            "pt-BR",
            "0.0.0",
            NameGenerator.AlgorithmVersion);

    private static NameCulturePack MinimalPack() => new(
        "pt-BR",
        "0.0.0",
        [
            new WeightedNameValue("Caio", 4UL),
            new WeightedNameValue("Davi", 3UL),
            new WeightedNameValue("João"),
            new WeightedNameValue("Éder")
        ],
        [new WeightedNameValue("Ana"), new WeightedNameValue("Beatriz"), new WeightedNameValue("Íris")],
        [],
        [
            new WeightedNameValue("Alencar", 4UL),
            new WeightedNameValue("Barros", 3UL),
            new WeightedNameValue("Silva"),
            new WeightedNameValue("Sá")
        ],
        [
            new WeightedNameValue("{given} {family}", 55UL),
            new WeightedNameValue("{given} {family} {family2}", 30UL),
            new WeightedNameValue("{given} {given2} {family}", 15UL)
        ],
        new NameDisplayRules(
            [
                new WeightedNameValue("{given}", 50UL),
                new WeightedNameValue("{family}", 40UL),
                new WeightedNameValue("{given} {family}", 10UL)
            ],
            [new WeightedNameValue("{common}", 90UL), new WeightedNameValue("{family}", 10UL)],
            [new WeightedNameValue("{common}", 80UL), new WeightedNameValue("{family}", 20UL)]),
        new NameLengthLimits(80, 24, 16, 18));
}
