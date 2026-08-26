using SoccerSim.Core.Domain;

namespace SoccerSim.Core.Match;

public sealed class MatchTeam
{
    public MatchTeam(int clubId, IReadOnlyList<Player> selectedEleven, bool attackingPositiveX)
    {
        ClubId = clubId;
        AttackingPositiveX = attackingPositiveX;
        Players = selectedEleven.Select((player, slot) => new MatchPlayer(player, clubId, slot)).ToArray();
    }

    public int ClubId { get; }
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
            player.Location = Formation.KickOffPosition(player.Slot, AttackingPositiveX);
        }
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
