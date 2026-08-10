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
| FND-012 | 🟢 | Unit/integration/architecture tests — 32 passing; boundary tests verified by deliberate violation injection |
| FND-013 | 🟡 | CI — workflow authored and locally reproduced step by step; must still pass on GitHub runners |
| FND-014 | 🟡 | Godot smoke test — `net10.0` + `Godot.NET.Sdk/4.7.1` compiles clean; editor/export validation pending |
| FND-015 | 🔴 | Asset/render pipeline contract — intentionally removed from Foundation; use an ART spike later |
| FND-016 | 🟢 | SDK pin relaxed to `10.0.100` / `latestFeature` (see ADR-0001) |
| FND-017 | 🟢 | Transitive pin of `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 clearing GHSA-2m69-gcr7-jv3q |

### Legend applied to Foundation

- **Decided:** every ADR in `docs/adr` is Accepted.
- **Planned:** MATCH/PLYR sequence below; no code exists for it.
- **Implemented:** FND-001…FND-012, FND-016, FND-017.
- **Tested:** boundaries, determinism contract, match isolation, migrations + seed, career save/checkpoint, headless vertical slice.

**FOUNDATION-00 overall: 🟡** — locally green end to end; stays 🟡 until remote CI is green and the Godot editor smoke test is run on a developer machine. It becomes 🔵 only after MATCH consumes these boundaries without needing to reshape them.

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
