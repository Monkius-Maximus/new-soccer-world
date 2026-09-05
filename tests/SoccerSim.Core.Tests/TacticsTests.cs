using SoccerSim.Core.Domain;
using SoccerSim.Core.Match;
using SoccerSim.Core.Simulation;
using SoccerSim.Core.Tactics;

namespace SoccerSim.Core.Tests;

/// <summary>
/// TACT-00: formation, roles and the two phases. The point of these tests is that a tactical
/// setting must change something — an instruction the simulation ignores is a lie in the UI.
/// </summary>
public sealed class TacticsTests
{
    private static MatchResult Play(ulong seed, TeamTactics? home = null, TeamTactics? away = null)
    {
        var world = TestWorld.Build(homeTactics: home, awayTactics: away);
        return MatchSimulation.Run(new MatchContext(
            TestWorld.HomeClubId, TestWorld.AwayClubId, seed,
            SimulationSettings.SimulationVersion, world.Snapshot()));
    }

    [Fact]
    public void Every_shipped_formation_is_a_legal_eleven()
    {
        Assert.NotEmpty(Formations.All);

        foreach (var formation in Formations.All)
        {
            Assert.Equal(FormationShape.PlayersOnPitch, formation.Slots.Count);
            Assert.Equal(1, formation.CountOf(PitchLine.Goalkeeper));
            Assert.Equal(PitchLine.Goalkeeper, formation.Slots[0].Line);

            // Every outfield slot sits on the pitch it will be placed on.
            foreach (var slot in formation.Slots)
            {
                Assert.True(Pitch.IsInsidePlay(slot.Anchor), $"{formation.Name}: anchor {slot.Anchor} is off the pitch.");
            }
        }
    }

    [Fact]
    public void Formation_names_round_trip_and_unknown_names_are_refused()
    {
        foreach (var formation in Formations.All)
        {
            Assert.Same(formation, Formations.Parse(formation.Name));
        }

        Assert.Throws<ArgumentException>(() => Formations.Parse("4-4-3"));
    }

