# Architecture

## 1. Authority boundaries

The project distinguishes persistent authority from session authority:

1. `sql/migrations` + `sql/seeds` are canonical source inputs for the base world.
2. `world_template.db` is generated and never edited manually.
3. Creating a career copies the template to `saves/{SaveId}/world.db`.
4. Between sessions, that career database is the persistent authority.
5. While a career is open, `WorldState` in memory is the operational authority.
6. Persistence occurs at explicit transactional checkpoints. SQLite is not queried tick by tick.

### The save contract

Checkpoint kinds are manual / autosave / save-and-exit / end-of-match / day-advance. They all commit identically — the distinction exists for the caller's UX, not for the storage layer.

What the boundary guarantees, and what `CheckpointSemanticsTests` asserts:

| Behaviour | Guarantee |
|---|---|
| Applying an outcome | Mutates `WorldState` only. The database is untouched, and `HasUnsavedChanges` flips to true. |
| Checkpoint | Writes progress **and** metadata in a single transaction, then clears the dirty flag. |
| Autosave | No-ops when nothing has been applied, so a periodic timer does not rewrite an unchanged career. |
| Save & Exit | Commits, then hands back the closed `CareerSave`. |
| Quit without saving | Writes nothing. Reopening yields the last committed state, losing exactly the work applied since. |
| Failed checkpoint | Rolls back whole. The previous save stays intact and readable. |

Reference data — countries, cities, clubs, stadiums, players, competitions — is immutable content shipped in `world_template.db`. Career progress is what accumulates on top of it, and `simulation_run` (migrations `0002` and `0004`) is currently the only such aggregate: one row per applied match, carrying the scoreline and the digest that proves which simulation produced it. Competition tables, standings and fixtures belong to `COMP-00`.

### Deterministic apply order

Matches are produced independently and hand back a `MatchOutcome` that holds no reference to the world. `CareerApplication.ApplyOutcomes` sorts them by `(home club, away club, seed)` before applying, rather than trusting arrival order, and assigns each one a career-local `Ordinal` as it lands.

That sort is what keeps the applied sequence replayable once matches run in parallel: the same set of outcomes always produces the same ordinals in the same order, regardless of which finished first. `WorldState.ApplySimulationRun` is the single writer, and an architecture test keeps the presentation layer and the storage adapters from calling it.

### Connection pooling must stay off

`Microsoft.Data.Sqlite` pools connections by default, and a pooled connection keeps the database file handle open after `Dispose`. On Windows — a first-class 1.0 target — an open handle makes the file impossible to copy, delete or replace, which breaks the two operations this architecture depends on most: copying `world_template.db` into a new career, and replacing a career database during Save & Exit. POSIX unlink semantics hide the problem entirely on Linux, so a green Linux run is not evidence.

Every connection string therefore sets `Pooling=False`, and `PersistenceContractTests` fails the build if a new connection factory omits it or if a call site opens a connection outside a `using` scope.

## 2. Dependency boundaries

```text
SoccerSim.Core
  ├─ domain model
  ├─ persistence ports
  └─ deterministic simulation primitives

SoccerSim.Application → SoccerSim.Core
SoccerSim.Infrastructure → SoccerSim.Core

Godot presentation
  ├─ invokes Application services for presentation behavior
  └─ uses Infrastructure only in its composition root to provide concrete adapters
```

`Core` knows neither Godot nor SQLite. `Application` does not reference Infrastructure. `Infrastructure` does not reference Application or Godot.

The small Godot `CompositionRoot` is deliberately the outermost wiring point, and the only presentation file allowed to name an Infrastructure type. Scenes obtain the world through the `CareerApplication` use case; they never construct a repository and never contain SQL.

These rules are enforced, not merely documented. `tests/SoccerSim.Architecture.Tests` asserts them against the `.csproj` graph and the source tree on disk rather than against a compiled reference graph, because the compiler only emits references that are actually used — an unused-but-forbidden dependency would slip past assembly-level checks. The suite covers: Core having no project or package references at all, Application referencing only Core, Infrastructure implementing the Core ports without reaching outward, and the Godot layer containing no SQL and no database package.

## 3. Match isolation

A match receives a snapshot/context and owns its random source and mutable match state. It cannot mutate the active career while running.

```text
WorldState
    ↓ snapshot
MatchContext A ──→ MatchResult A
MatchContext B ──→ MatchResult B
MatchContext C ──→ MatchResult C
                    ↓
            deterministic apply phase
                    ↓
                WorldState
```

This makes future parallel execution between matches possible without shared mutable match state.

## 4. Determinism contract

The Foundation promises exact reproduction only for:

**same build + same platform/architecture + same initial state + same inputs + same seed**.

