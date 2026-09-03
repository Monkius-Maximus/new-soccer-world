# MATCH-01 — Authoritative visual match slice

## Objective

Render the existing MATCH-00 simulation as an observable 11v11 match in Godot without creating
a second source of football truth.

MATCH-01 is a presentation milestone. It does not add tactics, fouls, substitutions, injuries,
ball height, manual player control or final art.

## Product slice

The first slice shows:

- a 105 x 68 metre pitch;
- twenty-two distinguishable placeholder players;
- one ball;
- fixed broadcast/isometric camera;
- score and match clock;
- playback controls: pause, 1x, 2x and 4x;
- visible reactions to kick-off, passes, interceptions, tackles, shots, saves, goals, half-time
  and full-time;
- deterministic replay from the same match result.

Capsules, primitive meshes and flat team colours are acceptable. Final character assets are not.

## Missing contract in MATCH-00

`MatchResult.Events` explains discrete actions but does not preserve the spatial state between
them. Rendering players from independent Godot AI would invent a second match. MATCH-01
therefore introduces a sampled read model.

Suggested immutable contract:

```csharp
public readonly record struct MatchPlayerSample(
    int PlayerId,
    int ClubId,
    int Slot,
    Vec2 Location,
    double Stamina);

public sealed record MatchFrame(
    int Tick,
    int HomeScore,
    int AwayScore,
    Vec2 BallLocation,
    int? CarrierPlayerId,
    IReadOnlyList<MatchPlayerSample> Players);
```

`MatchResult` gains `IReadOnlyList<MatchFrame> Frames`.

## Sampling policy

- Capture one frame per simulated second: every 20 ticks at the current 50 ms timestep.
- Always capture tick 0, the final tick and a frame at every discrete match event.
- Keep the ordered event stream; frames do not replace semantic events.
- Sample creation is part of the deterministic Core path.
- The renderer interpolates positions only. It never changes the sampled state.
- Sampling cadence is a versioned presentation contract but must not consume RNG or affect the
  match digest.

At 5,400 regular samples, 22 compact player samples plus ball and metadata remain small enough
for the first slice. Memory and serialization measurements are acceptance evidence; assumptions
are not.

## Layer responsibilities

| Layer | Responsibility |
|---|---|
| Core | Produce deterministic events, result and sampled spatial frames |
| Application | Start a match and expose a read-only playback package |
| Godot adapter | Convert metre-space `Vec2` to Godot `Vector3` |
| Godot scene | Interpolate, animate, display UI and control playback time |
| Infrastructure | Persist the final career outcome only; no tick/frame reads |

## Playback rules

A 90-minute simulated match need not take 90 real minutes. The initial target is a configurable
12-minute presentation:

- playback clock advances 7.5 simulated seconds per real second at 1x;
- frames are interpolated using presentation time;
- event effects are triggered when playback crosses their tick;
- pause and speed changes never mutate `MatchResult`;
- seeking is deferred unless it falls out cheaply from the immutable frame list.

## Acceptance criteria

### Core

- Same context and seed produce value-equal frame sequences.
- Adding frames does not change the pre-MATCH-01 score, event stream, final RNG state or digest.
- Frames are ordered, contain exactly 22 unique players and remain inside pitch bounds.
- Event ticks have a corresponding frame.
- Sampling performs no random draw.

### Application

- A use case returns club names, player display data, events and frames without exposing
  repositories to Godot.
- Running playback does not dirty `WorldState`.
- Applying the finished outcome remains an explicit, separate operation.

### Godot

- Scene displays pitch, 22 players, ball, score and clock.
- Coordinate mapping follows ADR-0006.
- Headless smoke output proves the scene consumed a real MATCH-00 result.
- Linux and Windows release exports still pass.
- No SQL, SQLite type or match-rule calculation exists in presentation scripts.

### Performance evidence

Record, on the CI/reference runner:

- MATCH-00 runtime before and after frame capture;
- frame count;
- approximate retained bytes per result;
- Godot frame time with all 22 actors;
- exported build smoke result.

## Delivery sequence

1. `MATCH-01A` — sampled-state contract and regression tests.
2. `MATCH-01B` — Application playback DTO/use case.
3. `MATCH-01C` — primitive 3D pitch, actors, camera and HUD.
4. `MATCH-01D` — interpolation, event cues and playback controls.
5. `MATCH-01E` — headless/export gates and measurements.

## Explicitly deferred

- final character models and kits;
- dynamic kit shaders;
- manual control;
- tactical roles and phase behaviours (`TACT-00`);
- vertical ball physics;
- stadium crowd;
- replay cinematics;
- commentary;
- seamless city traversal.
