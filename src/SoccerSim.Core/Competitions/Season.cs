namespace SoccerSim.Core.Competitions;

/// <summary>
/// A competition in progress.
/// <para>
/// Fixtures are generated from the entrants and the start date rather than stored, because
/// nothing yet postpones or redraws them, and a stored list that only ever mirrors the
/// generator would be a second source of truth. A postponement feature is what would make a
/// fixture table earn its place.
/// </para>
/// </summary>
public sealed record Season(int CompetitionId, DateOnly StartDate, int CurrentMatchday, IReadOnlyList<int> ClubIds)
{
    /// <summary>Matchday numbering starts at 1; 0 means the season has not kicked off.</summary>
    public const int NotStarted = 0;

    // Deliberately not cached in a field: `with` copies fields without re-running the
    // constructor, so a cache would survive a change to the entrants or the start date and
    // quietly answer with the old calendar.
    public IReadOnlyList<Fixture> Fixtures =>
        RoundRobinScheduler.Build(CompetitionId, ClubIds, StartDate);

    /// <summary>
    /// Asked of the scheduler rather than counted off a generated list, so the common questions
    /// — is the season over, which round is next — do not rebuild the whole calendar.
    /// </summary>
    public int TotalMatchdays => RoundRobinScheduler.MatchdayCount(ClubIds.Count);

    public bool IsComplete => CurrentMatchday >= TotalMatchdays;

    public IReadOnlyList<Fixture> FixturesFor(int matchday) =>
        [.. Fixtures.Where(f => f.Matchday == matchday)];

    /// <summary>The round that <see cref="CurrentMatchday"/> has not yet reached.</summary>
    public IReadOnlyList<Fixture> NextFixtures() =>
        IsComplete ? [] : FixturesFor(CurrentMatchday + 1);

    public DateOnly DateOf(int matchday) =>
        StartDate.AddDays((matchday - 1) * RoundRobinScheduler.DaysBetweenMatchdays);

    /// <summary>
    /// Value equality, entrants included. The compiler-generated version compares
    /// <see cref="ClubIds"/> by reference, so two seasons loaded from the same save would come
    /// back unequal — the same trap <c>MatchResult</c> hit with its event list.
    /// </summary>
    public bool Equals(Season? other) =>
        other is not null
        && CompetitionId == other.CompetitionId
        && StartDate == other.StartDate
        && CurrentMatchday == other.CurrentMatchday
        && ClubIds.SequenceEqual(other.ClubIds);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CompetitionId);
        hash.Add(StartDate);
        hash.Add(CurrentMatchday);
        foreach (var clubId in ClubIds)
        {
            hash.Add(clubId);
        }

        return hash.ToHashCode();
    }
}