Cross-platform bit-exact determinism is explicitly not promised. The deterministic path must not depend on process-global randomness, wall-clock time, GUID generation, incidental unordered collection traversal or shared mutable state.

The fixed timestep is centralized at `SimulationSettings.FixedTimeStepMilliseconds`. Foundation carried a provisional 100 ms; MATCH-00 revised it to **50 ms**, which is what that provision was for. At 100 ms a sprinting player covers most of a metre per tick and pressing resolves in visible jumps, which would also make MATCH-01's visual slice stutter. Ball tunnelling is handled by segment tests rather than by tick rate, so this was a motion-quality decision, not a correctness one.

`SimulationVersion` is now **2**. It is bumped whenever results for a given seed would change — tick length, tuning constants, or the rules themselves — and every stored result records the version that produced it, so a replay against a different version is detectable rather than silently wrong.

`DeterminismGuardTests` turns each clause of this contract into a failing build when violated: banned entropy sources (`Random.Shared`, `new Random(`, wall-clock reads, `Guid.NewGuid()`, `Environment.TickCount`, `Stopwatch.GetTimestamp`, `RandomNumberGenerator`) anywhere on the deterministic path, `Dictionary`/`HashSet` in Core, any mutable static field in the Core assembly (found by reflection, ignoring compiler-generated members), and more than one declaration of the fixed timestep.

## 5. Player contract (PLYR-00)

A player carries the minimum a match needs to be simulable, and nothing else.

**Position** is a closed enum, not a free string. `player.position_code` is constrained by the schema to the ten seeded codes, and the loader parses it into `PlayerPosition`, so an unknown code becomes a load failure rather than a player the simulation silently skips. `PitchLine` groups positions into goalkeeper / defence / midfield / attack — the coarse grouping MATCH needs long before formations exist. TACT will layer roles on top of this rather than replace it.

**Attributes** are nine integers on a 1–20 scale, each earning its place by feeding a decision MATCH-00 has to make:

| Attribute | Match decision it feeds |
|---|---|
| Pace | who reaches a loose ball first |
| Stamina | how much of that survives to the 90th minute |
| Strength | physical duels, holding the ball up |
| Passing | whether an attempted pass finds its target |
| Shooting | whether a shot troubles the goal |
| Tackling | the defending half of a challenge |
| Dribbling | the attacking half of a take-on |
| Positioning | off-ball decision quality, both phases |
| Goalkeeping | shot stopping; outfielders simply rate low |

Deliberately absent: overall rating, form, morale, hidden mentals, growth curves, injuries. A rating in particular is derived presentation, not stored state — storing it would create two sources of truth the moment an attribute changes.

The scale is enforced twice on purpose. `PlayerAttributes` rejects out-of-range values in its constructor, and the schema carries matching `CHECK` constraints, so neither a bad loader nor hand-edited SQL can introduce a player the simulation cannot reason about.

### Schema compatibility

`SchemaVersions.Expected` names the migration version this build reads and writes, and an architecture test asserts it matches the highest file in `sql/migrations`.

There is no migrate-on-open path: a career database is a copy of a template built by one specific build. Opening a save whose `schema_version` differs therefore fails loudly, and creating a career from a stale template does too. That is the honest behaviour until upgrade migrations exist — half-reading a career whose schema moved underneath it is how saves get corrupted. Adding a migration means bumping the constant and regenerating `world_template.db`.

## 6. The match (MATCH-00)

A match is spatial and advances on the centralized fixed timestep. Twenty-two players and a ball hold positions in metres on a 105 x 68 pitch; possession, passing, shooting and tackling are resolved from distances and attributes rather than from an abstract possession counter. That choice was made so `MATCH-01` can render the same simulation instead of inventing a second one.

`MatchSimulation` receives a world snapshot and owns everything it mutates — two teams, a ball, its own `IRandomSource`. It never writes to the world it came from, and returns a `MatchResult` carrying the score, the ordered event stream and a digest. `CareerApplication` applies that result later, in the deterministic order described in section 1.

### Arithmetic discipline

The simulation restricts itself to `+`, `-`, `*`, `/` and `sqrt`. IEEE-754 requires those to be correctly rounded, so they produce identical results on any conforming machine. `Sin`, `Cos`, `Atan2`, `Pow`, `Exp` and `Log` carry no such requirement and can differ between platforms and runtime versions.

Nothing in football needs them: direction comes from normalising a difference vector, proximity from comparing squared distances. `DeterminismGuardTests` fails the build if one appears on the deterministic path.

**Observed, not promised.** CI run #8 produced byte-identical digests for the same seeds on `ubuntu-latest` (.NET 10.0.10) and `windows-latest` (.NET 10.0.11):

