using System.Text;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Names;

/// <summary>Deterministic NMG-002 person-name generator.</summary>
public sealed class NameGenerator(NameCulturePack pack)
{
    public const string AlgorithmVersion = "tp-names-pcg32-streams-v1";
    private const int MaximumDistinctAttempts = 32;

    private static class Streams
    {
        public const string FullPattern = "Names.Full.Pattern.v1";
        public const string GivenPrimary = "Names.Given.Primary.v1";
        public const string GivenSecondary = "Names.Given.Secondary.v1";
        public const string FamilyPrimary = "Names.Family.Primary.v1";
        public const string FamilySecondary = "Names.Family.Secondary.v1";
        public const string CommonPattern = "Names.Common.Pattern.v1";
        public const string ShirtPattern = "Names.Shirt.Pattern.v1";
        public const string ScoreboardPattern = "Names.Scoreboard.Pattern.v1";
    }

    public PlayerName Generate(NameGenerationRequest request)
    {
        var givenPool = pack.GetGivenNames(request.Gender);
        if (givenPool.Count == 0 || pack.FamilyNames.Count == 0)
        {
            throw new NameGenerationException(
                "The requested culture pool is empty; no fallback to another culture or gender is permitted.");
        }

        var fullPattern = Choose(pack.FullNamePatterns, Stream(request, Streams.FullPattern));
        var given = Choose(givenPool, Stream(request, Streams.GivenPrimary));
        string? given2 = null;
        if (fullPattern.Contains("{given2}", StringComparison.Ordinal))
        {
            given2 = ChooseDistinct(
                givenPool,
                Stream(request, Streams.GivenSecondary),
                new HashSet<string>(StringComparer.Ordinal) { ComparisonKey(given) });
        }

        var family = ChooseDistinct(
            pack.FamilyNames,
            Stream(request, Streams.FamilyPrimary),
            new HashSet<string>(StringComparer.Ordinal)
            {
                ComparisonKey(given),
                ComparisonKey(given2 ?? string.Empty)
            });
        string? family2 = null;
        if (fullPattern.Contains("{family2}", StringComparison.Ordinal))
        {
            family2 = ChooseDistinct(
                pack.FamilyNames,
                Stream(request, Streams.FamilySecondary),
                new HashSet<string>(StringComparer.Ordinal)
                {
                    ComparisonKey(given),
                    ComparisonKey(given2 ?? string.Empty),
                    ComparisonKey(family)
                });
        }

        var fullName = Render(fullPattern, given, given2, family, family2, null);
        var commonPattern = Choose(pack.DisplayRules.CommonName, Stream(request, Streams.CommonPattern));
        var commonName = Render(commonPattern, given, given2, family, family2, null);

        var shirtPattern = Choose(pack.DisplayRules.ShirtName, Stream(request, Streams.ShirtPattern));
        var shirtSelected = Render(shirtPattern, given, given2, family, family2, commonName);
        var shirtName = PresentationValue(
            shirtSelected, [family, given], pack.Limits.ShirtNameMax, "shirtName");

        var scoreboardPattern = Choose(
            pack.DisplayRules.ScoreboardName,
            Stream(request, Streams.ScoreboardPattern));
        var scoreboardSelected = Render(
            scoreboardPattern, given, given2, family, family2, commonName);
        var scoreboardName = PresentationValue(
            scoreboardSelected, [family, given], pack.Limits.ScoreboardNameMax, "scoreboardName");

        EnforceLimit(fullName, pack.Limits.FullNameMax, "fullName");
        EnforceLimit(commonName, pack.Limits.CommonNameMax, "commonName");

        return new PlayerName(
            given,
            given2,
            family,
            family2,
            fullName,
            commonName,
            shirtName,
            scoreboardName,
            pack.CultureId,
            pack.PackVersion,
            AlgorithmVersion);
    }

