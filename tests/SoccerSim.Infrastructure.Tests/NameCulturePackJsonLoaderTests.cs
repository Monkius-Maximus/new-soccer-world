using SoccerSim.Core.Names;
using SoccerSim.Infrastructure.Names;

namespace SoccerSim.Infrastructure.Tests;

public sealed class NameCulturePackJsonLoaderTests
{
    private const string MinimalJson = """
        {
          "schemaVersion": "1.0",
          "cultureId": "pt-BR",
          "packVersion": "0.0.0",
          "status": "prototype",
          "normalization": "NFC",
          "sourceManifest": "test-fixture",
          "givenNames": {
            "male": [{"value":"Caio","weight":4}, "Davi", "João"],
            "female": ["Ana"],
            "neutral": []
          },
          "familyNames": [{"value":"Alencar","weight":3}, "Barros", "Sá"],
          "fullNamePatterns": [{"value":"{given} {family}","weight":1}],
          "footballDisplayRules": {
            "commonName": [{"value":"{family}","weight":1}],
            "shirtName": [{"value":"{common}","weight":1}],
            "scoreboardName": [{"value":"{common}","weight":1}]
          },
          "limits": {
            "fullNameMax": 80,
            "commonNameMax": 24,
            "shirtNameMax": 16,
            "scoreboardNameMax": 18
          }
        }
        """;

    [Fact]
    public void Loader_accepts_string_and_weighted_object_entries()
    {
        var pack = NameCulturePackJsonLoader.Parse(MinimalJson);

        Assert.Equal("pt-BR", pack.CultureId);
        Assert.Equal(3, pack.MaleGivenNames.Count);
        Assert.Equal(4UL, pack.MaleGivenNames[0].Weight);
        Assert.Equal("João", pack.MaleGivenNames[2].Value);
        Assert.Empty(pack.NeutralGivenNames);
    }

    [Fact]
    public void Loader_rejects_a_non_positive_weight()
    {
        var invalid = MinimalJson.Replace(
            "\"weight\":4",
            "\"weight\":0",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => NameCulturePackJsonLoader.Parse(invalid));
    }

    [Fact]
    public void Loaded_pack_can_drive_the_core_generator_without_network_access()
    {
        var pack = NameCulturePackJsonLoader.Parse(MinimalJson);
        var result = new NameGenerator(pack).Generate(
            new NameGenerationRequest(7UL, 12UL, NameGender.Male));

        Assert.Equal("pt-BR", result.CultureId);
        Assert.Equal(NameGenerator.AlgorithmVersion, result.AlgorithmVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.FullName));
    }
}
