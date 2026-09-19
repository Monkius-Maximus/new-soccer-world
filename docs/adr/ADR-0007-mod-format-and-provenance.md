# ADR-0007 — Mod format and template provenance

- Status: Accepted
- Date: 2026-09-09

## Context

`docs/context/02-revisao-arquitetura.md` §8 names the shape of a mod — a folder
with `manifest.json` and ordered `.sql` scripts — and stops there. Three
questions it leaves open each have a wrong answer that is expensive to reverse,
and two of them collide with contracts that already exist in code:

1. **Who owns ordering.** "Ordered scripts" does not say whether the order is a
   manifest field, a filename convention, or the launcher's list.
2. **Whether a mod may change schema.** `SchemaVersions.Expected` is a `const
   int` compiled into the binary, and both the template builder and the career
   store refuse to open a database whose version differs. A mod running `ALTER
   TABLE` makes that constant false: the template's schema would no longer be
   determined by the build that reads it.
3. **What a changed mod list does to an existing career.** `SaveMetadata` has no
   `ModList` field today, and `ContentVersion` is a free string that nothing
   computes — it is stored and printed, never derived.

## Decision

A mod is a directory containing `manifest.json` and numerically prefixed `.sql`
scripts, applied over the base template.

**Ordering has exactly two levels, and neither is a manifest field.** Order
*between* mods is the launcher's list: position in that list is the order.
Order *within* a mod is the numeric filename prefix, the same convention
`sql/migrations` already uses. There is no `load_order` key — it would be a
third way to express an order that is already fully determined, and it would let
two mods claim the same position.

**Mods are data-only.** They may `INSERT`, `UPDATE` and `DELETE`. DDL is
rejected by the builder, not merely discouraged, so `SchemaVersions.Expected`
stays true by construction rather than by author discipline. Schema belongs to
`sql/migrations` and to the build.

Data-only does not mean schema-independent: a mod inserting into `player`
depends on that table's shape. Each manifest therefore declares the
`schema_version` it targets, and the builder refuses a mod that targets a
different one.

The manifest carries four fields and no others: `id`, `name`, `version`,
`schema_version`.

**Provenance is advisory, never blocking.** `SaveMetadata` gains `ModList` —
the ordered `id` and `version` of every mod that produced the template. A career
is a *copy* of that template (ADR-0003), so once created it depends on no mod;
the list answers "where did this world come from" for bug reports, and lets a
launcher warn that the enabled set has changed. It must not refuse to open a
career. Blocking would contradict ADR-0003, whose entire point is that a career
is self-contained.

`ContentVersion` becomes the derived counterpart: a stable hash over the ordered
`(id, version)` pairs, so provenance is one comparable value while `ModList`
carries the readable detail. It must use a stable digest over a canonical
string — never `string.GetHashCode`, which .NET randomizes per process and which
would make the same mod set produce a different `ContentVersion` on every run.

## Consequences

### Positive

- The schema of any template is determined by the build that reads it, which is
  what makes the loud version refusal in `SchemaVersions` honest.
- Reordering mods is a launcher action, not a file edit across mods.
- A career created from a modded template keeps working when the player
  disables those mods, because it never depended on them.
- Provenance is recorded early, so a bug report from a modded world is
  reproducible instead of guesswork.

### Negative

- Content that genuinely needs a new column cannot ship as a mod. It requires a
  migration, a `SchemaVersions.Expected` bump and a build — deliberately, since
  the alternative is a migration system nested inside the migration system.
- Mods break on schema changes rather than adapting, and must be republished
  against the new `schema_version`.
- `ModList` and `ContentVersion` are provenance the player can make wrong by
  editing a save; they are diagnostics, not integrity guarantees.

## Scope

This decides the *format*. Whether the mod pipeline ships in 1.0 is governed by
ADR-0005, where `docs/context/02-revisao-arquitetura.md` §6 marks modding 🟡.
This ADR does not widen the 1.0 scope.

## Follow-up

`MOD-00` implements this: manifest parsing, ordered application over the base
template in `SqliteWorldTemplateBuilder`, DDL rejection, `schema_version`
matching, and the `ModList`/`ContentVersion` provenance path.

DDL rejection and the ordering rule are contracts, so each needs a test that
fails when the contract is broken — verified by deliberate violation, the way
the Foundation tripwires were. Until `MOD-00` lands, this ADR is a decision, not
a shipped capability, and nothing in the roadmap may be marked 🟢 for it.

**Shipped.** `MOD-00` implements this ADR in full; see `docs/ARCHITECTURE.md` §8 for how
each clause is enforced. One thing the decision did not anticipate: DDL is not the only way
to break it. A mod can corrupt `schema_migrations` or forge a `template_mod` row with plain
`INSERT` statements, so the builder fingerprints those two tables alongside the schema
rather than guarding DDL alone. That strengthens the decision rather than changing it, so
it is recorded here instead of in a superseding ADR.
