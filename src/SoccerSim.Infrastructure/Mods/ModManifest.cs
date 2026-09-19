using System.Text.Json.Serialization;

namespace SoccerSim.Infrastructure.Mods;

/// <summary>
/// The raw shape of a mod's <c>manifest.json</c>.
/// <para>
/// ADR-0007 gives a manifest four fields and no others, and
/// <see cref="JsonUnmappedMemberHandling.Disallow"/> is what makes that true rather than
/// merely documented: an unknown key fails the parse instead of being ignored. A key that
/// is silently dropped is a mod author believing in behaviour that does not exist.
/// </para>
/// <para>
/// Every field is nullable here on purpose. This type is the wire format, not a validated
/// mod — <see cref="ModLoader"/> turns a missing or empty field into a named error rather
/// than letting a default reach the database.
/// </para>
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ModManifest
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// The schema this mod was authored against. Data-only does not mean
    /// schema-independent: a mod inserting into <c>player</c> depends on that table's shape.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public int? SchemaVersion { get; init; }
}
