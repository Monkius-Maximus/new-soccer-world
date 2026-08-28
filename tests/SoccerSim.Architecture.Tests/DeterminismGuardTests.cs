using System.Reflection;
using System.Runtime.CompilerServices;
using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;
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

    private static WorldState BuildWorld()
    {
        PlayerPosition[] shape =
        [
            PlayerPosition.Goalkeeper, PlayerPosition.Goalkeeper,
            PlayerPosition.RightBack, PlayerPosition.CentreBack, PlayerPosition.CentreBack,
            PlayerPosition.LeftBack, PlayerPosition.CentreBack,
            PlayerPosition.DefensiveMidfielder, PlayerPosition.CentralMidfielder,
            PlayerPosition.CentralMidfielder, PlayerPosition.AttackingMidfielder,
            PlayerPosition.RightWinger, PlayerPosition.LeftWinger,
            PlayerPosition.Striker, PlayerPosition.Striker
        ];
        var attributes = new PlayerAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10);

        IEnumerable<Player> Squad(int clubId, int idBase) => shape.Select((position, index) =>
            new Player(idBase + index, 1, clubId, $"P{idBase + index}", "Test",
                new DateOnly(2000, 1, 1), index + 1, position, attributes));

        return new WorldState(
            [new Country(1, "BRA", "Brasil")],
            [new City(1, 1, "Recife")],
            [new Stadium(1, 1, "Estadio das Pontes", 18000)],
            [new Club(1, 1, 1, "Recife Azul", "RAZ"), new Club(2, 1, 1, "Recife Vermelho", "RVM")],
            [.. Squad(1, 100), .. Squad(2, 200)],
            [new Competition(1, 1, "Amistoso da Fundacao", "friendly")]);
    }

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
    public void The_simulation_path_uses_no_transcendental_maths()
    {
        // IEEE-754 requires +, -, *, / and sqrt to be correctly rounded, so they give identical
        // results everywhere. Sin, Cos, Atan2, Pow, Exp and Log carry no such requirement and can
        // differ between platforms and even runtime versions. A spatial simulation that avoids
        // them keeps replay exactness a property of the arithmetic rather than of the machine.
        string[] banned =
        [
            "Math.Sin", "Math.Cos", "Math.Tan", "Math.Asin", "Math.Acos",
            "Math.Atan", "Math.Atan2", "Math.Pow", "Math.Exp", "Math.Log",
            "Math.Cbrt", "Math.Sinh", "Math.Cosh", "Math.Tanh", "MathF."
        ];

        foreach (var layer in DeterministicPath)
        {
            foreach (var (path, text) in RepoLayout.SourceFiles(layer))
            {
                foreach (var token in banned)
                {
                    Assert.False(
                        text.Contains(token, StringComparison.Ordinal),
                        $"'{path}' is on the deterministic path and must not use '{token}'. " +
                        "Use vector normalisation and squared distances instead.");
                }
            }
        }
    }

    [Fact]
    public void Every_match_gets_its_own_random_source()
    {
        // Two matches from the same seed must not be able to influence each other, which is what
        // makes running them in parallel safe later.
        var world = BuildWorld();

        var a = MatchSimulation.Run(new MatchContext(1, 2, 31337UL, SimulationSettings.SimulationVersion, world.Snapshot()));
        var b = MatchSimulation.Run(new MatchContext(1, 2, 31337UL, SimulationSettings.SimulationVersion, world.Snapshot()));

        Assert.Equal(a.Digest, b.Digest);
        Assert.Equal(a.FinalRandomState, b.FinalRandomState);
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
