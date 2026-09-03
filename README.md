# New Soccer World / SoccerDreamGame

Canonical implementation repository for the offline-first football simulation project.

## Current status

**🔵 FOUNDATION-00**  ·  **🟢 PLYR-00**  ·  **🟢 MATCH-00**  ·  **🟡 MATCH-01 — first 3D playback slice**

CI is green on `ubuntu-latest` and `windows-latest`: 0 warnings, 0 errors, 87 tests, and Godot 4.7.1 .NET both runs the presentation scene and exports release builds for the two 1.0 desktop targets — the exported Linux binary is executed in CI against a real career database.

`MATCH-00` simulates football spatially on a fixed 50 ms timestep: twenty-two players and a ball in metre-space on a 105 x 68 pitch, with possession, passing, tackling and shooting resolved from distance and attributes. It runs headless in about 60 ms, replays exactly from a seed, and returns an ordered event stream that `MATCH-01` will render rather than re-derive.

The accepted presentation direction is **stylised low/mid-poly 3D over the authoritative 2D
metre-space simulation**, with controlled cameras and native 2D UI. Godot will consume sampled
states from Core and may interpolate them; it will not re-simulate football. See
[`ADR-0006`](docs/adr/ADR-0006-hybrid-3d-presentation.md) and
[`MATCH-01`](docs/MATCH-01.md).

Measured over 300 matches between evenly-rated squads: **3.14 goals and 28.3 shots per match**, with neither side structurally favoured. The balance constants are tuned against that observation and recorded next to the numbers that produced them.

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

To verify what would actually ship — release exports for both desktop targets, plus running the exported Linux build — install the matching export templates and run:

```bash
scripts/godot-export-test.sh /path/to/Godot_v4.7.1-stable_mono_linux.x86_64
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
- `tests/SoccerSim.Architecture.Tests` — layering, determinism and persistence tripwires
- `scripts` — headless Godot smoke and release-export checks, both gated by CI
- `tests` — boundary, persistence and determinism tests
- `docs` — architecture, roadmap and accepted ADRs

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/ROADMAP.md`](docs/ROADMAP.md).
