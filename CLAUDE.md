# CLAUDE.md

Working agreement for agents on this repository. Read it before touching anything.

## What this repository is

`Monkius-Maximus/new-soccer-world` is the **canonical runtime repository** for
SoccerDreamGame: an offline-first football ecosystem simulator with a
deterministic C# simulation core and Godot as a presentation consumer.

`Monkius-Maximus/SoccerDreamGame` is legacy reference material only. It is
mined, never referenced. See ADR-0006 and `docs/LEGACY_MIGRATION_MATRIX.md`.

## Authority order

When two documents disagree, the higher entry wins.

| # | Source | Kind |
|---|---|---|
| 1 | `docs/adr/*` — Accepted ADRs | **Binding.** Reversible only by a new ADR that supersedes the old one explicitly. |
| 2 | `docs/ARCHITECTURE.md`, `docs/ROADMAP.md` | **Descriptive.** What is actually built, and its status. |
| 3 | `docs/context/00-roadmap-guia.md` | **Intent.** Governance: closed decisions, scope, phase sequence. |
| 4 | `docs/context/02-revisao-arquitetura.md` | **Intent.** Technical consequences. Overrides `01`. |
| 5 | `docs/context/01-estrutura-canonica.md` | **Intent.** Conceptual model. Lowest. |

Entry 2 being *descriptive* matters: if the code diverges from intent, the code
is not automatically right. Reconcile it — by an ADR, or by fixing the code, or
by correcting the document. Never silently.

### The two roadmaps

There are two files called roadmap and they are not interchangeable.

- **`docs/ROADMAP.md`** — implementation status. The only place statuses are
  updated. `FND-*`, `CONS-*`, `PLYR-*`, `MATCH-*`.
- **`docs/context/00-roadmap-guia.md`** — the owner's guiding document,
  preserved verbatim. Phases, closed decisions, parallel tracks. Its Phase 0
  checkboxes are historical; Phase 0 shipped as `FOUNDATION-00` and was
  consolidated.

Never edit anything under `docs/context/`. Those are source documents.

## Rules

**Architecture**

- Godot never executes SQL. Godot calls `SoccerSim.Application`, which resolves
  through `SoccerSim.Core` ports and `SoccerSim.Infrastructure` adapters.
- `Core` knows neither Godot nor SQLite, and has no package references.
- `world_template.db` is a build artifact. It is never hand-edited and never
  committed. Sources are `sql/migrations/` and `sql/seeds/`.
- The in-memory `WorldState` is the session authority; SQLite is written at
  explicit checkpoints, never queried tick by tick.
- A match owns its state and its `IRandomSource` and cannot mutate the career
  it came from.

**Determinism**

- Determinism primitives come before game logic. Always.
- Banned on the deterministic path: `Random.Shared`, `new Random(`, wall-clock
  reads, `Guid.NewGuid()`, `Dictionary`/`HashSet` iteration in Core, mutable
  statics in Core, and the non-IEEE-exact math functions (`Sin`, `Cos`,
  `Atan2`, `Pow`, `Exp`, `Log`).
- The contract is scoped: same build + same platform + same seed. Cross-platform
  bit-exactness is observed, not promised.
- Bump `SimulationVersion` whenever results for a given seed would change.

These are enforced by `tests/SoccerSim.Architecture.Tests`, not by good
intentions. If a tripwire fires, fix the code — do not relax the tripwire.

**Code**

- No speculative code. Build what the current milestone requires, nothing else.
- One correct path per problem. No fallbacks, no alternatives.
- Throw on unmet preconditions. Never silence an error.
- Surgical changes. Fix root causes, not symptoms.

**Process**

- Nothing is 🟢 without a test. Nothing is 🔵 without an ADR.
- One atomic commit per deliverable, with a message that says what changed and
  why.
- If a request contradicts an Accepted ADR, say so instead of complying.
- Repository artifacts — code, docs, commits — are written in English. The
  `docs/context/` source documents stay in the language they were authored in.

## Owner decisions

An agent may not invent these. Three are settled; one is open.

| # | Decision | Status |
|---|---|---|
| A | 1.0 target platforms | **Closed** — Windows x86_64 + Linux x86_64 (ADR-0001) |
| B | .NET and Godot versions | **Closed** — `net10.0`, SDK `10.0.100`/`latestFeature`, Godot 4.7.1 .NET (ADR-0001) |
| C | Mod format | **Closed** — data-only mods, launcher-owned ordering, advisory provenance (ADR-0007). Implementation is `MOD-00`, not yet built. |
| D | Benchmark criterion | **Open.** Blocks `PERF-001`, which in turn blocks any `FastMatchSimulation`. Define the number before measuring. |

## Commands

```bash
dotnet build SoccerDreamGame.sln
dotnet test SoccerDreamGame.sln

dotnet run --project tools/SoccerSim.WorldBuilder  -- --output artifacts/world_template.db
dotnet run --project tools/SoccerSim.HeadlessRunner -- --template artifacts/world_template.db --seed 123456789

scripts/godot-smoke-test.sh  /path/to/Godot_v4.7.1-stable_mono_linux.x86_64
scripts/godot-export-test.sh /path/to/Godot_v4.7.1-stable_mono_linux.x86_64
```

CI runs build + tests + the Godot smoke and export checks on `ubuntu-latest`
and `windows-latest`. Zero warnings is the standing bar.
