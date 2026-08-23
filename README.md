# New Soccer World / SoccerDreamGame

Canonical implementation repository for the offline-first football simulation project.

## Current status

**🟢 FOUNDATION-00 — complete and verified on both 1.0 target platforms**

CI is green on `ubuntu-latest` and `windows-latest`: the solution builds with 0 warnings and 0 errors, 34 tests pass, the headless slice reproduces identical digests across separate processes for one seed while diverging for another, and Godot 4.7.1 .NET runs the presentation scene headless against a real career database.

It stays 🟢 rather than 🔵 until MATCH consumes these boundaries without reshaping them — see [`docs/ROADMAP.md`](docs/ROADMAP.md) for the two contracts most likely to move.

The first milestone intentionally contains no real football gameplay. It proves the technical path that later modules depend on:

`SQL migrations + seeds → world_template.db → career world.db → WorldState in memory → Application → isolated deterministic simulation → Godot presentation`

### Status semantics

- 🔴 Cancelled / discarded
- 🟠 Started
- 🟡 In progress / requires attention
- 🟢 Completed
- 🔵 Module consolidated

## Technical baseline

- Godot **4.7.1 .NET**
- .NET **10** / `net10.0` (`global.json` pins `10.0.100` with `rollForward: latestFeature`)
- First-class 1.0 desktop targets: **Windows x86_64** and **Linux x86_64**
- SQLite is a persistence boundary, never a tick-by-tick simulation dependency
- Simulation Core is engine-agnostic
- Determinism contract: same build + same platform/architecture + same initial state + same inputs + same seed => exact reproduction

## Quick start

```bash
dotnet restore SoccerDreamGame.sln
dotnet build SoccerDreamGame.sln

dotnet run --project tools/SoccerSim.WorldBuilder -- --output artifacts/world_template.db
dotnet run --project tools/SoccerSim.HeadlessRunner -- --template artifacts/world_template.db --seed 123456789
```

For the Godot smoke test, run it headless the way CI does:

```bash
scripts/godot-smoke-test.sh /path/to/Godot_v4.7.1-stable_mono_linux.x86_64
```

Or open `game/SoccerDreamGame/project.godot` in the .NET edition of Godot 4.7.1. Without `SOCCER_SAVE_DB`, the scene proves that the C# project loads. Set `SOCCER_SAVE_DB` to a generated career `world.db` to render seeded club/roster data.

## Repository map

- `src/SoccerSim.Core` — domain, persistence ports, deterministic simulation primitives
- `src/SoccerSim.Application` — use cases and presentation-facing queries
- `src/SoccerSim.Infrastructure` — SQLite adapters
- `tools/SoccerSim.WorldBuilder` — reproducible `world_template.db` builder
- `tools/SoccerSim.HeadlessRunner` — end-to-end deterministic foundation proof
- `game/SoccerDreamGame` — Godot .NET presentation smoke test
- `sql/migrations` — canonical schema evolution
- `sql/seeds` — canonical foundation seed content
- `tests/SoccerSim.Architecture.Tests` — layering and determinism-contract tripwires
- `tests` — boundary, persistence and determinism tests
- `docs` — architecture, roadmap and accepted ADRs

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/ROADMAP.md`](docs/ROADMAP.md).
