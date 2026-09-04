# ADR-0006 — Legacy repository boundary

- Status: Accepted
- Date: 2026-09-03

## Context

The original `SoccerDreamGame` repository contains mature football simulation work that predates the current `new-soccer-world` architecture. It includes a tick-based `PitchSimulation`, `TacticalPlayerBrain`, tactical weighting, influence maps, a minute-level `MatchEngine`, LOD resolvers and a meaningful regression-test suite.

The new repository has a cleaner domain/application/infrastructure boundary and a deliberately smaller 1.0 scope. Continuing both codebases as co-equal runtimes would create duplicated authority and make later integration harder.

## Decision

`Monkius-Maximus/new-soccer-world` is the canonical runtime repository.

`Monkius-Maximus/SoccerDreamGame` is legacy reference material. It may be mined for algorithms, tests, design decisions and prototypes, but it must not become a runtime dependency of the new project.

Migration is controlled by `docs/LEGACY_MIGRATION_MATRIX.md`.

## Rules

1. New domain contracts are authoritative over legacy types.
2. Legacy implementations must be adapted rather than bulk-copied.
3. High-value regression tests should be migrated before or together with the behavior they protect.
4. Godot presentation code is not migrated into Core; behavior is recovered through Application-facing contracts.
5. The old repository remains useful until the relevant migration gates are green.
6. A component can be retired from legacy consideration only after its replacement has equivalent or intentionally improved acceptance coverage.

## Consequences

### Positive

- One source of truth for the production runtime.
- Existing football engineering is preserved instead of discarded.
- The new deterministic/world-state architecture remains clean.
- Migration becomes auditable and reversible.

### Negative

- Some systems must be adapted twice: once to understand the old behavior and again to fit the new contracts.
- Legacy code cannot simply be referenced as a package.
- Some old abstractions will intentionally be retired.

## Follow-up

The next implementation gate is `PLYR-00`, because the current Player domain object is intentionally minimal while the legacy tactical and match systems require richer player inputs. After `PLYR-00`, recover the minimum legacy behavior needed by `MATCH-00` and `TACT-00`.
