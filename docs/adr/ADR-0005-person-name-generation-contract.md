# ADR-0005 — Person-name generation contract

- Status: Proposed
- Date: 2026-09-03

## Context

The game needs culturally scoped generated person names without coupling the runtime to
network services, a research database or an ambient random source. Earlier standalone
onomastics notes mentioned SplitMix64, while this repository already standardizes explicit
randomness on `Pcg32Random` under ADR-0004.

Names also have several football-facing forms. Recomputing those fields when a pack changes
would mutate existing careers, while silently borrowing a different culture or truncating a
shirt name would conceal bad content.

## Decision

The person-name subsystem reuses PCG32 and derives independent, versioned streams for each
semantic component. The derivation is FNV-1a 64 over a domain plus little-endian world seed,
person ID and UTF-8 stream name. Weighted selection uses UInt64 rejection sampling.

The algorithm identifier is `tp-names-pcg32-streams-v1`. Its language-neutral golden vectors
live outside the runtime implementation and must be matched by every port.

Generation has no silent culture/gender fallback and does not truncate display strings. An
empty requested pool, an exhausted distinct choice or an unsatisfied length limit fails with
`NameGenerationException`.

The exact generated result is persisted: primary and optional additional given/family names,
full/common/shirt/scoreboard forms, culture ID, pack version and algorithm version. Loading a
career never regenerates a name. Roster-level shirt and scoreboard collisions are resolved
before persistence; unresolved collisions require an editorial override.

Migration `0005` introduces `player_name`. During the compatibility phase,
`player.first_name`/`last_name` remain present and the repository verifies that they equal the
canonical primary components. Missing or divergent rows fail loudly. Schema version becomes 5.

## Consequences

- The repository keeps one PRNG family and gains semantic stream isolation.
- Content packs are data-only, offline and independently versioned.
- Existing saves are not upgraded in place; the current schema policy still requires a rebuilt
  world template and rejects another schema version.
- The full pt-BR prototype is not embedded in the game until redistribution and editorial gates
  are cleared. Tests use a deliberately small fixture.
- The standalone SplitMix64 proposal is superseded for integration with this repository.