    private static Pcg32Random Stream(NameGenerationRequest request, string streamName) =>
        NamedRandomStreams.Create(request.WorldSeed, request.PersonId, streamName);

    private static string Choose(IReadOnlyList<WeightedNameValue> entries, IRandomSource random)
    {
        if (entries.Count == 0)
        {
            throw new NameGenerationException("A weighted pool is empty.");
        }

        ulong total = 0;
        foreach (var entry in entries)
        {
            if (entry.Weight == 0)
            {
                throw new NameGenerationException("A weighted pool contains a zero weight.");
            }
            try
            {
                total = checked(total + entry.Weight);
            }
            catch (OverflowException error)
            {
                throw new NameGenerationException($"Weighted pool exceeds UInt64: {error.Message}");
            }
        }

        var target = NextBelow(random, total);
        ulong cursor = 0;
        foreach (var entry in entries)
        {
            cursor += entry.Weight;
            if (target < cursor)
            {
                return RequiredNfc(entry.Value, "name entry");
            }
        }
        throw new NameGenerationException("Weighted selection exhausted after a valid target.");
    }

    private static string ChooseDistinct(
        IReadOnlyList<WeightedNameValue> entries,
        IRandomSource random,
        IReadOnlySet<string> forbidden)
    {
        for (var attempt = 0; attempt < MaximumDistinctAttempts; attempt++)
        {
            var value = Choose(entries, random);
            if (!forbidden.Contains(ComparisonKey(value)))
            {
                return value;
            }
        }
        throw new NameGenerationException("Distinct selection exhausted after 32 attempts.");
    }

    private static ulong NextBelow(IRandomSource random, ulong upperExclusive)
    {
        if (upperExclusive == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(upperExclusive));
        }

        var threshold = unchecked(0UL - upperExclusive) % upperExclusive;
        while (true)
        {
            var value = ((ulong)random.NextUInt32() << 32) | random.NextUInt32();
            if (value >= threshold)
            {
                return value % upperExclusive;
            }
        }
    }

    private static string Render(
        string template,
        string given,
        string? given2,
        string family,
        string? family2,
        string? common)
    {
        var rendered = template
            .Replace("{given}", given, StringComparison.Ordinal)
            .Replace("{given2}", given2 ?? string.Empty, StringComparison.Ordinal)
            .Replace("{family}", family, StringComparison.Ordinal)
            .Replace("{family2}", family2 ?? string.Empty, StringComparison.Ordinal)
            .Replace("{common}", common ?? string.Empty, StringComparison.Ordinal);
        if (rendered.Contains('{') || rendered.Contains('}'))
        {
            throw new NameGenerationException($"Unresolved template token in '{template}'.");
        }

        return string.Join(
                ' ',
                rendered.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);
    }

    private static string PresentationValue(
        string selected,
        IReadOnlyList<string> fallbacks,
        int limit,
        string field)
    {
        foreach (var candidate in new[] { selected }.Concat(fallbacks))
        {
            var upper = candidate.ToUpperInvariant().Normalize(NormalizationForm.FormC);
            if (upper.Length > 0 && ScalarLength(upper) <= limit)
            {
                return upper;
            }
        }
        throw new NameGenerationException($"{field} cannot satisfy its length limit without truncation.");
    }

    private static void EnforceLimit(string value, int limit, string field)
    {
        if (ScalarLength(value) > limit)
        {
            throw new NameGenerationException($"{field} exceeds its pack limit.");
        }
    }

    private static int ScalarLength(string value) => value.EnumerateRunes().Count();

    private static string ComparisonKey(string value) =>
        value.Normalize(NormalizationForm.FormC).ToUpperInvariant();

    private static string RequiredNfc(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NameGenerationException($"{field} is empty.");
        }
        if (!value.IsNormalized(NormalizationForm.FormC))
        {
            throw new NameGenerationException($"{field} is not Unicode NFC.");
        }
        return value;
    }
}
