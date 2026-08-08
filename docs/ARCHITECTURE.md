# Architecture

## 1. Authority boundaries

The project distinguishes persistent authority from session authority:

1. `sql/migrations` + `sql/seeds` are canonical source inputs for the base world.
2. `world_template.db` is generated and never edited manually.
3. Creating a career copies the template to `saves/{SaveId}/world.db`.
4. Between sessions, that career database is the persistent authority.
5. While a career is open, `WorldState` in memory is the operational authority.
6. Persistence occurs at explicit transactional checkpoints. SQLite is not queried tick by tick.

Current Foundation checkpoints are manual/autosave/save-and-exit/end-of-match/day-advance concepts. Only metadata changes exist today; future career modules will persist dirty world aggregates through this boundary.

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

The small Godot `CompositionRoot` is deliberately the outermost wiring point. UI scripts do not contain SQL.

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

## 5. Headless-first simulation

`DeterministicSimulationProbe` is deliberately not football. It proves replayability and headless execution before real match logic exists. MATCH will replace/extend this with football state while preserving the same isolation and deterministic boundaries.

No `FastMatchSimulation` exists. Performance fidelity alternatives may only be introduced after a defined benchmark demonstrates a real need.

## 6. Godot

Godot is the primary presentation/runtime engine, not the authority for football rules. The Foundation project targets `net10.0` with `Godot.NET.Sdk/4.7.1` and is validated in CI as a normal .NET build. Interactive editor/export validation remains a developer-machine smoke test.

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
