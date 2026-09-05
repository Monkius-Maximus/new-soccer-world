using SoccerSim.Core.Domain;
using SoccerSim.Core.Tactics;

namespace SoccerSim.Core.Match;

public sealed class MatchTeam
{
    public MatchTeam(int clubId, IReadOnlyList<Player> selectedEleven, bool attackingPositiveX, TeamTactics tactics)
    {
        ClubId = clubId;
        AttackingPositiveX = attackingPositiveX;
        Tactics = tactics;
        Players = selectedEleven.Select((player, slot) => new MatchPlayer(player, clubId, slot)).ToArray();
    }

    public int ClubId { get; }
    public TeamTactics Tactics { get; }
    public IReadOnlyList<MatchPlayer> Players { get; }
    public bool AttackingPositiveX { get; private set; }
    public int Score { get; private set; }

    public MatchPlayer Goalkeeper => Players[0];

    public Vec2 AttackingGoal => Pitch.GoalCentre(AttackingPositiveX);
    public Vec2 DefendingGoal => Pitch.GoalCentre(!AttackingPositiveX);

    public void AddGoal() => Score++;

    /// <summary>Teams change ends at half time, as in the real game.</summary>
    public void SwapEnds() => AttackingPositiveX = !AttackingPositiveX;

    public void ResetToKickOffShape()
    {
        foreach (var player in Players)
        {
            player.Location = KickOffPosition(player.Slot);
        }
    }

    /// <summary>
    /// Where a slot rests in a given phase. Roles decide how far each one travels between the
    /// two, and the defensive line height shifts the whole block when defending.
    /// </summary>
    public Vec2 PhaseAnchor(int slot, bool inPossession)
    {
        var anchor = Tactics.Formation.Anchor(slot, AttackingPositiveX);
        var behaviour = Tactics.Formation.Slots[slot].Role.Behaviour();
        var forward = AttackingPositiveX ? 1.0 : -1.0;

        // A line height of 10 is neutral; 20 pushes the block roughly 12 m further up.
        var lineShift = (Tactics.DefensiveLineHeight - 10) * 1.2;

        var offset = inPossession
            ? behaviour.AttackingPush
            : lineShift - behaviour.DefensiveDrop;

        return Pitch.Clamp(new Vec2(anchor.X + (offset * forward), anchor.Y));
    }

    /// <summary>Kick-off shape: the resting anchor, pulled back into the team's own half.</summary>
    public Vec2 KickOffPosition(int slot)
    {
        var anchor = Tactics.Formation.Anchor(slot, AttackingPositiveX);
        var halfway = Pitch.Length / 2.0;

        var x = AttackingPositiveX
            ? Math.Min(anchor.X, halfway - 1.0)
            : Math.Max(anchor.X, halfway + 1.0);

        return new Vec2(x, anchor.Y);
    }

    /// <summary>Nearest player to a point, excluding the goalkeeper unless asked otherwise.</summary>
    public MatchPlayer Nearest(Vec2 point, bool includeGoalkeeper = true)
    {
        MatchPlayer? best = null;
        var bestDistance = double.MaxValue;

        // Indexed iteration over a stable array: no hash ordering can leak into the result.
        for (var i = 0; i < Players.Count; i++)
        {
            var candidate = Players[i];
            if (!includeGoalkeeper && candidate.IsGoalkeeper)
            {
                continue;
            }

            var distance = candidate.Location.DistanceSquaredTo(point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best ?? Players[0];
    }
}
