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
- **Planned:** COMP-00 onward; no code exists for them.
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

## MATCH-01 — visual 11v11 slice

The reason MATCH-00 was built spatially: the scene renders that simulation rather than a second one.

| ID | Status | Deliverable |
|---|---|---|
| MATCH-101 | 🟢 | Stepwise simulation API (`Start` / `Step` / `Snapshot`); `Run` is now a loop over it |
| MATCH-102 | 🟢 | Test proving a stepped match digests identically to a batch match |
| MATCH-103 | 🟢 | `CareerApplication.StartMatch` — presentation gets a match from a use case |
| MATCH-104 | 🟢 | `Match.tscn` / `MatchView` — pitch, 22 players, ball, clock and score, drawn per tick |
| MATCH-105 | 🟢 | Frame-rate independence: wall-clock time is spent in whole ticks, never passed to the sim |
| MATCH-106 | 🟢 | Formation-shape test (defence behind midfield behind attack, per attacking direction) |
| MATCH-107 | 🟢 | CI gate: console and rendered matches must produce the same digest for the same seed |
| MATCH-108 | 🟢 | Optional PNG capture, so a headless run can produce visual evidence |

**MATCH-01: 🟢** — 93 tests. Verified: the rendered match and the console match agree on digest `2107336527299BBE` for seed 123456790, and a captured frame shows both 4-4-2 shapes, the goalkeepers and the ball.

Deliberately not here: kits, sprites, animation, camera work, replays and UI chrome. `ART-SPIKE-001` owns rendering fidelity; this slice is about the simulation being watchable and provably the same one.

## TACT-00 — formations, roles and phases

Before this, every club in the world played an identical hard-coded 4-4-2, and positioning ignored whether the team had the ball.

| ID | Status | Deliverable |
|---|---|---|
| TACT-001 | 🟢 | `FormationShape` — validated data (11 slots, one keeper), three real shapes shipped |
| TACT-002 | 🟢 | `PlayerRole` defined purely by how far a slot pushes forward and drops back |
| TACT-003 | 🟢 | Two phases: `PhaseAnchor` differs with and without the ball |
| TACT-004 | 🟢 | Three instructions on the 1–20 scale, each feeding an existing decision |
| TACT-005 | 🟢 | `SquadSelection` fills the chosen formation instead of a fixed 4-4-2 |
| TACT-006 | 🟢 | Migration `0005` + seed — the two Recife clubs deliberately set up differently |
| TACT-007 | 🟢 | `ShotBlocked` event, so every shot has a named outcome |
| TACT-008 | 🟢 | `SimulationVersion` → 3 |

**TACT-00: 🟢** — 114 tests. Measured over 200 matches per setup between identically-rated squads, so any difference is tactical rather than ability:

| Setup | goals | shots | off | blocked | saved | split |
|---|---|---|---|---|---|---|
| balanced vs balanced | 3.29 | 29.4 | 9.7 | 0.1 | 16.3 | 1.78 – 1.51 |
| 4-3-3 press vs 5-3-2 block | 3.14 | 34.5 | 10.5 | 2.2 | 16.7 | 2.29 – 0.84 |
| high press vs low block | 3.39 | 29.4 | 9.5 | 0.1 | 16.3 | 2.15 – 1.24 |

Between identically-rated squads, the setup decides the match: an attacking press beats a low block 2.29 goals to 0.84. The settings move results rather than decorating a menu.

Two things found by measurement rather than by reading:

- **The shot column did not add up.** Seven shots a match were neither saved, off target nor goals. Defenders were blocking them and the event stream had no way to say so; `ShotBlocked` closes that, and a test now asserts every shot ends in an outcome the stream names.
- **A deep block collapsed onto its own goal line.** Phase offsets plus the shift towards the ball pushed every target past the touchline, where clamping stacked the whole team into a single column — visible on screen, invisible in the digest. Drift is now capped and outfielders hold shape off the goal line. A test asserts a team keeps its spread, ignoring the players actually chasing the ball, since those are doing a job rather than holding shape.

