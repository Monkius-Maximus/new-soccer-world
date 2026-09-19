namespace SoccerSim.Infrastructure.Mods;

/// <summary>
/// A validated mod: its identity and the scripts to apply, already in order.
/// <para>
/// Ordering within a mod is the numeric filename prefix, the convention
/// <c>sql/migrations</c> already uses. Ordering <em>between</em> mods is the caller's list
/// position and is deliberately not represented here — ADR-0007 gives the launcher that
/// authority, and a <c>load_order</c> field would be a second way to express an order
/// already fully determined.
/// </para>
/// </summary>
public sealed record ModPackage(
    string Id,
    string Name,
    string Version,
    int SchemaVersion,
    IReadOnlyList<string> Scripts);
