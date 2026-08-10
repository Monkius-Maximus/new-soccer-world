using System.Reflection;
using System.Runtime.CompilerServices;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Architecture.Tests;

/// <summary>
/// Structural tripwires for the determinism contract in ADR-0004. They cannot prove a
/// simulation is deterministic, but they do fail the moment someone reintroduces one of
/// the entropy sources the contract bans.
/// </summary>
public sealed class DeterminismGuardTests
{
    private static readonly string[] DeterministicPath =
    [
        "src/SoccerSim.Core",
        "src/SoccerSim.Application"
    ];

    private static readonly string[] BannedEntropySources =
    [
        "Random.Shared",
        "new Random(",
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
        "Guid.NewGuid()",
        "Environment.TickCount",
        "Stopwatch.GetTimestamp",
        "RandomNumberGenerator"
    ];

    [Fact]
    public void Deterministic_path_contains_no_ambient_entropy_source()
    {
        foreach (var layer in DeterministicPath)
        {
            foreach (var (path, text) in RepoLayout.SourceFiles(layer))
            {
                foreach (var token in BannedEntropySources)
                {
                    Assert.False(
                        text.Contains(token, StringComparison.Ordinal),
                        $"'{path}' is on the deterministic path and must not use '{token}'. " +
                        "Time and identity have to be passed in by the caller.");
                }
            }
        }
    }

    [Fact]
    public void Core_never_iterates_hash_ordered_collections()
    {
        // Dictionary and HashSet have no guaranteed enumeration order, so a simulation that
        // walks one can diverge between runs. Core uses ordered sequences instead.
        foreach (var (path, text) in RepoLayout.SourceFiles("src/SoccerSim.Core"))
        {
            Assert.False(
                text.Contains("Dictionary<", StringComparison.Ordinal),
                $"'{path}' must not use Dictionary; use an ordered collection.");
            Assert.False(
                text.Contains("HashSet<", StringComparison.Ordinal),
                $"'{path}' must not use HashSet; use an ordered collection.");
        }
    }

    [Fact]
    public void Core_declares_no_mutable_global_state()
    {
        var offenders = typeof(IRandomSource).Assembly
            .GetTypes()
            .Where(type => !IsCompilerGenerated(type))
            .SelectMany(type => type
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsLiteral && !field.IsInitOnly)
                .Where(field => !IsCompilerGenerated(field))
                .Select(field => $"{type.FullName}.{field.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Random_source_carries_its_state_explicitly()
    {
        Assert.NotNull(typeof(IRandomSource).GetProperty(nameof(IRandomSource.State)));

        // Two instances built from the same seed are independent but identical: this is what
        // lets a future parallel run isolate one IRandomSource per match instance.
        IRandomSource first = new Pcg32Random(7UL);
        IRandomSource second = new Pcg32Random(7UL);

        for (var i = 0; i < 64; i++)
        {
            Assert.Equal(first.NextUInt32(), second.NextUInt32());
        }

        Assert.Equal(first.State, second.State);
    }

    [Fact]
    public void Fixed_timestep_is_defined_in_exactly_one_place()
    {
        var declarations = RepoLayout.SourceFiles("src/SoccerSim.Core")
            .Where(file => file.Text.Contains("FixedTimeStepMilliseconds =", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        Assert.Single(declarations);
        Assert.EndsWith("SimulationSettings.cs", declarations[0], StringComparison.Ordinal);
        Assert.True(SimulationSettings.FixedTimeStepMilliseconds > 0);
    }

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) ||
        member.Name.Contains('<', StringComparison.Ordinal);
}
