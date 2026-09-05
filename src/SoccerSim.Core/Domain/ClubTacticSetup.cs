using SoccerSim.Core.Tactics;

namespace SoccerSim.Core.Domain;

/// <summary>A club's chosen setup, as stored in the career world.</summary>
public sealed record ClubTacticSetup(int ClubId, TeamTactics Tactics);