    [Fact]
    public void A_formation_must_have_eleven_players_and_one_keeper()
    {
        var slots = Formations.FourFourTwo.Slots;

        Assert.Throws<ArgumentException>(() => new FormationShape("short", [.. slots.Take(10)]));
        Assert.Throws<ArgumentException>(() => new FormationShape("two keepers",
            [slots[0], slots[0], .. slots.Skip(2)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Tactical_instructions_outside_the_scale_are_refused(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TeamTactics(Formations.FourFourTwo, value, 10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TeamTactics(Formations.FourFourTwo, 10, value, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TeamTactics(Formations.FourFourTwo, 10, 10, value));
    }

    [Fact]
    public void Every_outfield_slot_stands_further_forward_in_possession_than_out_of_it()
    {
        // This is what "two phases" means concretely. A role whose push and drop were both zero
        // would show up here as a slot that never moves between phases.
        foreach (var formation in Formations.All)
        {
            var tactics = new TeamTactics(formation, 10, 10, 10);

            foreach (var attackingPositiveX in new[] { true, false })
            {
                var team = new MatchTeam(1, TestWorld.Eleven(), attackingPositiveX, tactics);
                var forward = attackingPositiveX ? 1.0 : -1.0;

                for (var slot = 1; slot < formation.Slots.Count; slot++)
                {
                    var attacking = team.PhaseAnchor(slot, inPossession: true);
                    var defending = team.PhaseAnchor(slot, inPossession: false);

                    Assert.True(forward * attacking.X > forward * defending.X,
                        $"{formation.Name} slot {slot} ({formation.Slots[slot].Role}) does not advance in possession.");
                }
            }
        }
    }

    [Fact]
    public void A_higher_defensive_line_pushes_the_defending_block_up_the_pitch()
    {
        var deep = new MatchTeam(1, TestWorld.Eleven(), true,
            new TeamTactics(Formations.FourFourTwo, defensiveLineHeight: 3, 10, 10));
        var high = new MatchTeam(1, TestWorld.Eleven(), true,
            new TeamTactics(Formations.FourFourTwo, defensiveLineHeight: 18, 10, 10));

        for (var slot = 1; slot < FormationShape.PlayersOnPitch; slot++)
        {
            Assert.True(
                high.PhaseAnchor(slot, inPossession: false).X > deep.PhaseAnchor(slot, inPossession: false).X,
                $"slot {slot} does not sit higher with a higher line.");
        }
    }

    [Fact]
    public void Changing_a_formation_changes_the_match()
    {
        var baseline = Play(4242UL, home: new TeamTactics(Formations.FourFourTwo, 10, 10, 10));
        var reshaped = Play(4242UL, home: new TeamTactics(Formations.FourThreeThree, 10, 10, 10));

        Assert.NotEqual(baseline.Digest, reshaped.Digest);
    }

    [Theory]
    [InlineData(3, 18)]
    [InlineData(1, 20)]
    public void Changing_an_instruction_changes_the_match(int low, int high)
    {
        var cautious = Play(99UL, home: new TeamTactics(Formations.FourFourTwo, low, low, low));
        var bold = Play(99UL, home: new TeamTactics(Formations.FourFourTwo, high, high, high));

        Assert.NotEqual(cautious.Digest, bold.Digest);
    }

    [Fact]
    public void Directness_raises_how_often_a_side_shoots()
    {
        // Measured across seeds: one match could differ by chance, a trend cannot.
        var patient = 0;
        var direct = 0;

        for (ulong seed = 1; seed <= 25; seed++)
        {
            patient += Shots(Play(seed, home: new TeamTactics(Formations.FourFourTwo, 10, 10, 3)), TestWorld.HomeClubId);
            direct += Shots(Play(seed, home: new TeamTactics(Formations.FourFourTwo, 10, 10, 18)), TestWorld.HomeClubId);
        }

        Assert.True(direct > patient,
            $"A direct side took {direct} shots and a patient one {patient}; directness is not reaching the decision.");
    }

    [Fact]
    public void Squad_selection_fills_the_lines_the_formation_asks_for()
    {
        var roster = TestWorld.Build().GetRoster(TestWorld.HomeClubId);

        foreach (var formation in Formations.All)
        {
            var eleven = SquadSelection.PickEleven(roster, formation);
            Assert.Equal(FormationShape.PlayersOnPitch, eleven.Count);

            // Slot 0 is always the keeper, whichever shape is picked.
            Assert.Equal(PlayerPosition.Goalkeeper, eleven[0].Position);
        }
    }

    [Fact]
    public void A_club_without_stored_tactics_still_plays()
    {
        var world = TestWorld.Build();
        Assert.Empty(world.ClubTactics);
        Assert.Equal(TeamTactics.Default, world.TacticsFor(TestWorld.HomeClubId));
    }

    [Fact]
    public void A_deep_block_stays_a_block_instead_of_collapsing_onto_its_goal_line()
    {
        // A low block defending a ball near its own goal had every target pushed past the
        // touchline, and clamping stacked the whole team in one column on the line. Nothing in
        // the digest notices that — it is only visible on screen, or here.
        var world = TestWorld.Build(
            homeTactics: new TeamTactics(Formations.FourThreeThree, 18, 18, 16),
            awayTactics: new TeamTactics(Formations.FiveThreeTwo, 1, 2, 4));

        var match = MatchSimulation.Start(new MatchContext(
            TestWorld.HomeClubId, TestWorld.AwayClubId, 321UL,
            SimulationSettings.SimulationVersion, world.Snapshot()));

        for (var step = 0; step < 6000; step++)
        {
            match.Step();

            if (step % 400 != 0)
            {
                continue;
            }

            var snapshot = match.Snapshot();

            foreach (var side in new[] { snapshot.HomeClubId, snapshot.AwayClubId })
            {
                // Chasers legitimately go wherever the ball is, including onto a goal line.
                // The shape is what the other nine are doing.
                var holdingShape = snapshot.Players
                    .Where(p => p.ClubId == side && !p.IsGoalkeeper)
                    .Where(p => p.Location.DistanceTo(snapshot.Ball) > 8.0)
                    .ToArray();

                if (holdingShape.Length < 6)
                {
                    continue;
                }

                // A team squashed onto one line has almost no spread along the pitch.
                var spread = holdingShape.Max(p => p.Location.X) - holdingShape.Min(p => p.Location.X);
                Assert.True(spread > 12.0,
                    $"club {side} collapsed to a {spread:F1} m column at tick {snapshot.Tick}.");
            }
        }
    }

    private static int Shots(MatchResult result, int clubId) =>
        result.Events.Count(e => e.Kind == MatchEventKind.Shot && e.ClubId == clubId);
}
