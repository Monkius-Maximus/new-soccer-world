# ADR-0003 — Base world and career save model

- Status: Accepted
- Date: 2026-08-08

## Context

A base football database is mostly static source content, while a career mutates for many seasons. Treating one shared database as both would make updates and saves interfere with each other.

## Decision

Migrations and seeds generate `world_template.db`. A new career copies it to `saves/{SaveId}/world.db`. Between sessions the career database is persistent authority; during an open session `WorldState` in memory is operational authority. SQLite is written only at explicit transactional checkpoints.

## Consequences

Existing careers are isolated from later base-content updates. "Exit without saving" can discard changes since the last checkpoint. Future schema migrations must distinguish structural save upgrades from content updates.
