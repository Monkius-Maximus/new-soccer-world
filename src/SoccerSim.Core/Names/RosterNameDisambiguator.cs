using System.Text;
using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Names;

/// <summary>
/// Resolves shirt and scoreboard collisions inside one active roster. Call this before persistence;
/// the resolved values become part of <see cref="PlayerName"/> and are not recalculated on load.
/// </summary>
public static class RosterNameDisambiguator
{
    public static IReadOnlyDictionary<ulong, PlayerName> ResolveForPersistence(
        IReadOnlyList<RosterNameEntry> roster,
        int shirtNameMax = 16,
        int scoreboardNameMax = 18)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (roster.Select(item => item.PersonId).Distinct().Count() != roster.Count)
        {
            throw new NameGenerationException("A roster contains a duplicate PersonId.");
        }

        var shirts = ResolveField(roster, item => item.Name.ShirtName, shirtNameMax, "shirtName");
        var scoreboard = ResolveField(
            roster, item => item.Name.ScoreboardName, scoreboardNameMax, "scoreboardName");

        return roster.ToDictionary(
            item => item.PersonId,
            item => item.Name with
            {
                ShirtName = shirts[item.PersonId],
                ScoreboardName = scoreboard[item.PersonId]
            });
    }

    private static IReadOnlyDictionary<ulong, string> ResolveField(
        IReadOnlyList<RosterNameEntry> roster,
        Func<RosterNameEntry, string> selected,
        int limit,
        string field)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var groups = roster.GroupBy(item => Key(selected(item))).ToArray();
        var result = new Dictionary<ulong, string>();
        var reserved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in groups.Where(group => group.Count() == 1))
        {
            var item = group.Single();
            var value = UpperNfc(selected(item));
            if (ScalarLength(value) > limit)
            {
                throw new NameGenerationException($"Persisted {field} exceeds the roster limit.");
            }
            reserved.Add(Key(value));
            result.Add(item.PersonId, value);
        }

        var unresolved = groups
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .Select(item => new CandidateSet(
                item.PersonId,
                Candidates(item.Name, selected(item), limit)
                    .Where(candidate => !reserved.Contains(Key(candidate)))
                    .ToArray()))
            .OrderBy(set => set.Candidates.Count)
            .ThenBy(set => set.PersonId)
            .ToArray();

        if (!TryAssign(unresolved, 0, reserved, result))
        {
            throw new NameGenerationException(
                $"Roster {field} collision cannot be resolved without truncation or an editorial override.");
        }
        return result;
    }

    private static bool TryAssign(
        IReadOnlyList<CandidateSet> sets,
        int index,
        HashSet<string> reserved,
        Dictionary<ulong, string> result)
    {
        if (index == sets.Count)
        {
            return true;
        }

        var set = sets[index];
        foreach (var candidate in set.Candidates)
        {
            var key = Key(candidate);
            if (!reserved.Add(key))
            {
                continue;
            }
            result.Add(set.PersonId, candidate);
            if (TryAssign(sets, index + 1, reserved, result))
            {
                return true;
            }
            result.Remove(set.PersonId);
            reserved.Remove(key);
        }
        return false;
    }

    private static IEnumerable<string> Candidates(PlayerName name, string selected, int limit)
    {
        var initial = name.GivenName.EnumerateRunes().First().ToString();
        var raw = new List<string>
        {
            $"{initial} {name.FamilyName}",
            $"{name.GivenName} {name.FamilyName}",
            name.FullName
        };
        if (name.AdditionalFamilyName is not null)
        {
            raw.Add($"{initial} {name.FamilyName} {name.AdditionalFamilyName}");
            raw.Add(name.AdditionalFamilyName);
        }
        raw.Add(name.FamilyName);
        raw.Add(name.GivenName);
        raw.Add(selected);

        return raw
            .Select(UpperNfc)
            .Where(value => value.Length > 0 && ScalarLength(value) <= limit)
            .DistinctBy(Key, StringComparer.Ordinal);
    }

    private static string UpperNfc(string value) =>
        value.ToUpperInvariant().Normalize(NormalizationForm.FormC);

    private static string Key(string value) => UpperNfc(value);

    private static int ScalarLength(string value) => value.EnumerateRunes().Count();

    private sealed record CandidateSet(ulong PersonId, IReadOnlyList<string> Candidates);
}

public sealed record RosterNameEntry(ulong PersonId, PlayerName Name);
