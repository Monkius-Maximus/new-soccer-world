using SoccerSim.Infrastructure.Sqlite;

var options = ParseArgs(args);
var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var output = options.TryGetValue("output", out var requestedOutput)
    ? Path.GetFullPath(requestedOutput)
    : Path.Combine(repoRoot, "artifacts", "world_template.db");

var migrations = Path.Combine(repoRoot, "sql", "migrations");
var seeds = Path.Combine(repoRoot, "sql", "seeds");
new SqliteWorldTemplateBuilder().Build(migrations, seeds, output);
Console.WriteLine($"Generated world template: {output}");
return 0;

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
