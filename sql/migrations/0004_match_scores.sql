-- MATCH-00: applied results now carry a scoreline.
--
-- simulation_run previously recorded only that a deterministic run happened. With real football
-- behind it, the result a career cares about is the score, so the table is rebuilt with the two
-- columns and their constraints. The digest stays: it is what proves a replay reproduced the
-- same match, and a scoreline alone would not.

PRAGMA foreign_keys = OFF;

CREATE TABLE simulation_run_new (
    ordinal INTEGER PRIMARY KEY,
    home_club_id INTEGER NOT NULL REFERENCES club(id),
    away_club_id INTEGER NOT NULL REFERENCES club(id),
    home_score INTEGER NOT NULL CHECK (home_score >= 0),
    away_score INTEGER NOT NULL CHECK (away_score >= 0),
    seed TEXT NOT NULL,
    simulation_version INTEGER NOT NULL,
    ticks INTEGER NOT NULL CHECK (ticks > 0),
    digest TEXT NOT NULL,
    CHECK (home_club_id <> away_club_id)
);

INSERT INTO simulation_run_new (
    ordinal, home_club_id, away_club_id, home_score, away_score,
    seed, simulation_version, ticks, digest)
SELECT
    ordinal, home_club_id, away_club_id, 0, 0,
    seed, simulation_version, ticks, digest
FROM simulation_run;

DROP TABLE simulation_run;
ALTER TABLE simulation_run_new RENAME TO simulation_run;

PRAGMA foreign_keys = ON;
