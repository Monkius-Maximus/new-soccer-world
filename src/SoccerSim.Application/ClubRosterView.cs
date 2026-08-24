using SoccerSim.Core.Domain;

namespace SoccerSim.Application;

public sealed record PlayerView(
    int Id,
    int SquadNumber,
    string DisplayName,
    string PositionCode,
    PitchLine Line,
    PlayerAttributes Attributes);

public sealed record ClubRosterView(int ClubId, string ClubName, string ShortName, IReadOnlyList<PlayerView> Players);
