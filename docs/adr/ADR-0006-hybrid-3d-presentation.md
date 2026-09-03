# ADR-0006 — Hybrid 3D presentation over a 2D football simulation

- Status: Accepted
- Date: 2026-09-03

## Context

The project accumulated two incompatible presentation directions. Early exploration treated
2D sprites as canonical, including a possible Blender-to-sprite pipeline. Later world, character
and UI work assumed stylised 3D geometry with controlled cameras. Leaving both directions
"active" makes every asset, animation and MATCH-01 decision provisional.

The football simulation implemented by MATCH-00 is already spatial in a 105 x 68 metre
`Vec2` plane. Godot is already the presentation engine and the Core is deliberately independent
from it.

## Decision

The product uses a **hybrid presentation architecture**:

1. The authoritative football simulation remains 2D metre-space in `SoccerSim.Core`.
2. Matches are presented by Godot with stylised low/mid-poly 3D geometry.
3. Version 1.0 uses controlled broadcast/isometric cameras. A free gameplay camera is not a
   requirement.
4. Menus, HUD, maps and contextual panels remain native 2D UI.
5. The broader life world uses bounded 3D scenes for relevant locations. Cities are graphs of
   locations loaded on demand, not seamless fully simulated open worlds.
6. Godot consumes sampled authoritative match states and events. It may interpolate between
   samples for smoothness, but it may not re-decide possession, movement, shots or outcomes.
7. Character models may be reused to generate portraits, cards or promotional renders, but
   pre-rendered field sprites are not the canonical runtime representation.
8. Match height is presentation data until a football rule needs it. Ball flight and jumping may
   later add an explicit deterministic vertical component without replacing the `Vec2` pitch.

## Coordinate mapping

The simulation owns `(X, Y)` in metres. The adapter maps it into Godot as:

- simulation `X` → Godot `X` (pitch length);
- simulation `Y` → Godot `Z` (pitch width);
- Godot `Y` → presentation height.

No screen resolution, camera transform, model scale or animation state enters Core.

## Consequences

### Positive

- MATCH-00 remains the single football authority.
- One modular 3D character system can serve match, training, social scenes and close-ups.
- Camera, interpolation, animation and art can evolve without changing match results.
- The project avoids authoring eight-direction sprite sets for every body, kit and animation.
- Headless simulation and deterministic replay remain possible.

### Cost

- A modular character/animation pipeline is required.
- MATCH-01 needs an explicit sampled-state contract; the current event stream alone is not
  enough to render movement faithfully.
- 3D performance budgets, LOD and camera readability must be measured by dedicated spikes.

## Rejected alternatives

### Fully 2D runtime

Rejected as the canonical direction because it duplicates character assets across field, replay
and life-world contexts and makes the world-facing ambition disproportionately expensive.

### Full-physics 3D football authority

Rejected because it couples game rules to engine physics, weakens replayability and makes
headless simulation and testing substantially harder.

### Seamless global open world

Rejected for version 1.0. Global scope is represented through persistent data and a graph of
bounded locations.

## Guardrail

This ADR settles presentation direction, not final art style. Asset counts, topology, rig,
materials, LOD thresholds and kit composition remain deliverables of `ART-SPIKE-001`.
