# New Soccer World / SoccerDreamGame

Canonical implementation repository for the offline-first football simulation project.

## Current status

**🔵 FOUNDATION-00**  ·  **🟢 PLYR-00**  ·  **🟢 MATCH-00**  ·  **🟢 MATCH-01**  ·  **🟢 TACT-00**  ·  **🟢 COMP-00**

CI is green on `ubuntu-latest` and `windows-latest`: 0 warnings, 0 errors, 176 tests, and Godot 4.7.1 .NET runs all three scenes headless and exports release builds for the two 1.0 desktop targets, with the exported Linux binary executed against a real career database.

Football is simulated spatially on a fixed 50 ms timestep — twenty-two players and a ball in metre-space on a 105 x 68 pitch. `MATCH-01` draws that simulation tick by tick rather than replaying a recording, and CI fails if the console and rendered matches ever disagree.

`TACT-00` makes the shape a choice. A club picks a formation and three instructions — defensive line height, pressing intensity, directness — and each one changes a decision the match already makes. Measured over 200 matches between identically-rated squads, a low block cuts scoring from 3.35 to 2.48 goals a match, and a high press is worth 2.13 goals to 1.23 against one.

`COMP-00` makes results accumulate. A career plays a league season: a generated double round-robin, a weekly calendar, and a table recomputed from the results behind it. Match seeds are derived from the career and the fixture, so a season replays identically whether it was advanced one round at a time or all at once. `Season.tscn` shows the table, and CI fails if the rendered standings and the console ones ever disagree.

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

To see a league season played out, run `Season.tscn` with `SOCCER_SAVE_DB` set to a career `world.db`.

To watch a match, open the project and run `Match.tscn`. `SOCCER_MATCH_SPEED` sets the time multiplier (default 30x, so a full match takes about three minutes) and `SOCCER_MATCH_SEED` picks the match.

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
