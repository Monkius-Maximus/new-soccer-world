# Roadmap

This file describes implementation status, not just ideas.

| Status | Meaning |
|---|---|
| 🔴 | Cancelled / discarded |
| 🟠 | Started |
| 🟡 | In progress / requires attention |
| 🟢 | Completed |
| 🔵 | Module consolidated |
| ⬜ | Decided / planned — no code yet |

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
- **Planned:** MATCH-01 onward, plus MOD-00; no code exists for them. PERF-001 is half-built: harness and guard ship, the measurement awaits the reference machine.
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
| MATCH-011 | 🔴 | `FastMatchSimulation` — not built; gated by ADR-0008, and last in its ordered remedies |

**MATCH-00: 🟢** — 85 tests. Measured over 300 matches between evenly-rated squads: 3.14 goals and 28.3 shots per match, neither side structurally favoured. A match was observed at roughly 60 ms, but no code in the repository measures it — `PERF-001` makes that reproducible (ADR-0008).

Two bugs found by measuring rather than reading, both documented in `ARCHITECTURE.md`:

- Fast shots tunnelled through the goal line when detection sampled the ball's position per tick.
- Loose balls were awarded by array order, handing one side a 1.5x scoring advantage between identical squads.

The foundation contracts held: match isolation, the checkpoint boundary and the deterministic apply order all absorbed real football without being reshaped. The only Foundation contract that moved is the one that was explicitly marked provisional — the timestep.

## MOD-00 — mod format

`ADR-0007` closes the owner decision on what a mod is, and every clause it states is now
enforced by something that fails when broken.

**MOD-00: 🟢** — migration `0005` adds `template_mod`, and `SchemaVersions.Expected` moves
to 5.

| ID | Status | Deliverable |
|---|---|---|
| MOD-001 | 🟢 | `manifest.json` parsing — four keys, unknown ones rejected by the serializer |
| MOD-002 | 🟢 | Ordered application over the base template in `SqliteWorldTemplateBuilder` |
| MOD-003 | 🟢 | DDL rejection by structural fingerprint, verified by deliberate violation |
| MOD-004 | 🟢 | `schema_version` mismatch refused loudly |
| MOD-005 | 🟢 | `SaveMetadata.ModList` + derived `ContentVersion`, advisory and never blocking |

Ordering lives in two places and neither is the manifest: between mods it is the
launcher's list position, within a mod it is the numeric filename prefix.

Unscheduled against the sequence below — modding is 🟡 in the 1.0 scope, so this
does not displace `MATCH-01`.

## PERF-001 — round-advance measurement

`ADR-0008` sets the criterion. The harness and the CI guard exist; the measurement
itself needs the reference machine, so it is the owner's to run.

| ID | Status | Deliverable |
|---|---|---|
| PERF-001a | 🟢 | `tools/SoccerSim.Benchmark` — runs N matches, reports ms/match, ns/tick and machine identity |
| PERF-001b | ⬜ | Reference machine specification recorded in ADR-0008 — blocked on a run by the owner |
| PERF-001c | ⬜ | First real measurement against the 100-matches-in-10s criterion — same block |
| PERF-001d | 🟢 | `--max-ms-per-match` in the benchmark's own CI step — 10x ceiling, measured where nothing competes |

The harness never judges the criterion itself — that is read by a person on the reference
machine. CI runs it with ten matches and applies the order-of-magnitude guard, in a step of
its own: the guard began as a unit test and failed on Windows at 1979 ms against a 1000 ms
ceiling, because `dotnet test` runs assemblies in parallel and the timing was competing
with a disk-heavy suite for two cores. Wall-clock inside a parallel test run measures the
scheduler.

.NET exposes no portable API for the CPU model, so the harness prints runtime, OS,
architecture and logical processor count and says plainly that the model has to be
recorded by hand. Two platform-specific code paths would have bought one string.

Single thread is the pessimistic bound, not a target: match isolation already
makes a round parallelise deterministically, and that headroom is excluded from
the criterion on purpose.

## Next canonical sequence

1. `MATCH-01` — first visual 11v11 slice in Godot.
2. `TACT-00` — formation/roles/in-possession/out-of-possession behavior.
3. `COMP-00` — league/cup/calendar rules.
4. `CLUB-00` — persistent club/squad systems.
5. `CAREER-00` — first complete long-term club career loop.

`PERF-001` is now defined by ADR-0008. `FastMatchSimulation` stays 🔴 and may only be introduced by an ADR that supersedes ADR-0008 and states why reducing detailed competitions and optimising the existing engine were insufficient.

`ART-SPIKE-001` will separately test dynamic kit rendering at real field-sprite resolution before any UV lookup or palette-mask approach becomes an architectural contract.
