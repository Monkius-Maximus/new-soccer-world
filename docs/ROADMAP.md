# Roadmap

This file describes implementation status, not just ideas.

| Status | Meaning |
|---|---|
| 🔴 | Cancelled / discarded |
| 🟠 | Started |
| 🟡 | In progress / requires attention |
| 🟢 | Completed |
| 🔵 | Module consolidated |

## Foundation

| ID | Status | Deliverable |
|---|---|---|
| FND-001 | 🟢 | Monorepo structure |
| FND-002 | 🟢 | Core / Application / Infrastructure projects |
| FND-003 | 🟢 | Godot 4.7.1 .NET project structure |
| FND-004 | 🟢 | Dependency boundaries + tests |
| FND-005 | 🟢 | Versioned SQL migrations |
| FND-006 | 🟢 | Reproducible `world_template.db` builder |
| FND-007 | 🟢 | Career snapshot + in-memory session architecture |
| FND-008 | 🟢 | Minimal country/city/club/stadium/player/competition model |
| FND-009 | 🟢 | Central fixed simulation timestep |
| FND-010 | 🟢 | Seeded deterministic RNG + replay test |
| FND-011 | 🟢 | Headless end-to-end runner |
| FND-012 | 🟢 | Unit/integration/architecture tests — 34 passing; every tripwire verified by deliberate violation injection |
| FND-013 | 🟢 | CI — green on ubuntu-latest and windows-latest |
| FND-014 | 🟢 | Godot smoke test — editor imports the project, scene renders both clubs headless, gated by CI |
| FND-015 | 🔴 | Asset/render pipeline contract — intentionally removed from Foundation; use an ART spike later |
| FND-016 | 🟢 | SDK pin relaxed to `10.0.100` / `latestFeature` (see ADR-0001) |
| FND-017 | 🟢 | `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 clearing GHSA-2m69-gcr7-jv3q |
| FND-018 | 🟢 | `Pooling=False` on every connection + `PersistenceContractTests` guarding it |
| FND-019 | 🟢 | `scripts/godot-smoke-test.sh` — headless Godot run promoted from manual check to CI job |

## Consolidation (CONS-00)

Hardening the Foundation contracts that existed only on paper. No football gameplay.

| ID | Status | Deliverable |
|---|---|---|
| CONS-001 | 🟢 | Migration `0002` — `simulation_run`, the first real career aggregate |
| CONS-002 | 🟢 | `WorldState` gains a controlled mutation surface + `HasUnsavedChanges` |
| CONS-003 | 🟢 | `Checkpoint` writes progress **and** metadata in one transaction |
| CONS-004 | 🟢 | Manual save / autosave / Save & Exit / discard-without-saving as real use cases |
| CONS-005 | 🟢 | Deterministic apply order independent of match arrival order |
| CONS-006 | 🟢 | Rollback proven: a failed checkpoint leaves the previous save intact |
| CONS-007 | 🟢 | Release export for Linux + Windows x86_64, exported binary executed in CI |
| CONS-008 | 🟢 | `dotnet/project/solution_directory` fix — export produced a launchable build |

### Legend applied

- **Decided:** every ADR in `docs/adr` is Accepted.
- **Planned:** PLYR-00 / MATCH-00 below; no code exists for them.
- **Implemented:** FND-001…FND-019 (except FND-015, discarded) and CONS-001…CONS-008.
- **Tested:** 48 tests — boundaries, determinism contract, match isolation, migrations + seed, the full save contract including rollback and discard, headless vertical slice, Godot presentation path, and the exported release build.

**FOUNDATION-00: 🔵 module consolidated.** Every contract it declared is now exercised by something that fails when broken, on both 1.0 target platforms, including the packaged artifact.

Two contracts remain deliberately provisional, and MATCH is expected to move them:

- `SimulationSettings.FixedTimeStepMilliseconds` — provisional until gameplay granularity is known. Changing it after release requires a `SimulationVersion` bump.
- `simulation_run` records applied *simulation* results, not football events. MATCH replaces its payload; the checkpoint boundary around it should not need to change.


## Next canonical sequence

1. `PLYR-00` — minimal football attributes/positions contract required by match simulation.
2. `MATCH-00` — football match state and deterministic headless rules.
3. `MATCH-01` — first visual 11v11 slice in Godot.
4. `TACT-00` — formation/roles/in-possession/out-of-possession behavior.
5. `COMP-00` — league/cup/calendar rules.
6. `CLUB-00` — persistent club/squad systems.
7. `CAREER-00` — first complete long-term club career loop.

Before real MATCH performance work, define `PERF-001`: reference hardware, active-world size and maximum acceptable round-advance time. Do not invent FastMatchSimulation before that measurement.

`ART-SPIKE-001` will separately test dynamic kit rendering at real field-sprite resolution before any UV lookup or palette-mask approach becomes an architectural contract.
