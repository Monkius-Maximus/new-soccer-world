-- MOD-00: a template records which mods produced it.
--
-- ADR-0007 makes this provenance, not a dependency. A career is a copy of a template
-- (ADR-0003), so once it exists it needs no mod to keep working; this table answers "where
-- did this world come from" when a bug is reported, and lets a launcher warn that the
-- enabled set has changed. It must never block opening a career.
--
-- The table lives in the template rather than in save_metadata because the builder writes
-- it before any career exists. The career inherits it through the file copy.

CREATE TABLE template_mod (
    ordinal INTEGER PRIMARY KEY,
    mod_id TEXT NOT NULL UNIQUE,
    mod_version TEXT NOT NULL
);
