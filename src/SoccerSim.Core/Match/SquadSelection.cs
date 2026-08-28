using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Match;

/// <summary>
/// Picks a starting eleven from a club's roster, filling the 4-4-2 slots in order.
/// <para>
/// Entirely deterministic and RNG-free: the same roster always produces the same eleven, so a
/// replayed match starts from the same state. Team selection is a manager decision that CLUB-00
/// will own; until then the best available player for each slot is the honest default.
/// </para>
/// </summary>
public static class SquadSelection
{
    public static IReadOnlyList<Player> PickEleven(IReadOnlyList<Player> roster)
    {
        if (roster.Count < Formation.Slots.Count)
        {
            throw new InvalidOperationException(
                $"A squad needs at least {Formation.Slots.Count} players, but only {roster.Count} were available.");
        }

        var remaining = roster.OrderBy(player => player.Id).ToList();
        var eleven = new List<Player>(Formation.Slots.Count);

        foreach (var line in Formation.Slots)
        {
            var pick = BestFor(remaining, line, naturalOnly: true)
                       ?? BestFor(remaining, line, naturalOnly: false)!;

            eleven.Add(pick);
            remaining.Remove(pick);
        }

        return eleven;
    }

    private static Player? BestFor(List<Player> candidates, PitchLine line, bool naturalOnly)
    {
        Player? best = null;
        var bestRating = int.MinValue;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (naturalOnly && candidate.Line != line)
            {
                continue;
            }

            // A player out of position is rated for the job asked of them, minus a penalty.
            var rating = RatingFor(candidate.Attributes, line) - (candidate.Line == line ? 0 : 12);
            if (rating > bestRating)
            {
                bestRating = rating;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>How well a set of attributes suits a line. Used for selection only.</summary>
    public static int RatingFor(PlayerAttributes attributes, PitchLine line) => line switch
    {
        PitchLine.Goalkeeper => (attributes.Goalkeeping * 3) + attributes.Positioning,
        PitchLine.Defence => attributes.Tackling + attributes.Positioning + attributes.Strength,
        PitchLine.Midfield => attributes.Passing + attributes.Positioning + attributes.Stamina,
        PitchLine.Attack => attributes.Shooting + attributes.Dribbling + attributes.Pace,
        _ => throw new ArgumentOutOfRangeException(nameof(line), line, "Unmapped pitch line.")
    };
}
