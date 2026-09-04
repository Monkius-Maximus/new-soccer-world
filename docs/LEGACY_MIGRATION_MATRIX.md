# Legacy Migration Matrix — SoccerDreamGame → new-soccer-world

Status: Active architectural control document
Date: 2026-09-03

## Purpose

This document prevents reimplementation and uncontrolled copying from the original `SoccerDreamGame` repository. The old repository is treated as a legacy research/codebase: valuable algorithms, tests and prototypes may be recovered, but the new repository's contracts remain authoritative.

## Authority rule

`new-soccer-world` is the canonical runtime and architecture. Legacy code is evidence, not authority.

Every migrated component must satisfy the current Core/Application/Infrastructure boundaries, current determinism contract, current domain model and current test gates.

## Decision vocabulary

- **PORT** — implementation can be adapted with minimal semantic change.
- **REDESIGN** — concept is valuable, but API/domain assumptions must change first.
- **EXTRACT** — recover algorithms/tests as independent pieces rather than copying the containing system.
- **DEFER** — useful, but not needed for the current vertical slice.
- **REJECT** — conflicts with the current architecture or creates unnecessary complexity.

## Matrix

| Legacy component | Evidence | Decision | Destination / action | Gate |
|---|---|---|---|---|
| `PitchSimulation` | `src/SoccerSim.Core/Pitch/PitchSimulation.cs` | REDESIGN | Rebuild as the playable/tick match substrate behind current `MatchContext` and player contracts | MATCH-01 |
| `TacticalPlayerBrain` | `src/SoccerSim.Core/Ai/TacticalPlayerBrain.cs` | PORT | Preserve decision contract and hysteresis concepts; adapt perception/player domain types | TACT-00 |
| `BehaviourWeights` | `src/SoccerSim.Core/Ai/BehaviourWeights.cs` | EXTRACT | Keep as the single modulation function combining tactics + personality + role/duty; redesign inputs around canonical Player | PLYR-00 / TACT-00 |
| `PersonalityProfile` | `src/SoccerSim.Core/Pitch/PitchStates.cs` | REDESIGN | Retain bounded personality axes only if they demonstrably affect decisions; place them in canonical Player/Personality model | PLYR-00 |
| `InfluenceMap` | `src/SoccerSim.Core/Ai/InfluenceMap.cs` | PORT | Preserve as a tactical spatial service; avoid coupling it to rendering | TACT-00 |
| `TeamTactics` / `Formation` / roles | `src/SoccerSim.Core/Tactics/*` | PORT | Canonicalize formation, role and duty vocabulary before import | TACT-00 |
| `MatchEngine` | `src/SoccerSim.Core/Simulation/MatchEngine.cs` | EXTRACT | Recover minute-level resolver and invariants; do not introduce a faster resolver until PERF-001 | MATCH-00 / PERF-001 |
| `Resolvers` / LOD tiers | `src/SoccerSim.Core/Simulation/Resolvers.cs` | REDESIGN | Re-evaluate tier boundaries against measured performance and new WorldState/MatchContext contracts | PERF-001 |
| `MatchPresentationService` | `src/SoccerSim.Core/Simulation/MatchPresentation.cs` | REJECT/REBUILD | Presentation belongs outside Core; keep behavioral tests and rebuild against Application ports | MATCH-01 |
| old Godot `MatchScene` | `game/scenes/match/MatchScene.cs` | EXTRACT | Recover UX/prototype behavior only; rebuild scene against current Application API | ART/UI spike |
| deterministic RNG tests | `tests/SoccerSim.Core.Tests/*` | PORT | Migrate high-value invariants and seed reproducibility tests | FOUNDATION / MATCH-00 |
| score/scorer invariants | `MatchEngineTests.cs` and related tests | PORT | Become acceptance tests for current MatchResult | MATCH-00 |
| tactical oscillation/hysteresis tests | `TacticalBrainTests.cs` | PORT | Preserve as regression tests for decision stability | TACT-00 |
| tactics validation tests | `TacticsValidationTests.cs` | PORT | Preserve fail-fast validation behavior under canonical models | TACT-00 |

## Migration order

1. Freeze the legacy repository as reference material.
2. Complete `PLYR-00` in the new repository.
3. Port the smallest useful set of legacy player/tactical types and tests.
4. Build `MATCH-00` around the current `MatchContext`, `WorldState` snapshot and deterministic RNG contract.
5. Only after the background resolver is valid, build the tick substrate (`PitchSimulation` successor).
6. Measure performance (`PERF-001`) before creating any alternate simulation path.
7. Connect Godot only through Application-facing contracts.

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
