namespace SoccerSim.Core.Persistence;

/// <summary>
/// One mod that produced a template, as provenance: which mod, at which version.
/// <para>
/// This is deliberately not a dependency. A career is a copy of its template (ADR-0003),
/// so it keeps working when the player disables every mod in this list. The list exists so
/// a bug report from a modded world is reproducible, and so a launcher can say the enabled
/// set has changed — never so anything can refuse to open a save.
/// </para>
/// </summary>
public sealed record ModReference(string Id, string Version);
