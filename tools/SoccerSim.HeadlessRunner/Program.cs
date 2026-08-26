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

var homeId = rosters[0].ClubId;
var awayId = rosters[1].ClubId;

var first = application.RunMatch(career, homeId, awayId, seed).Result;
var replay = application.RunMatch(career, homeId, awayId, seed).Result;
var alternate = application.RunMatch(career, homeId, awayId, seed + 1UL).Result;

// Reproduction record: everything a future run needs to reproduce these results exactly.
// The guarantee is same build + same platform/architecture + same initial state + same
// inputs + same seed. It is deliberately not a cross-platform guarantee.
Console.WriteLine("--- reproduction record ---");
Console.WriteLine($"SimulationVersion   = {first.SimulationVersion}");
Console.WriteLine($"FixedTimeStepMs     = {SoccerSim.Core.Simulation.SimulationSettings.FixedTimeStepMilliseconds}");
Console.WriteLine($"Ticks               = {first.Ticks}");
Console.WriteLine($"SchemaVersion       = {career.Save.Metadata.SchemaVersion}");
Console.WriteLine($"ContentVersion      = {career.Save.Metadata.ContentVersion}");
Console.WriteLine($"GameVersion         = {career.Save.Metadata.GameVersion}");
Console.WriteLine($"CareerSeed          = {career.Save.Metadata.CareerSeed}");
Console.WriteLine($"Runtime             = {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"Platform            = {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
Console.WriteLine("---------------------------");

var homeName = rosters[0].ClubName;
var awayName = rosters[1].ClubName;
Console.WriteLine($"Seed {seed}:   {homeName} {first.HomeScore}-{first.AwayScore} {awayName}   digest={first.Digest:X16}");
Console.WriteLine($"Replay {seed}: {homeName} {replay.HomeScore}-{replay.AwayScore} {awayName}   digest={replay.Digest:X16}");
Console.WriteLine($"Seed {seed + 1UL}: {homeName} {alternate.HomeScore}-{alternate.AwayScore} {awayName}   digest={alternate.Digest:X16}");

Console.WriteLine("Goals:");
foreach (var goal in first.Goals)
{
    Console.WriteLine($"  {goal.Minute:D2}'  club {goal.ClubId}  player {goal.PlayerId}");
}

if (first.Digest != replay.Digest || first.FinalRandomState != replay.FinalRandomState)
{
    Console.Error.WriteLine("Determinism failure: identical input did not reproduce exactly.");
    return 12;
}
if (first.Digest == alternate.Digest)
{
    Console.Error.WriteLine("Random-source sanity failure: adjacent seeds produced an identical match.");
    return 13;
}

// Prove the save contract end to end: apply -> checkpoint -> reopen -> discard.
application.ApplyOutcomes(career, [application.RunMatch(career, homeId, awayId, seed)]);
if (!career.World.HasUnsavedChanges)
{
    Console.Error.WriteLine("Applying an outcome should have left the career dirty.");
    return 14;
}

application.Checkpoint(career, SoccerSim.Core.Persistence.CheckpointKind.EndOfMatch, DateTimeOffset.UtcNow);
var committed = application.OpenCareer(career.Save.DatabasePath);
if (committed.World.SimulationRuns.Count != 1)
{
    Console.Error.WriteLine("Checkpoint did not persist the applied run.");
    return 15;
}

application.ApplyOutcomes(career, [application.RunMatch(career, homeId, awayId, seed + 7UL)]);
var discarded = application.DiscardAndReload(career);
if (discarded.World.SimulationRuns.Count != 1)
{
    Console.Error.WriteLine("Quitting without saving should have discarded post-checkpoint work.");
    return 16;
}

var stored = committed.World.SimulationRuns[0];
Console.WriteLine(
    $"Save contract: committed {stored.HomeScore}-{stored.AwayScore}; " +
    $"discarded {career.World.SimulationRuns.Count - discarded.World.SimulationRuns.Count} unsaved run(s).");
Console.WriteLine("Headless vertical slice passed.");
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
