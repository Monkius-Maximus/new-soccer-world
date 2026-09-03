using System.Text;
using System.Text.Json;
using SoccerSim.Core.Names;

namespace SoccerSim.Infrastructure.Names;

/// <summary>Strict, offline loader for a NameCulturePack v1 JSON document.</summary>
public static class NameCulturePackJsonLoader
{
    public static NameCulturePack Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    public static NameCulturePack Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var root = document.RootElement;
            RequireEqual(root, "schemaVersion", "1.0");
            RequireEqual(root, "normalization", "NFC");

            var given = RequiredProperty(root, "givenNames");
            var display = RequiredProperty(root, "footballDisplayRules");
            var limits = RequiredProperty(root, "limits");
            var pack = new NameCulturePack(
                RequiredString(root, "cultureId"),
                RequiredString(root, "packVersion"),
                Entries(RequiredProperty(given, "male"), "givenNames.male"),
                Entries(RequiredProperty(given, "female"), "givenNames.female"),
                Entries(RequiredProperty(given, "neutral"), "givenNames.neutral"),
                Entries(RequiredProperty(root, "familyNames"), "familyNames"),
                Entries(RequiredProperty(root, "fullNamePatterns"), "fullNamePatterns"),
                new NameDisplayRules(
                    Entries(RequiredProperty(display, "commonName"), "footballDisplayRules.commonName"),
                    Entries(RequiredProperty(display, "shirtName"), "footballDisplayRules.shirtName"),
                    Entries(RequiredProperty(display, "scoreboardName"), "footballDisplayRules.scoreboardName")),
                new NameLengthLimits(
                    PositiveInt(limits, "fullNameMax"),
                    PositiveInt(limits, "commonNameMax"),
                    PositiveInt(limits, "shirtNameMax"),
                    PositiveInt(limits, "scoreboardNameMax")));

            ValidateRequiredPools(pack);
            return pack;
        }
        catch (Exception error) when (error is JsonException
                                      or KeyNotFoundException
                                      or InvalidOperationException
                                      or FormatException
                                      or OverflowException
                                      or ArgumentException)
        {
            throw new InvalidDataException("Invalid NameCulturePack v1 JSON.", error);
        }
    }

    private static IReadOnlyList<WeightedNameValue> Entries(JsonElement element, string location)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{location} must be an array.");
        }

        var result = new List<WeightedNameValue>();
        foreach (var item in element.EnumerateArray())
        {
            string value;
            ulong weight;
            if (item.ValueKind == JsonValueKind.String)
            {
                value = item.GetString()!;
                weight = 1UL;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in item.EnumerateObject())
                {
                    if (property.Name is not ("value" or "weight"))
                    {
                        throw new InvalidDataException($"{location} contains unknown field '{property.Name}'.");
                    }
                }
                value = RequiredString(item, "value");
                weight = item.TryGetProperty("weight", out var weightElement)
                    ? PositiveUInt64(weightElement, $"{location}.weight")
                    : 1UL;
            }
            else
            {
                throw new InvalidDataException($"{location} entries must be strings or objects.");
            }

            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            {
                throw new InvalidDataException($"{location} contains an empty or padded value.");
            }
            if (!value.IsNormalized(NormalizationForm.FormC))
            {
                throw new InvalidDataException($"{location} contains text that is not Unicode NFC.");
            }
            result.Add(new WeightedNameValue(value, weight));
        }
        return result;
    }

    private static void ValidateRequiredPools(NameCulturePack pack)
    {
        if (pack.MaleGivenNames.Count + pack.FemaleGivenNames.Count + pack.NeutralGivenNames.Count == 0)
        {
            throw new InvalidDataException("At least one given-name pool must contain entries.");
        }
        if (pack.FamilyNames.Count == 0 || pack.FullNamePatterns.Count == 0
            || pack.DisplayRules.CommonName.Count == 0 || pack.DisplayRules.ShirtName.Count == 0
            || pack.DisplayRules.ScoreboardName.Count == 0)
        {
            throw new InvalidDataException("Family, pattern and display-rule pools must not be empty.");
        }
    }

    private static JsonElement RequiredProperty(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw new KeyNotFoundException($"Required property '{name}' is absent.");
        }
        return value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        var value = RequiredProperty(parent, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"'{name}' must be a non-empty string.");
        }
        return value.GetString()!;
    }

    private static void RequireEqual(JsonElement parent, string name, string expected)
    {
        var actual = RequiredString(parent, name);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{name}' must be '{expected}', not '{actual}'.");
        }
    }

    private static int PositiveInt(JsonElement parent, string name)
    {
        var value = RequiredProperty(parent, name);
        if (!value.TryGetInt32(out var number) || number <= 0)
        {
            throw new InvalidDataException($"'{name}' must be a positive Int32.");
        }
        return number;
    }

    private static ulong PositiveUInt64(JsonElement value, string location)
    {
        if (!value.TryGetUInt64(out var number) || number == 0)
        {
            throw new InvalidDataException($"'{location}' must be a positive UInt64.");
        }
        return number;
    }
}
