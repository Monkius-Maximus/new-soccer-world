namespace SoccerSim.Core.Persistence;

public sealed record SaveMetadata(
    string SaveId,
    string GameVersion,
    int SchemaVersion,
    string ContentVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastPlayedAt,
    ulong CareerSeed);
