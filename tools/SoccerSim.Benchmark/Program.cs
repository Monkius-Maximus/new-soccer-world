using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SoccerSim.Application;
using SoccerSim.Infrastructure.Sqlite;

// PERF-001 — the measurement ADR-0008 asks for.
//
// It reports and never asserts. Whether the criterion is met — 100 matches per round
// within 10 seconds on one thread, on the reference machine — is judged by a person
// reading this output. A wall-clock assertion belongs nowhere near a shared CI runner:
// it fails from noise, and a test that fails randomly is one people learn to ignore.
// CI guards the order of magnitude instead, in
// tests/SoccerSim.Core.Tests/PerformanceGuardTests.cs.
//
// Matches run sequentially on purpose. Match isolation already makes a round
// parallelisable and deterministic, but ADR-0008 fixes the criterion to a single thread
// so that parallelism stays headroom rather than something the budget silently depends
// on.

const int RoundMatches = 100;
const double RoundBudgetMs = 10_000;

var options = ParseArgs(args);
var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var template = Path.GetFullPath(options.GetValueOrDefault("template", Path.Combine(repoRoot, "artifacts", "world_template.db")));
var saves = Path.GetFullPath(options.GetValueOrDefault("saves", Path.Combine(repoRoot, "saves")));
var matches = int.Parse(options.GetValueOrDefault("matches", "100"), CultureInfo.InvariantCulture);
var warmup = int.Parse(options.GetValueOrDefault("warmup", "5"), CultureInfo.InvariantCulture);
var baseSeed = ulong.Parse(options.GetValueOrDefault("seed", "500000"), CultureInfo.InvariantCulture);

if (matches < 1)
{
    throw new ArgumentOutOfRangeException(nameof(matches), matches, "--matches must be at least 1.");
}

if (warmup < 1)
{
    // Without a warm-up the first match pays for JIT compilation of the entire simulation
    // loop, which lands entirely in the reported average.
    throw new ArgumentOutOfRangeException(nameof(warmup), warmup, "--warmup must be at least 1.");
}

if (!File.Exists(template))
{
    Console.Error.WriteLine($"Template not found: {template}");
    Console.Error.WriteLine("Run SoccerSim.WorldBuilder first.");
    return 10;
}

// Setup is deliberately outside the measurement: loading the world and creating the career
// are once-per-session costs, not per-match ones.
var application = new CareerApplication(new SqliteCareerStore());
var career = application.CreateCareer(
    template,
    saves,
    $"perf-001-{Guid.NewGuid():N}",
    careerSeed: 424242UL,
    gameVersion: "0.0.1-perf",
    timestamp: DateTimeOffset.UtcNow);

var rosters = new ClubRosterQueryService().GetClubRosters(career.World);
if (rosters.Count < 2)
{
    Console.Error.WriteLine("Benchmark needs at least two clubs in the template.");
    return 11;
}

var homeId = rosters[0].ClubId;
var awayId = rosters[1].ClubId;

for (var i = 0; i < warmup; i++)
{
    _ = application.RunMatch(career, homeId, awayId, baseSeed + (ulong)i);
}

// Seeds vary per match so this measures a spread of matches rather than the same one
// repeatedly through an increasingly warm branch predictor.
var ticksPerMatch = 0;
var stopwatch = Stopwatch.StartNew();
for (var i = 0; i < matches; i++)
{
    ticksPerMatch = application.RunMatch(career, homeId, awayId, baseSeed + 1_000_000UL + (ulong)i).Result.Ticks;
}
stopwatch.Stop();

var totalMs = stopwatch.Elapsed.TotalMilliseconds;
var msPerMatch = totalMs / matches;
var nsPerTick = msPerMatch * 1_000_000 / ticksPerMatch;
var projectedRoundMs = msPerMatch * RoundMatches;

Console.WriteLine("--- PERF-001 round-advance measurement ---");
Console.WriteLine($"Criterion (ADR-0008)   {RoundMatches} matches in <= {RoundBudgetMs / 1000:F0} s, single thread, reference machine");
Console.WriteLine($"Matches measured       {matches} (after {warmup} warm-up matches, discarded)");
Console.WriteLine($"Ticks per match        {ticksPerMatch}");
Console.WriteLine($"Total                  {totalMs:F1} ms");
Console.WriteLine($"Per match              {msPerMatch:F2} ms");
Console.WriteLine($"Per tick               {nsPerTick:F0} ns");
Console.WriteLine($"Projected round        {projectedRoundMs / 1000:F2} s for {RoundMatches} matches");
Console.WriteLine($"Verdict                {(projectedRoundMs <= RoundBudgetMs ? "WITHIN BUDGET" : "OVER BUDGET")}");
Console.WriteLine("--- machine ---");
Console.WriteLine($"Runtime                {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"RuntimeIdentifier      {RuntimeInformation.RuntimeIdentifier}");
Console.WriteLine($"OS                     {RuntimeInformation.OSDescription}");
Console.WriteLine($"OS architecture        {RuntimeInformation.OSArchitecture}");
Console.WriteLine($"Process architecture   {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Logical processors     {Environment.ProcessorCount}");
Console.WriteLine("CPU model              not available: .NET exposes no portable API. Record it by hand in ADR-0008.");
Console.WriteLine("------------------------------------------");
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
