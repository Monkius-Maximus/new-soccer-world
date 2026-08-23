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

Reference data — countries, cities, clubs, stadiums, players, competitions — is immutable content shipped in `world_template.db`. Career progress is what accumulates on top of it, and `simulation_run` (migration `0002`) is currently the only such aggregate. It records applied simulation results rather than football events; MATCH will add those. It exists so the checkpoint boundary has a real dirty aggregate to flush, instead of being a contract validated only by metadata timestamps.

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

The fixed timestep is centralized at `SimulationSettings.FixedTimeStepMilliseconds`. Its Foundation value is provisional until MATCH validates gameplay granularity. Once released simulation behavior depends on it, changing it requires a `SimulationVersion` bump.

`DeterminismGuardTests` turns each clause of this contract into a failing build when violated: banned entropy sources (`Random.Shared`, `new Random(`, wall-clock reads, `Guid.NewGuid()`, `Environment.TickCount`, `Stopwatch.GetTimestamp`, `RandomNumberGenerator`) anywhere on the deterministic path, `Dictionary`/`HashSet` in Core, any mutable static field in the Core assembly (found by reflection, ignoring compiler-generated members), and more than one declaration of the fixed timestep.

## 5. Headless-first simulation

`DeterministicSimulationProbe` is deliberately not football. It proves replayability and headless execution before real match logic exists. MATCH will replace/extend this with football state while preserving the same isolation and deterministic boundaries.

No `FastMatchSimulation` exists. Performance fidelity alternatives may only be introduced after a defined benchmark demonstrates a real need.

## 6. Godot

Godot is the primary presentation/runtime engine, not the authority for football rules. The Foundation project targets `net10.0` with `Godot.NET.Sdk/4.7.1` and is validated in CI as a normal .NET build.

**.NET 10 target validation:** `Godot.NET.Sdk/4.7.1` restores and compiles `game/SoccerDreamGame` against `net10.0` with no errors and no warnings. Beyond compiling, Godot 4.7.1 .NET itself imports the project and runs `Main.tscn` headless, where the scene reaches the Application layer and renders both seeded clubs and all 30 players. No incompatibility was found, so the target was not lowered.

`scripts/godot-smoke-test.sh` performs that run and CI gates on it, so the presentation path is covered by automation rather than by a developer remembering to open the editor. The scene mirrors its rendered text to stdout behind a `[SOCCER-SMOKE]` marker and quits when `SOCCER_SMOKE_EXIT=1`; interactive runs ignore both and stay open.

### Release export

`scripts/godot-export-test.sh` exports release builds for both 1.0 desktop targets from the tracked `game/SoccerDreamGame/export_presets.cfg`, then runs the exported Linux binary against a real career database. A working editor is not evidence that a packaged build works, so the artifact that would ship is the one verified. The Windows build cannot execute on a Linux runner; it is checked for being a PE32+ x86-64 image.

Two things about this are easy to get wrong and are worth writing down:

- **The solution has to be findable.** Godot's .NET export looks for a `.sln` inside the Godot project directory, but in a monorepo the solution lives at the root. `project.godot` therefore sets `dotnet/project/solution_directory="../.."`. Without it the export still exits 0 and still produces an executable — one with no .NET assemblies beside it, which segfaults the moment it launches.
- **Godot's exit code is not the signal.** A `.NET`-half failure is reported through the log while the process still exits 0, so the script greps the log and refuses to trust the status code.

`export_presets.cfg` is tracked rather than gitignored, because CI needs the export to be reproducible. Keep its paths relative and never put signing credentials in it.

## 7. Explicitly deferred

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
