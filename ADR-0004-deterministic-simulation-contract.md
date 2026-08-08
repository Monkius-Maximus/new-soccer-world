# ADR-0004 — Deterministic simulation contract

- Status: Accepted
- Date: 2026-08-08

## Context

Headless simulation, reproducible bug reports and future parallel round processing require match runs to be replayable rather than dependent on ambient process state.

## Decision

Each match owns explicit state and its own seeded `IRandomSource`. Simulation uses one centralized fixed timestep. The deterministic path must avoid global random sources, wall-clock APIs, GUID generation, incidental unordered iteration and shared mutable state.

The guarantee is deliberately scoped to **same build + same platform/architecture + same initial state + same inputs + same seed**. Cross-platform bit-exact determinism is not promised.

Matches do not mutate career `WorldState` while executing. Results are applied later in deterministic order, permitting future parallelism between isolated matches.

## Consequences

Bugs can be reproduced using build/platform/state/input/seed information. Fixed-point cross-platform math is not required now. Changing released simulation semantics requires a `SimulationVersion` bump.
