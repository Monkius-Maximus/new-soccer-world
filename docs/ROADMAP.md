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
| FND-012 | 🟢 | Unit/integration/architecture tests authored |
| FND-013 | 🟡 | CI — workflow authored; must pass remotely |
| FND-014 | 🟡 | Godot smoke test — project authored; editor validation pending |
| FND-015 | 🔴 | Asset/render pipeline contract — intentionally removed from Foundation; use an ART spike later |

**FOUNDATION-00 overall: 🟡** until CI and local Godot editor smoke tests pass.

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
