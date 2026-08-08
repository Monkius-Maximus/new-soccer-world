# ADR-0002 — Engine-agnostic simulation core

- Status: Accepted
- Date: 2026-08-08

## Context

Football rules and career state must remain testable headlessly and must not be trapped inside a renderer or scene graph.

## Decision

`SoccerSim.Core` references neither Godot nor SQLite. `SoccerSim.Application` references Core. `SoccerSim.Infrastructure` references Core and implements its persistence ports. Godot is an outer presentation/composition layer.

## Consequences

The same match simulation can later run visually or headlessly. Godot-specific state cannot become football authority. Architecture tests guard these boundaries.
