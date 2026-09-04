# Legacy Migration Matrix — SoccerDreamGame → new-soccer-world

Status: Active architectural control document
Date: 2026-09-03

## Purpose

This document prevents reimplementation and uncontrolled copying from the original `SoccerDreamGame` repository. The old repository is treated as a legacy research/codebase: valuable algorithms, tests and prototypes may be recovered, but the new repository's contracts remain authoritative.

## Authority rule

`new-soccer-world` is the canonical runtime and architecture. Legacy code is evidence, not authority.

Every migrated component must satisfy the current Core/Application/Infrastructure boundaries, current determinism contract, current domain model and current test gates.

## Current project state

The new repository has already completed the player contract (`PLYR-00`) and the first spatial deterministic match (`MATCH-00`). `MatchSimulation` owns isolated mutable match state and its own RNG; it receives a world snapshot and returns a `MatchResult` instead of mutating the source world. The next canonical product gate is `MATCH-01`, followed by `TACT-00`. Therefore legacy migration is now **targeted recovery for the next gates**, not prerequisite foundation work.

## Decision vocabulary

- **PORT** — implementation can be adapted with minimal semantic change.
- **REDESIGN** — concept is valuable, but API/domain assumptions must change first.
- **EXTRACT** — recover algorithms/tests as independent pieces rather than copying the containing system.
- **DEFER** — useful, but not needed for the current vertical slice.
- **REJECT** — conflicts with the current architecture or creates unnecessary complexity.

## Matrix

| Legacy component | Evidence | Decision | Destination / action | Gate |
|---|---|---|---|---|
| `PitchSimulation` | `src/SoccerSim.Core/Pitch/PitchSimulation.cs` | REDESIGN | Do not port wholesale. Compare its tick mechanics against current `MatchSimulation`; recover only mechanics missing from the current playable substrate | MATCH-01 |
| `TacticalPlayerBrain` | `src/SoccerSim.Core/Ai/TacticalPlayerBrain.cs` | PORT | Preserve decision contract and hysteresis concepts; adapt perception/player domain types to the completed `PLYR-00` contract | TACT-00 |
| `BehaviourWeights` | `src/SoccerSim.Core/Ai/BehaviourWeights.cs` | EXTRACT | Recover the modulation formula as a candidate implementation; redesign inputs around canonical Player + tactics + role/duty | TACT-00 |
| `PersonalityProfile` | `src/SoccerSim.Core/Pitch/PitchStates.cs` | REDESIGN / DEFER | Do not add hidden mental attributes merely because the legacy prototype has them. Introduce only the bounded dimensions proven necessary by the tactical model | TACT-00 |
| `InfluenceMap` | `src/SoccerSim.Core/Ai/InfluenceMap.cs` | PORT | Preserve as a tactical spatial service; keep it independent from rendering and match presentation | TACT-00 |
| `TeamTactics` / `Formation` / roles | `src/SoccerSim.Core/Tactics/*` | PORT | Canonicalize formation, role and duty vocabulary before importing behavior | TACT-00 |
| `MatchEngine` | `src/SoccerSim.Core/Simulation/MatchEngine.cs` | EXTRACT | Recover statistical resolver ideas and regression invariants; current `MatchSimulation` is authoritative for MATCH-00 | PERF-001 / future LOD |
| `Resolvers` / LOD tiers | `src/SoccerSim.Core/Simulation/Resolvers.cs` | REDESIGN / DEFER | Re-evaluate tier boundaries only after `PERF-001`; do not create alternate simulation paths speculatively | PERF-001 |
| `MatchPresentationService` | `src/SoccerSim.Core/Simulation/MatchPresentation.cs` | REJECT/REBUILD | Presentation stays outside Core; recover behavioral requirements and rebuild through current Application contracts | MATCH-01 |
| old Godot `MatchScene` | `game/scenes/match/MatchScene.cs` | EXTRACT | Recover UX/prototype behavior only; rebuild the visual match scene around current `MatchSimulation` output | MATCH-01 |
| deterministic RNG tests | `tests/SoccerSim.Core.Tests/*` | PORT | Migrate high-value invariants and seed reproducibility tests where current coverage does not already subsume them | MATCH / regression |
| score/scorer invariants | `MatchEngineTests.cs` and related tests | PORT | Compare against current `MatchRulesTests` and retain any missing invariant as regression coverage | MATCH-00 |
| tactical oscillation/hysteresis tests | `TacticalBrainTests.cs` | PORT | Re-establish as regression tests when `TACT-00` lands | TACT-00 |
| tactics validation tests | `TacticsValidationTests.cs` | PORT | Preserve fail-fast validation behavior under canonical models | TACT-00 |

## What has already been recovered conceptually

The old repository is not being treated as a lost project. Its strongest contributions have already influenced the new architecture: deterministic match execution, isolated match state, event streams, spatial simulation, tactical decision concepts and regression-oriented testing. The new repository now owns the actual implementations and contracts.

## Migration order from the current point

1. Use the current `MatchSimulation` as the baseline; do not fork the match engine.
2. Build `MATCH-01` and prove that the existing simulation can drive the first visual 11v11 slice.
3. Port only legacy mechanics that the visual slice exposes as missing or insufficient.
4. Implement `TACT-00` using the old AI as an evidence source, with current domain contracts as authority.
5. Port the old tactical regression tests alongside each behavior.
6. Define `PERF-001` from measurements of the real active-world workload.
7. Only then decide whether LOD/resolver work from the legacy repository is needed.

## Explicit non-goals

- No bulk copy of the old `src/` tree.
- No direct reference from the new repository to the old repository.
- No Godot dependency inside `SoccerSim.Core`.
- No SQLite calls from match ticks.
- No `FastMatchSimulation` before measured performance requires it.
- No expansion into Life World systems during the football vertical slice.

## Definition of done

A migrated component is considered complete only when:

- its current ownership layer is explicit;
- its public contract is compatible with the new domain model;
- deterministic behavior is covered by tests where applicable;
- no legacy namespace/project dependency remains;
- the component is exercised by the current CI/headless path when appropriate.