Deliberately absent: tempo, width, offside traps, set-piece routines, and per-player instructions. Nothing in the simulation reads them, and an instruction that changes nothing is a lie in the UI.

## COMP-00 — league season and calendar

Before this, a career could only play friendlies. Results were recorded but nothing tied one match to the next.

| ID | Status | Deliverable |
|---|---|---|
| COMP-001 | 🟢 | `RoundRobinScheduler` — deterministic double round-robin, byes for odd entrant counts |
| COMP-002 | 🟢 | `Season` — a cursor over a generated calendar; fixtures derived, never stored |
| COMP-003 | 🟢 | `LeagueTable` — standings recomputed from results, total tie-break order |
| COMP-004 | 🟢 | `MatchSeeds` — seeds derived from career + fixture, not from play order |
| COMP-005 | 🟢 | `AppliedSimulationRun` carries competition and fixture; friendlies stay ordinal zero |
| COMP-006 | 🟢 | Migration `0006` — `season`, `season_club`, and the two new `simulation_run` columns |
| COMP-007 | 🟢 | Seed — the two Recife clubs contest a league; a new career starts at matchday zero |
| COMP-008 | 🟢 | `CareerApplication.AdvanceMatchday` — plays a round, then moves the season on |
| COMP-009 | 🟢 | `SeasonQueryService` + `Season.tscn` — the table on screen, through a use case |
| COMP-010 | 🟢 | CI gate: the rendered table and the console table must be identical |
| COMP-011 | 🔴 | Cup / knockout format — deferred with a stated reason, see below |

**COMP-00: 🟢** — 176 tests. Measured on a synthetic 20-club league, the largest shape the scheduler is tested against:

| Measure | Value |
|---|---|
| Fixtures in a season | 380 across 38 matchdays |
| Time to play the season (Release) | 21.3 s, 56.2 ms per match |
| Goals per match | 3.24 |
| Home / draw / away | 137 / 99 / 144 |

The per-match cost is unchanged from MATCH-00's ~60 ms and flat in world size — 56 ms per match at 2 clubs and at 20 — so a season costs what its matches cost and nothing more. With identical squads and no home advantage modelled, home and away wins land within a handful of each other across 380 matches, which is the same absence of structural bias MATCH-009 established for a single fixture.

The scheduler was written test-first, deliberately: round-robin generators fail subtly — a pair that meets three times, a club playing twice in a round, a season quietly one match short — and none of those are visible by reading the code. Twenty-six tests cover every entrant count from 2 to 20, and all passed on the first run.

Two guards were verified by injecting the violation they claim to catch, as every guard in this repo is:

- Corrupting the rendered table by one character made the CI comparison fail, so the gate is real rather than decorative.
- The `0006` `CHECK` constraints reject a result that half belongs to a competition, a competition type nothing can schedule, and a second season row. All three are asserted by tests that expect the insert to be refused.

**The cup is deliberately not built.** The shipped world has two clubs. A league of two is degenerate but real — a home-and-away pair with a table that adds up. A cup of two is a single final. Bracket generation, seeding, byes and replay rules would all be designed against imagined content, which is exactly the speculative abstraction this project forbids. The cup belongs with the milestone that ships enough clubs to need one.

Also deliberately absent: promotion and relegation, multiple divisions, fixture postponement, congestion, and any calendar finer than one round a week. Nothing in the world exercises them yet.

## Next canonical sequence

1. `CLUB-00` — persistent club/squad systems.
2. `CAREER-00` — first complete long-term club career loop, which is where a second competition, and therefore a cup, first has real content behind it.

Before real MATCH performance work, define `PERF-001`: reference hardware, active-world size and maximum acceptable round-advance time. Do not invent FastMatchSimulation before that measurement.

`ART-SPIKE-001` will separately test dynamic kit rendering at real field-sprite resolution before any UV lookup or palette-mask approach becomes an architectural contract.
