using System.Text.Json;
using SoccerSim.Core.Persistence;

namespace SoccerSim.Infrastructure.Mods;

/// <summary>
/// Reads a mod directory into a validated <see cref="ModPackage"/>, or throws saying why
/// it is not one. Nothing here touches a database: a malformed mod has to fail before the
/// build starts writing, not halfway through.
/// </summary>
public static class ModLoader
{
    public const string ManifestFileName = "manifest.json";

    public static ModPackage Load(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        var fullPath = Path.GetFullPath(modDirectory);
        if (!System.IO.Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Mod directory not found: {fullPath}");
        }

        var manifestPath = Path.Combine(fullPath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"A mod directory must contain '{ManifestFileName}'.", manifestPath);
        }

        var manifest = Parse(manifestPath);

        var id = Required(manifest.Id, "id", manifestPath);
        var name = Required(manifest.Name, "name", manifestPath);
        var version = Required(manifest.Version, "version", manifestPath);

        if (manifest.SchemaVersion is not { } schemaVersion)
        {
            throw new InvalidDataException($"'{manifestPath}' is missing 'schema_version'.");
        }

        // Data-only is not schema-independent. A mod writes rows into tables whose shape it
        // assumed, so a mod authored against another schema is refused rather than applied
        // and left to fail on a column that moved.
        if (schemaVersion != SchemaVersions.Expected)
        {
            throw new InvalidDataException(
                $"Mod '{id}' targets schema version {schemaVersion}, but this build is at " +
                $"{SchemaVersions.Expected}. Republish the mod against the current schema.");
        }

        var scripts = System.IO.Directory
            .EnumerateFiles(fullPath, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        if (scripts.Length == 0)
        {
            throw new InvalidDataException($"Mod '{id}' contains no .sql scripts.");
        }

        foreach (var script in scripts)
        {
            RequireOrderingPrefix(id, script);
        }

        return new ModPackage(id, name, version, schemaVersion, scripts);
    }

    private static ModManifest Parse(string manifestPath)
    {
        try
        {
            return JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(manifestPath))
                ?? throw new InvalidDataException($"'{manifestPath}' is empty.");
        }
        catch (JsonException exception)
        {
            // Includes the unknown-key failure, which is the point of the Disallow policy.
            throw new InvalidDataException(
                $"'{manifestPath}' is not a valid mod manifest. ADR-0007 allows exactly " +
                "'id', 'name', 'version' and 'schema_version'.",
                exception);
        }
    }

    private static string Required(string? value, string field, string manifestPath) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"'{manifestPath}' is missing '{field}'.")
            : value;

    /// <summary>
    /// Scripts are ordered by a numeric filename prefix. A file without one would still sort
    /// somewhere, which is worse than failing: the mod would apply in an order nobody chose.
    /// </summary>
    private static void RequireOrderingPrefix(string modId, string scriptPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        var separator = fileName.IndexOf('_', StringComparison.Ordinal);
        if (separator <= 0 || !fileName[..separator].All(char.IsAsciiDigit))
        {
            throw new InvalidDataException(
                $"Script '{fileName}' in mod '{modId}' must start with a numeric prefix " +
                "followed by '_', as sql/migrations does: 010_clubs.sql.");
        }
    }
}