```
seed 123456789 -> DEF624ECB336F0E4   on both
seed 123456790 -> 2107336527299BBE   on both
```

That is what the arithmetic discipline was for, and it is encouraging. It is **not** a promise: two platforms, two seeds and one build is an observation, not a proof, and nothing in the code guarantees it holds for every CPU, every JIT and every future runtime. The contract in section 4 stays as written — same build, same platform. Anyone wanting a real cross-platform guarantee should treat this as a reason to invest in fixed-point arithmetic and a conformance suite, not as evidence the work is already done.

### Two failure modes worth naming

Both were found by measuring output over hundreds of matches, not by reading the code.

- **Ball tunnelling.** A shot travels over a metre per tick. Sampling the ball's position each tick lets fast shots pass straight through the goal line, and the harder the shot the likelier it is to be missed. Goal detection therefore tests the *segment* the ball travelled, not its endpoint.
- **Array-order bias.** Players who reach the ball stop exactly on it, so two chasers routinely end a tick at an identical distance. Awarding that ball to whichever player the loop met first handed every fifty-fifty to the same side, worth roughly a goal a game — a 1.5x scoring advantage between two identical squads. Genuine ties are now contested with a weighted roll, and a test asserts neither side is structurally favoured across sixty matches.

### Balance

`MatchTuning` holds every constant the balance depends on, with the measured output recorded beside it: 3.14 goals and 28.3 shots per match across 300 matches between evenly-rated squads. Real top-flight football sits near 2.7 goals and 25 shots. The numbers are tuned against observation, not asserted, and they are not claimed to be final.

No `FastMatchSimulation` exists. ADR-0008 sets the criterion — 100 matches per round in at most 10 seconds single-threaded on the reference machine — and orders the remedies, with a second engine last and requiring an ADR that supersedes it.

A match has been observed at roughly 60 ms. That figure is still an unverified past
observation: `tools/SoccerSim.Benchmark` now exists to measure it, but it has not yet been
run on the reference machine, and until it is, no performance claim here is evidence.

The measurement is split in two on purpose. The harness reports and never asserts, because
the criterion is only meaningful on the machine ADR-0008 names. `PerformanceGuardTests` is
the CI half, and it guards a ceiling ten times the per-match budget — loose enough that
runner variance cannot reach it, tight enough that a tenfold regression cannot hide. A
wall-clock assertion at the real budget would go red from noise, and a randomly red test
is one the team stops reading.

## 7. Godot

Godot is the primary presentation/runtime engine, not the authority for football rules. The Foundation project targets `net10.0` with `Godot.NET.Sdk/4.7.1` and is validated in CI as a normal .NET build.

**.NET 10 target validation:** `Godot.NET.Sdk/4.7.1` restores and compiles `game/SoccerDreamGame` against `net10.0` with no errors and no warnings. Beyond compiling, Godot 4.7.1 .NET itself imports the project and runs `Main.tscn` headless, where the scene reaches the Application layer and renders both seeded clubs and all 30 players. No incompatibility was found, so the target was not lowered.

`scripts/godot-smoke-test.sh` performs that run and CI gates on it, so the presentation path is covered by automation rather than by a developer remembering to open the editor. The scene mirrors its rendered text to stdout behind a `[SOCCER-SMOKE]` marker and quits when `SOCCER_SMOKE_EXIT=1`; interactive runs ignore both and stay open.

### Release export

`scripts/godot-export-test.sh` exports release builds for both 1.0 desktop targets from the tracked `game/SoccerDreamGame/export_presets.cfg`, then runs the exported Linux binary against a real career database. A working editor is not evidence that a packaged build works, so the artifact that would ship is the one verified. The Windows build cannot execute on a Linux runner; it is checked for being a PE32+ x86-64 image.

Two things about this are easy to get wrong and are worth writing down:

- **The solution has to be findable.** Godot's .NET export looks for a `.sln` inside the Godot project directory, but in a monorepo the solution lives at the root. `project.godot` therefore sets `dotnet/project/solution_directory="../.."`. Without it the export still exits 0 and still produces an executable — one with no .NET assemblies beside it, which segfaults the moment it launches.
- **Godot's exit code is not the signal.** A `.NET`-half failure is reported through the log while the process still exits 0, so the script greps the log and refuses to trust the status code.

`export_presets.cfg` is tracked rather than gitignored, because CI needs the export to be reproducible. Keep its paths relative and never put signing credentials in it.

## 8. Explicitly deferred

The following are not Foundation contracts:

- real football gameplay
- complete player attribute schema
- FastMatchSimulation
- runtime kit shader / UV lookup
- final sprite pipeline
- Life World
- Player Career
- transfer/scouting systems
- complete competition engine
