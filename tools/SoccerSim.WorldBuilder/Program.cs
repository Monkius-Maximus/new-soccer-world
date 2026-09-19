using SoccerSim.Infrastructure.Sqlite;

var options = ParseArgs(args);
var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var output = options.TryGetValue("output", out var requestedOutput)
    ? Path.GetFullPath(requestedOutput)
    : Path.Combine(repoRoot, "artifacts", "world_template.db");

var migrations = Path.Combine(repoRoot, "sql", "migrations");
var seeds = Path.Combine(repoRoot, "sql", "seeds");

// The launcher owns mod order, and on this CLI that is the order they are listed in
// (ADR-0007). There is no ordering field in a manifest to override it.
var mods = ParseMods(options.GetValueOrDefault("mods", string.Empty));

new SqliteWorldTemplateBuilder().Build(migrations, seeds, mods, output);
Console.WriteLine($"Generated world template: {output}");
if (mods.Count > 0)
{
    Console.WriteLine($"Applied {mods.Count} mod(s), in order: {string.Join(" -> ", mods.Select(Path.GetFileName))}");
}
return 0;

static IReadOnlyList<string> ParseMods(string value) =>
    value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Path.GetFullPath)
        .ToArray();

static Dictionary<string, string> ParseArgs(string[] values)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < values.Length; i++)
    {
        if (!values[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= values.Length)
        {
            continue;
        }
        parsed[values[i][2..]] = values[++i];
    }
    return parsed;
}

static string FindRepoRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "SoccerDreamGame.sln")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate SoccerDreamGame.sln.");
}
