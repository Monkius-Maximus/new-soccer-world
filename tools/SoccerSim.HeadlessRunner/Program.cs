using SoccerSim.Application;
using SoccerSim.Infrastructure.Sqlite;

var options = ParseArgs(args);
var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var template = Path.GetFullPath(options.GetValueOrDefault("template", Path.Combine(repoRoot, "artifacts", "world_template.db")));
var saves = Path.GetFullPath(options.GetValueOrDefault("saves", Path.Combine(repoRoot, "saves")));
var seed = ulong.Parse(options.GetValueOrDefault("seed", "123456789"), System.Globalization.CultureInfo.InvariantCulture);

if (!File.Exists(template))
{
    Console.Error.WriteLine($"Template not found: {template}");
    Console.Error.WriteLine("Run SoccerSim.WorldBuilder first.");
    return 10;
}

var saveId = $"foundation-smoke-{Guid.NewGuid():N}";
var application = new CareerApplication(new SqliteCareerStore());
var career = application.CreateCareer(
    template,
    saves,
    saveId,
    careerSeed: 424242UL,
    gameVersion: "0.0.1-foundation",
    contentVersion: "foundation-recife-1",
    timestamp: DateTimeOffset.UtcNow);

var query = new ClubRosterQueryService();
var rosters = query.GetClubRosters(career.World);
Console.WriteLine($"Loaded {rosters.Count} clubs and {career.World.Players.Count} players from {career.Save.DatabasePath}");
foreach (var club in rosters)
{
    Console.WriteLine($"- {club.ClubName} ({club.ShortName}): {club.Players.Count} players");
}

if (rosters.Count < 2)
{
    Console.Error.WriteLine("Foundation seed must contain at least two clubs.");
    return 11;
}

const int ticks = 4096;
var homeId = rosters[0].ClubId;
var awayId = rosters[1].ClubId;
var first = application.RunFoundationSimulationProbe(career, homeId, awayId, seed, ticks);
var replay = application.RunFoundationSimulationProbe(career, homeId, awayId, seed, ticks);
var alternate = application.RunFoundationSimulationProbe(career, homeId, awayId, seed + 1UL, ticks);

Console.WriteLine($"SimulationVersion={first.SimulationVersion}; TickMs={SoccerSim.Core.Simulation.SimulationSettings.FixedTimeStepMilliseconds}");
Console.WriteLine($"Seed {seed}: digest={first.Digest:X16}, finalRng={first.FinalRandomState:X16}");
Console.WriteLine($"Replay {seed}: digest={replay.Digest:X16}, finalRng={replay.FinalRandomState:X16}");
Console.WriteLine($"Seed {seed + 1UL}: digest={alternate.Digest:X16}, finalRng={alternate.FinalRandomState:X16}");

if (first != replay)
{
    Console.Error.WriteLine("Determinism failure: identical input did not reproduce exactly.");
    return 12;
}
if (first.Digest == alternate.Digest && first.FinalRandomState == alternate.FinalRandomState)
{
    Console.Error.WriteLine("Random-source sanity failure: adjacent seeds produced an identical probe state.");
    return 13;
}

application.Checkpoint(career, SoccerSim.Core.Persistence.CheckpointKind.EndOfMatch, DateTimeOffset.UtcNow);
Console.WriteLine("Foundation headless vertical slice passed.");
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
