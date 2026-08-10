using System.Xml.Linq;

namespace SoccerSim.Architecture.Tests;

/// <summary>
/// Reads the repository as it exists on disk. Boundary rules are asserted against the
/// declared project graph and the source tree, because a compiled reference graph only
/// shows the dependencies the compiler happened to emit.
/// </summary>
internal static class RepoLayout
{
    public static string Root { get; } = FindRoot();

    public const string Core = "src/SoccerSim.Core/SoccerSim.Core.csproj";
    public const string Application = "src/SoccerSim.Application/SoccerSim.Application.csproj";
    public const string Infrastructure = "src/SoccerSim.Infrastructure/SoccerSim.Infrastructure.csproj";
    public const string Presentation = "game/SoccerDreamGame/SoccerDreamGame.csproj";

    /// <summary>Project file names referenced by <paramref name="projectRelativePath"/>, e.g. "SoccerSim.Core".</summary>
    public static IReadOnlyList<string> ProjectReferences(string projectRelativePath) =>
        ItemIncludes(projectRelativePath, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>NuGet package ids referenced by <paramref name="projectRelativePath"/>.</summary>
    public static IReadOnlyList<string> PackageReferences(string projectRelativePath) =>
        ItemIncludes(projectRelativePath, "PackageReference")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Every hand-written .cs file under a directory, excluding build output.</summary>
    public static IEnumerable<(string Path, string Text)> SourceFiles(string directoryRelativePath)
    {
        var directory = Path.Combine(Root, directoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(directory), $"Expected source directory '{directoryRelativePath}' to exist.");

        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{separator}.godot{separator}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (Path.GetRelativePath(Root, path), File.ReadAllText(path)));
    }

    private static IEnumerable<string> ItemIncludes(string projectRelativePath, string itemName)
    {
        var fullPath = Path.Combine(Root, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Expected project '{projectRelativePath}' to exist.");

        return XDocument.Load(fullPath)
            .Descendants(itemName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SoccerDreamGame.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
    }
}
