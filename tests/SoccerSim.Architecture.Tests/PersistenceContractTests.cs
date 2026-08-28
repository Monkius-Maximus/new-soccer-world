using System.Globalization;
using System.Text.RegularExpressions;
using SoccerSim.Core.Persistence;

namespace SoccerSim.Architecture.Tests;

/// <summary>
/// Guards the persistence rules that only misbehave on one platform, so a green Linux run
/// cannot hide them. Windows x86_64 is a first-class 1.0 target.
/// </summary>
public sealed partial class PersistenceContractTests
{
    [GeneratedRegex(@"Data Source=[^""]*", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPattern();

    [Fact]
    public void Every_connection_string_disables_pooling()
    {
        // Microsoft.Data.Sqlite pools connections by default, and a pooled connection keeps
        // the database file handle open after Dispose. On Windows that makes the file
        // impossible to copy, delete or replace — which breaks new-career template copies
        // and Save & Exit, not just test cleanup. POSIX unlink semantics hide this on Linux.
        var found = 0;

        foreach (var (path, text) in RepoLayout.SourceFiles("src/SoccerSim.Infrastructure"))
        {
            foreach (Match match in ConnectionStringPattern().Matches(text))
            {
                found++;
                Assert.True(
                    match.Value.Contains("Pooling=False", StringComparison.OrdinalIgnoreCase),
                    $"Connection string in '{path}' must set Pooling=False, but was: {match.Value}");
            }
        }

        Assert.True(found > 0, "Expected to find at least one SQLite connection string to check.");
    }

    [Fact]
    public void The_expected_schema_version_matches_the_highest_migration_on_disk()
    {
        // Adding a migration without bumping SchemaVersions.Expected produces a template the
        // build then refuses to open. Catch it here rather than at first run.
        var migrations = Directory
            .EnumerateFiles(Path.Combine(RepoLayout.Root, "sql", "migrations"), "*.sql")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(migrations);

        var highest = migrations
            .Select(name => int.Parse(name!.Split('_', 2)[0], CultureInfo.InvariantCulture))
            .Max();

        Assert.Equal(SchemaVersions.Expected, highest);
    }

    [Fact]
    public void Migration_versions_are_contiguous_and_unique()
    {
        var versions = Directory
            .EnumerateFiles(Path.Combine(RepoLayout.Root, "sql", "migrations"), "*.sql")
            .Select(path => int.Parse(Path.GetFileName(path).Split('_', 2)[0], CultureInfo.InvariantCulture))
            .OrderBy(version => version)
            .ToArray();

        Assert.Equal(Enumerable.Range(1, versions.Length), versions);
    }

    [Fact]
    public void Every_connection_call_site_is_bound_by_a_using_scope()
    {
        // Only call sites are checked. A factory that builds a connection and returns it is
        // correct by construction; it is the caller that owns disposal.
        var checkedSites = 0;

        foreach (var (path, text) in RepoLayout.SourceFiles("src/SoccerSim.Infrastructure"))
        {
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.Contains("= Open(", StringComparison.Ordinal))
                {
                    continue;
                }

                checkedSites++;
                Assert.True(
                    line.StartsWith("using ", StringComparison.Ordinal),
                    $"'{path}' line {i + 1} opens a connection without a `using` scope: {line}");
            }
        }

        Assert.True(checkedSites > 0, "Expected to find at least one connection call site to check.");
    }
}
