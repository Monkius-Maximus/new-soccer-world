# ADR-0001 — Monorepo and technical baseline

- Status: Accepted
- Date: 2026-08-08

## Context

The project combines a simulation core, persistence adapters, development tools and a Godot presentation layer. Splitting these into repositories now would add versioning and integration friction before the boundaries are proven.

## Decision

Use one monorepo. The 1.0 desktop baseline is Windows x86_64 and Linux x86_64. Foundation tooling targets `net10.0`. The presentation project uses Godot 4.7.1 .NET.

`global.json` pins the SDK as `10.0.100` with `rollForward: latestFeature` rather than an exact patch. The language target is what the baseline actually constrains — `net10.0` — and pinning one exact SDK patch made the repository unbuildable on any machine or runner carrying a different .NET 10 feature band. `latestFeature` keeps the floor at .NET 10 while accepting any newer .NET 10 SDK. CI derives its SDK from this file (`global-json-file`) so the pin has exactly one source of truth.

## Consequences

Cross-project changes remain atomic and CI can validate boundaries together. Other platforms are not 1.0 commitments. If Godot 4.7.1 demonstrates a concrete incompatibility with `net10.0`, the target may be lowered only with an evidence-backed ADR amendment.
