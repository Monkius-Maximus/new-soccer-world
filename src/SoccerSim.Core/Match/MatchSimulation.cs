using SoccerSim.Core.Domain;
using SoccerSim.Core.Simulation;

namespace SoccerSim.Core.Match;

/// <summary>
/// A spatial, fixed-timestep football match, run headless.
/// <para>
/// The simulation receives an immutable world snapshot and owns everything it mutates: two
/// teams, a ball, its own <see cref="IRandomSource"/>. It never writes to the world it was
/// launched from, and hands back a <see cref="MatchResult"/> for the Application to apply.
/// Two matches can therefore run at the same time without sharing a single mutable field.
/// </para>
/// </summary>
public sealed class MatchSimulation
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly MatchContext _context;
    private readonly IRandomSource _random;
    private readonly MatchTeam _home;
    private readonly MatchTeam _away;
    private readonly MatchPlayer[] _everyone;
    private readonly Vec2[] _targets;
    private readonly int[] _contenders;
    private readonly List<MatchEvent> _events = [];
    private readonly double _secondsPerTick;
    private readonly int _totalTicks;
    private readonly int _decisionInterval;

    private Vec2 _ballLocation;
    private Vec2 _ballVelocity;
    private MatchPlayer? _carrier;
    private int _lastToucherClubId;
    private bool _shotInFlight;
    private bool _shotContested;
    private int _shotPower;
    private int _tick;
    private ulong _digest = FnvOffsetBasis;

    private MatchSimulation(MatchContext context)
    {
        _context = context;

        // The match owns its randomness. Nothing outside this instance can advance it.
        _random = new Pcg32Random(context.Seed);

        _secondsPerTick = SimulationSettings.FixedTimeStepMilliseconds / 1000.0;
        _totalTicks = (int)(MatchTuning.MatchMinutes * 60.0 / _secondsPerTick);
        _decisionInterval = Math.Max(1, (int)(MatchTuning.DecisionIntervalSeconds / _secondsPerTick));

        var world = context.WorldSnapshot;
        _home = new MatchTeam(
            context.HomeClubId, SquadSelection.PickEleven(world.GetRoster(context.HomeClubId)), attackingPositiveX: true);
        _away = new MatchTeam(
            context.AwayClubId, SquadSelection.PickEleven(world.GetRoster(context.AwayClubId)), attackingPositiveX: false);

        _everyone = [.. _home.Players, .. _away.Players];
        _targets = new Vec2[_everyone.Length];
        _contenders = new int[_everyone.Length];
    }

    /// <summary>
    /// Runs a whole match and returns its result. This is the headless path, and it is written
    /// in terms of <see cref="Advance"/> on purpose: a renderer that steps the match sees the
    /// same code, so the two cannot drift into producing different football.
    /// </summary>
    public static MatchResult Run(MatchContext context)
    {
        var simulation = Begin(context);
        while (simulation.Advance())
        {
        }
        return simulation.Result;
    }

    /// <summary>
    /// Starts a match the caller advances itself, one fixed tick at a time. For a renderer:
    /// step, read <see cref="CurrentFrame"/>, draw, repeat.
    /// <para>
    /// The timestep is not negotiable here. ADR-0004 fixes it because a different tick length
    /// is a different simulation — intermediate events disappear — so a renderer advances the
    /// same 50 ms steps the headless run does and interpolates for display if it wants smooth
    /// motion.
    /// </para>
    /// </summary>
    public static MatchSimulation Begin(MatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var simulation = new MatchSimulation(context);
        simulation.KickOff(simulation._home);
        return simulation;
    }

    public bool IsFinished => _tick >= _totalTicks;

    /// <summary>
    /// Advances exactly one tick. Returns whether a tick was executed, so a caller loops
    /// <c>while (simulation.Advance())</c> and sees every tick of the match.
    /// </summary>
    public bool Advance()
    {
        if (IsFinished)
        {
            return false;
        }

        if (_tick == _totalTicks / 2)
        {
            Record(MatchEventKind.HalfTime, 0, 0);
            _home.SwapEnds();
            _away.SwapEnds();
            KickOff(_away);
        }

        AdvanceBall();
        MovePlayers();
        MixIntoDigest();

        _tick++;

        if (IsFinished)
        {
            Record(MatchEventKind.FullTime, 0, 0);
        }

        return true;
    }

    /// <summary>The finished match. Throws while the match is still running.</summary>
    public MatchResult Result => IsFinished
        ? new MatchResult(
            _home.ClubId,
            _away.ClubId,
            _home.Score,
            _away.Score,
            _context.Seed,
            _context.SimulationVersion,
            _totalTicks,
            _digest,
            _random.State,
            _events)
        : throw new InvalidOperationException(
            $"The match is at tick {_tick} of {_totalTicks}. Advance until it returns false.");

    /// <summary>
    /// What a renderer needs to draw the current tick: the ball, the twenty-two players, the
    /// clock and the score.
    /// <para>
    /// Every value is copied out. A frame is a snapshot, not a window onto the simulation's own
    /// state, so a renderer holding one cannot move a player and cannot change the result of
    /// the match it is watching. That is the same isolation ADR-0002 gives the world.
    /// </para>
    /// </summary>
    public MatchFrame CurrentFrame()
    {
        var players = new PlayerFrame[_everyone.Length];
        for (var i = 0; i < _everyone.Length; i++)
        {
            var player = _everyone[i];
            players[i] = new PlayerFrame(player.PlayerId, player.ClubId, player.Slot, player.Location);
        }

        return new MatchFrame(
            _tick,
            _totalTicks,
            Minute,
            _home.ClubId,
            _away.ClubId,
            _home.Score,
            _away.Score,
            _ballLocation,
            players);
    }

    private int Minute => (int)(_tick * _secondsPerTick / 60.0);

    private MatchTeam Opponent(MatchTeam team) => ReferenceEquals(team, _home) ? _away : _home;

    private MatchTeam TeamOf(MatchPlayer player) => player.ClubId == _home.ClubId ? _home : _away;

    private void Record(MatchEventKind kind, int clubId, int playerId) =>
        _events.Add(new MatchEvent(_tick, Minute, kind, clubId, playerId));

    /// <summary>A roll of "n in 1000", clamped so a rating can never make something certain.</summary>
    private bool Chance(int perThousand) =>
        _random.NextInt(1000) < Math.Clamp(perThousand, 1, 999);

    private void KickOff(MatchTeam takingTeam)
    {
        _home.ResetToKickOffShape();
        _away.ResetToKickOffShape();

        _ballLocation = Pitch.Centre;
        _ballVelocity = Vec2.Zero;
        _shotInFlight = false;

        var taker = takingTeam.Nearest(Pitch.Centre, includeGoalkeeper: false);
        taker.Location = Pitch.Centre;
        _carrier = taker;
        _lastToucherClubId = takingTeam.ClubId;

        Record(MatchEventKind.KickOff, takingTeam.ClubId, taker.PlayerId);
    }

    private void AdvanceBall()
    {
        if (_carrier is not null)
        {
            _ballLocation = _carrier.Location;

            if (_tick % _decisionInterval == 0)
            {
                Decide(_carrier);
            }

            return;
        }

        var from = _ballLocation;
        var to = from + (_ballVelocity * _secondsPerTick);

        // Segment test, not point sampling: a 25 m/s shot covers more than a metre per tick.
        if (Pitch.CrossesGoalLine(from, to, positiveXGoal: true))
        {
            ScoreGoal(_home.AttackingPositiveX ? _home : _away);
            return;
        }

        if (Pitch.CrossesGoalLine(from, to, positiveXGoal: false))
        {
            ScoreGoal(_home.AttackingPositiveX ? _away : _home);
            return;
        }

        if (!Pitch.IsInsidePlay(to))
        {
            GoOutOfPlay(to);
            return;
        }

        _ballLocation = to;
        _ballVelocity *= Math.Max(0.0, 1.0 - (MatchTuning.BallDragPerSecond * _secondsPerTick));

        TryClaimLooseBall();
    }

    private void TryClaimLooseBall()
    {
        // Collect everyone who can reach the ball, then find the closest.
        var contenders = 0;
        var best = double.MaxValue;

        for (var i = 0; i < _everyone.Length; i++)
        {
            var player = _everyone[i];
            var reach = player.IsGoalkeeper
                ? MatchTuning.KeeperBaseReach + (player.Attributes.Goalkeeping * MatchTuning.KeeperReachPerPoint)
                : MatchTuning.ControlRadius;

            var distance = player.Location.DistanceSquaredTo(_ballLocation);
            if (distance > reach * reach)
            {
                continue;
            }

            _contenders[contenders++] = i;
            if (distance < best)
            {
                best = distance;
            }
        }

        if (contenders == 0)
        {
            return;
        }

        var claimant = ResolveFiftyFifty(contenders, best);

        // A keeper reaching a shot is not the same as stopping it. Roll once per shot: a beaten
        // keeper lets the ball run on, so whether it ends up a goal is still decided by geometry.
        if (_shotInFlight && claimant.IsGoalkeeper && claimant.ClubId != _lastToucherClubId)
        {
            if (_shotContested)
            {
                return;
            }

            _shotContested = true;

            var odds = MatchTuning.KeeperSaveBaseChance
                       + (claimant.Attributes.Goalkeeping * MatchTuning.KeeperSavePerPoint)
                       - (_shotPower * MatchTuning.KeeperSavePenaltyPerShotPoint);

            if (!Chance(odds))
            {
                return;
            }

            Record(MatchEventKind.Save, claimant.ClubId, claimant.PlayerId);
        }

        _shotInFlight = false;
        _carrier = claimant;
        _lastToucherClubId = claimant.ClubId;
        _ballVelocity = Vec2.Zero;
    }

    /// <summary>
    /// Decides who actually comes away with a contested ball.
    /// <para>
    /// Players who reach the ball stop exactly on it, so two chasers routinely end a tick at the
    /// identical distance. Awarding that to whoever the loop met first would hand every fifty-fifty
    /// in the match to the same side — measurably worth over a goal a game. Genuine ties are
    /// contested instead, weighted by the attributes that decide a real loose ball.
    /// </para>
    /// </summary>
    private MatchPlayer ResolveFiftyFifty(int contenders, double bestDistanceSquared)
    {
        var tied = 0;
        var totalWeight = 0;

        for (var i = 0; i < contenders; i++)
        {
            var player = _everyone[_contenders[i]];
            var distance = player.Location.DistanceSquaredTo(_ballLocation);

            if (distance - bestDistanceSquared > MatchTuning.ContestedBallToleranceSquared)
            {
                continue;
            }

            _contenders[tied++] = _contenders[i];
            totalWeight += Weight(player);
        }

        if (tied == 1)
        {
            return _everyone[_contenders[0]];
        }

        var roll = _random.NextInt(totalWeight);
        for (var i = 0; i < tied; i++)
        {
            var player = _everyone[_contenders[i]];
            roll -= Weight(player);
            if (roll < 0)
            {
                return player;
            }
        }

        return _everyone[_contenders[tied - 1]];

        static int Weight(MatchPlayer player) =>
            player.Attributes.Strength + player.Attributes.Positioning;
    }

    private void Decide(MatchPlayer carrier)
    {
        var team = TeamOf(carrier);
        var opponents = Opponent(team);

        var challenger = opponents.Nearest(carrier.Location, includeGoalkeeper: false);
        var underPressure =
            challenger.Location.DistanceSquaredTo(carrier.Location) <= MatchTuning.PressureRadius * MatchTuning.PressureRadius;

        if (underPressure && AttemptTackle(challenger, carrier))
        {
            return;
        }

        var distanceToGoal = carrier.Location.DistanceTo(team.AttackingGoal);

        if (distanceToGoal <= MatchTuning.ShootingRange &&
            Chance(MatchTuning.ShotChancePerDecision + (carrier.Attributes.Shooting / 3)))
        {
            Shoot(carrier, team);
            return;
        }

        if (Chance(MatchTuning.PassChancePerDecision))
        {
            Pass(carrier, team, opponents);
        }
    }

    private bool AttemptTackle(MatchPlayer challenger, MatchPlayer carrier)
    {
        var attack = carrier.Attributes.Dribbling + carrier.Attributes.Strength;
        var defence = challenger.Attributes.Tackling + challenger.Attributes.Strength;

        // Ratings shift the odds around the base rate; neither side is ever certain.
        var odds = MatchTuning.TackleBaseChance + ((defence - attack) * 3);

        if (!Chance(odds))
        {
            return false;
        }

        Record(MatchEventKind.Tackle, challenger.ClubId, challenger.PlayerId);
        _carrier = challenger;
        _lastToucherClubId = challenger.ClubId;
        _ballLocation = challenger.Location;
        return true;
    }

    private void Shoot(MatchPlayer shooter, MatchTeam team)
    {
        var goal = team.AttackingGoal;

        // Worse finishers spray wider. The offset is spatial, so whether it stays between the
        // posts is decided by geometry rather than by a second roll.
        var spray = MatchTuning.ShotMaxSpray * (21 - shooter.Attributes.Shooting) / 20.0;
        var lateral = ((_random.NextInt(2001) - 1000) / 1000.0) * spray;

        var target = new Vec2(goal.X, goal.Y + lateral);
        var speed = MatchTuning.ShotBaseSpeed + (shooter.Attributes.Shooting * MatchTuning.ShotSpeedPerPoint);

        _carrier = null;
        _shotInFlight = true;
        _shotContested = false;
        _shotPower = shooter.Attributes.Shooting;
        _lastToucherClubId = shooter.ClubId;
        _ballLocation = shooter.Location;
        _ballVelocity = (target - shooter.Location).Normalised() * speed;

        Record(MatchEventKind.Shot, shooter.ClubId, shooter.PlayerId);
    }

    private void Pass(MatchPlayer passer, MatchTeam team, MatchTeam opponents)
    {
        var receiver = ChooseReceiver(passer, team);
        if (receiver is null)
        {
            return;
        }

        var travel = receiver.Location - passer.Location;
        var speed = MatchTuning.PassBaseSpeed + (passer.Attributes.Passing * MatchTuning.PassSpeedPerPoint);

        _carrier = null;
        _shotInFlight = false;
        _lastToucherClubId = passer.ClubId;
        _ballLocation = passer.Location;
        _ballVelocity = travel.Normalised() * speed;

        // A long pass across a crowded pitch is likelier to be read.
        var interceptor = opponents.Nearest(passer.Location + (travel * 0.5), includeGoalkeeper: false);
        var risk = MatchTuning.InterceptionBaseChance
                   + (interceptor.Attributes.Positioning * 8)
                   - (passer.Attributes.Passing * 7)
                   + (int)travel.Length;

        if (Chance(risk))
        {
            Record(MatchEventKind.PassIntercepted, interceptor.ClubId, interceptor.PlayerId);
            _carrier = interceptor;
            _lastToucherClubId = interceptor.ClubId;
            _ballLocation = interceptor.Location;
            _ballVelocity = Vec2.Zero;
            return;
        }

        Record(MatchEventKind.Pass, passer.ClubId, passer.PlayerId);
    }

    private MatchPlayer? ChooseReceiver(MatchPlayer passer, MatchTeam team)
    {
        MatchPlayer? best = null;
        var bestScore = double.MinValue;
        var goal = team.AttackingGoal;

        for (var i = 0; i < team.Players.Count; i++)
        {
            var mate = team.Players[i];
            if (ReferenceEquals(mate, passer) || mate.IsGoalkeeper)
            {
                continue;
            }

            var distance = mate.Location.DistanceTo(passer.Location);
            if (distance < 3.0 || distance > 40.0)
            {
                continue;
            }

            // Prefer a team-mate closer to goal than the passer, without ignoring the safe ball.
            var progress = passer.Location.DistanceTo(goal) - mate.Location.DistanceTo(goal);
            var score = progress - (distance * 0.25);

            if (score > bestScore)
            {
                bestScore = score;
                best = mate;
            }
        }

        return best;
    }

    private void ScoreGoal(MatchTeam scoringTeam)
    {
        scoringTeam.AddGoal();

        var scorer = _lastToucherClubId == scoringTeam.ClubId
            ? scoringTeam.Nearest(_ballLocation, includeGoalkeeper: false)
            : scoringTeam.Nearest(scoringTeam.AttackingGoal, includeGoalkeeper: false);

        Record(MatchEventKind.Goal, scoringTeam.ClubId, scorer.PlayerId);
        KickOff(Opponent(scoringTeam));
    }

    private void GoOutOfPlay(Vec2 exitPoint)
    {
        var restartingTeam = _lastToucherClubId == _home.ClubId ? _away : _home;

        if (_shotInFlight)
        {
            Record(MatchEventKind.ShotOffTarget, _lastToucherClubId, 0);
        }

        Record(MatchEventKind.OutOfPlay, restartingTeam.ClubId, 0);

        _shotInFlight = false;
        _ballLocation = Pitch.Clamp(exitPoint);
        _ballVelocity = Vec2.Zero;

        var restarter = restartingTeam.Nearest(_ballLocation, includeGoalkeeper: false);
        restarter.Location = _ballLocation;
        _carrier = restarter;
        _lastToucherClubId = restartingTeam.ClubId;
    }

    private void MovePlayers()
    {
        var defendingTeam = _carrier is null ? null : Opponent(TeamOf(_carrier));
        var presser = defendingTeam?.Nearest(_ballLocation, includeGoalkeeper: false);

        // Two passes on purpose. Targets depend on where players are, so deciding and moving in
        // one loop would let whoever is iterated first move into a position the next player then
        // reacts to — making the result depend on array order and handing the home side, which
        // happens to be first, a systematic advantage.
        for (var i = 0; i < _everyone.Length; i++)
        {
            _targets[i] = TargetFor(_everyone[i], presser);
        }

        for (var i = 0; i < _everyone.Length; i++)
        {
            _everyone[i].MoveTowards(_targets[i], _secondsPerTick);
        }
    }

    private Vec2 TargetFor(MatchPlayer player, MatchPlayer? presser)
    {
        var team = TeamOf(player);

        if (player.IsGoalkeeper)
        {
            // Hold a line between the ball and the middle of the goal, without straying far.
            var goal = team.DefendingGoal;
            var towardsBall = (_ballLocation - goal).Normalised();
            return goal + (towardsBall * 6.0);
        }

        if (ReferenceEquals(player, _carrier))
        {
            return team.AttackingGoal;
        }

        if (ReferenceEquals(player, presser))
        {
            return _ballLocation;
        }

        if (_carrier is null)
        {
            // Loose ball: the closest player of each side goes for it.
            var chaser = team.Nearest(_ballLocation, includeGoalkeeper: false);
            if (ReferenceEquals(player, chaser))
            {
                return _ballLocation;
            }
        }

        // Everyone else holds shape, shifted towards where the play actually is.
        var anchor = Formation.Anchor(player.Slot, team.AttackingPositiveX);
        var drift = (_ballLocation - Pitch.Centre) * 0.30;
        return Pitch.Clamp(anchor + drift);
    }

    /// <summary>
    /// Folds the whole match state into a rolling hash. Two runs that agree here agree on every
    /// position, every score and every random draw — a cheap, total replay check.
    /// </summary>
    private void MixIntoDigest()
    {
        Mix((ulong)_tick);
        Mix((ulong)_home.Score);
        Mix((ulong)_away.Score);
        Mix(Quantise(_ballLocation.X));
        Mix(Quantise(_ballLocation.Y));

        for (var i = 0; i < _everyone.Length; i++)
        {
            Mix(Quantise(_everyone[i].Location.X));
            Mix(Quantise(_everyone[i].Location.Y));
        }
    }

    private void Mix(ulong value) => _digest = unchecked((_digest ^ value) * FnvPrime);

    /// <summary>Millimetre-resolution integer view of a coordinate, so the digest is exact.</summary>
    private static ulong Quantise(double metres) => unchecked((ulong)(long)(metres * 1000.0));
}
