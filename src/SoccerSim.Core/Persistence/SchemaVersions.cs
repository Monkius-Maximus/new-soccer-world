namespace SoccerSim.Core.Persistence;

public static class SchemaVersions
{
    /// <summary>
    /// The migration version this build reads and writes. It must match the highest file in
    /// <c>sql/migrations</c>.
    /// <para>
    /// There is no migrate-on-open path yet: a career database is a copy of a template built by
    /// one specific build. Opening a save written against a different schema therefore fails
    /// loudly instead of half-working, which is the honest behaviour until upgrade migrations
    /// exist. Adding a migration means bumping this and regenerating the template.
    /// </para>
    /// </summary>
    public const int Expected = 5;
}
