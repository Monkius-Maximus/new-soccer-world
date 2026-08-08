namespace SoccerSim.Application;

public sealed record PlayerView(int Id, int SquadNumber, string DisplayName, string PositionCode);

public sealed record ClubRosterView(int ClubId, string ClubName, string ShortName, IReadOnlyList<PlayerView> Players);
