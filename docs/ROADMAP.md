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
- **Planned:** MATCH-01 onward; no code exists for them.
- **Implemented:** FND-001…FND-019 (except FND-015, discarded) and CONS-001…CONS-008.
- **Tested:** 48 tests — boundaries, determinism contract, match isolation, migrations + seed, the full save contract including rollback and discard, headless vertical slice, Godot presentation path, and the exported release build.

**FOUNDATION-00: 🔵 module consolidated.** Every contract it declared is now exercised by something that fails when broken, on both 1.0 target platforms, including the packaged artifact.

**Verified by MATCH-00.** Consolidation predicted that two contracts would move when real football arrived, and named them. Both predictions held exactly:

- `SimulationSettings.FixedTimeStepMilliseconds` moved, 100 ms → 50 ms, with the `SimulationVersion` bump the contract required.
- `simulation_run` gained a scoreline (migration `0004`). The checkpoint boundary around it did **not** change.

Nothing else in the Foundation had to be reshaped to absorb a spatial simulation, which is what 🔵 was claiming.


## PLYR-00 — player contract

The minimum a player needs for a match to be simulable. No match rules yet.

| ID | Status | Deliverable |
|---|---|---|
| PLYR-001 | 🟢 | `PlayerPosition` closed enum + `PitchLine` grouping, total mapping both ways |
| PLYR-002 | 🟢 | `PlayerAttributes` — nine attributes on a validated 1–20 scale |
| PLYR-003 | 🟢 | Migration `0003` — attribute columns with `CHECK`, position constrained to the closed set |
| PLYR-004 | 🟢 | Seed carries hand-authored attributes for all 30 players |
| PLYR-005 | 🟢 | `SchemaVersions.Expected` + refusal to open a save or template on another version |
| PLYR-006 | 🟢 | Position and attributes surfaced through Application to the Godot screen |

**PLYR-00: 🟢** — 69 tests. Both squads are provably able to field a goalkeeper and every line, which is the precondition MATCH-00 needs to pick a starting eleven.

Deliberately **not** modelled, to avoid speculative structure: overall rating (derived presentation, not stored state), form, morale, hidden mentals, growth/aging, injuries, preferred foot. Each belongs to a milestone that exists.

## MATCH-00 — spatial deterministic match

Real football, headless. Chosen over an event-based possession model so that `MATCH-01` can render this simulation rather than invent a second one.

| ID | Status | Deliverable |
|---|---|---|
| MATCH-001 | 🟢 | `Vec2` / `Pitch` — metre-space geometry using only IEEE-exact arithmetic |
| MATCH-002 | 🟢 | `MatchSimulation` — fixed-step spatial loop owning its own state and RNG |
| MATCH-003 | 🟢 | Possession, passing, interception, tackling, shooting, saves, restarts, half-time |
| MATCH-004 | 🟢 | `SquadSelection` + 4-4-2 `Formation`, deterministic and RNG-free |
| MATCH-005 | 🟢 | Ordered `MatchEvent` stream + `MatchResult` with a full-state digest |
| MATCH-006 | 🟢 | Fixed timestep validated and revised to 50 ms; `SimulationVersion` → 2 |
| MATCH-007 | 🟢 | Migration `0004` — applied results carry the scoreline |
| MATCH-008 | 🟢 | Segment-based goal detection (fixes shot tunnelling) |
| MATCH-009 | 🟢 | Contested fifty-fifty resolution (fixes array-order scoring bias) |
| MATCH-010 | 🟢 | Balance tuned against 300 measured matches, numbers recorded in `MatchTuning` |
| MATCH-011 | 🔴 | `FastMatchSimulation` — not built, and not to be built before `PERF-001` |

**MATCH-00: 🟢** — 85 tests. Measured over 300 matches between evenly-rated squads: 3.14 goals and 28.3 shots per match, neither side structurally favoured, ~60 ms per match.

Two bugs found by measuring rather than reading, both documented in `ARCHITECTURE.md`:

- Fast shots tunnelled through the goal line when detection sampled the ball's position per tick.
- Loose balls were awarded by array order, handing one side a 1.5x scoring advantage between identical squads.

The foundation contracts held: match isolation, the checkpoint boundary and the deterministic apply order all absorbed real football without being reshaped. The only Foundation contract that moved is the one that was explicitly marked provisional — the timestep.

## MATCH-01 — authoritative visual match slice

The first Godot 11v11 presentation consumes MATCH-00 rather than re-simulating it.

| ID | Status | Deliverable |
|---|---|---|
| MATCH-01A | 🟢 | Sampled immutable spatial frames + deterministic regression tests |
| MATCH-01B | 🟢 | Application playback package with presentation metadata |
| MATCH-01C | 🟢 | Primitive 3D pitch, 22 actors, ball, controlled camera and HUD |
| MATCH-01D | 🟡 | Interpolation, event cues, pause and playback speed |
| MATCH-01E | 🟡 | Headless/export gates and runtime/memory measurements |

**Direction accepted:** stylised 3D presentation over the existing 2D metre-space simulation,
with native 2D UI. See ADR-0006 and `docs/MATCH-01.md`.

MATCH-01A/B are green after CI run #26. MATCH-01C is green after CI run #40 loaded a real
career, produced authoritative frames and instantiated the 3D match scene with 22 actors.
Interpolation, pause and 1x/2x/4x controls exist, but MATCH-01D stays yellow until semantic event
cues are visible. MATCH-01E stays yellow until retained-memory and Godot frame-time measurements
are recorded. Foundation on Ubuntu/Windows, both Godot smoke scenes and both 1.0 release exports
passed.

## Next canonical sequence

1. `MATCH-01` — first visual 11v11 slice in Godot (🟡 contract accepted; implementation next).
2. `TACT-00` — formation/roles/in-possession/out-of-possession behavior.
3. `COMP-00` — league/cup/calendar rules.
4. `CLUB-00` — persistent club/squad systems.
5. `CAREER-00` — first complete long-term club career loop.

Before real MATCH performance work, define `PERF-001`: reference hardware, active-world size and maximum acceptable round-advance time. Do not invent FastMatchSimulation before that measurement.

`ART-SPIKE-001` will separately test dynamic kit rendering at real field-sprite resolution before any UV lookup or palette-mask approach becomes an architectural contract.
