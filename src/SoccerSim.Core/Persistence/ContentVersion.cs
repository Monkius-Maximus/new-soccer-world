using System.Security.Cryptography;
using System.Text;

namespace SoccerSim.Core.Persistence;

/// <summary>
/// Derives <see cref="SaveMetadata.ContentVersion"/> from the mods that produced a
/// template, so provenance is one comparable value while <see cref="SaveMetadata.ModList"/>
/// carries the readable detail.
/// </summary>
public static class ContentVersion
{
    /// <summary>
    /// A stable digest over the ordered (id, version) pairs.
    /// <para>
    /// Stable is the operative word. <c>string.GetHashCode</c> is randomized per process in
    /// .NET, so the same mod set would produce a different ContentVersion on every run and
    /// the value would be worthless for comparing one template against another. SHA-256 over
    /// a canonical string has no such surprise.
    /// </para>
    /// <para>
    /// An empty list is a real answer, not a missing one: it is the version of a template
    /// built from base content alone.
    /// </para>
    /// </summary>
    public static string From(IReadOnlyList<ModReference> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);

        var canonical = new StringBuilder();
        foreach (var mod in mods)
        {
            canonical.Append(mod.Id).Append('@').Append(mod.Version).Append('\n');
        }

        return Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}
