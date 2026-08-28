-- PLYR-00: the minimum a player needs for a match to be simulable.
--
-- Rebuilds `player` rather than using ALTER TABLE ADD COLUMN, because the point of this
-- migration is the constraints: the attribute scale and the closed position set have to be
-- enforced by the schema, not only by the loader. SQLite cannot add a CHECK to an existing
-- table, so the table is recreated and its rows carried across.
--
-- Attribute scale is 1..20 inclusive and is mirrored by PlayerAttributes.Minimum/Maximum.
-- Changing it here means changing it there.

PRAGMA foreign_keys = OFF;

CREATE TABLE player_new (
    id INTEGER PRIMARY KEY,
    nationality_country_id INTEGER NOT NULL REFERENCES country(id),
    club_id INTEGER NOT NULL REFERENCES club(id),
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    birth_date TEXT NOT NULL,
    squad_number INTEGER NOT NULL CHECK (squad_number BETWEEN 1 AND 99),
    position_code TEXT NOT NULL
        CHECK (position_code IN ('GK','RB','CB','LB','DM','CM','AM','RW','LW','ST')),

    pace INTEGER NOT NULL CHECK (pace BETWEEN 1 AND 20),
    stamina INTEGER NOT NULL CHECK (stamina BETWEEN 1 AND 20),
    strength INTEGER NOT NULL CHECK (strength BETWEEN 1 AND 20),
    passing INTEGER NOT NULL CHECK (passing BETWEEN 1 AND 20),
    shooting INTEGER NOT NULL CHECK (shooting BETWEEN 1 AND 20),
    tackling INTEGER NOT NULL CHECK (tackling BETWEEN 1 AND 20),
    dribbling INTEGER NOT NULL CHECK (dribbling BETWEEN 1 AND 20),
    positioning INTEGER NOT NULL CHECK (positioning BETWEEN 1 AND 20),
    goalkeeping INTEGER NOT NULL CHECK (goalkeeping BETWEEN 1 AND 20)
);

-- Carries over any rows an earlier template already had. Migrations run against an empty
-- database when the template is built, so in practice this copies nothing; it exists so the
-- migration is correct rather than merely sufficient.
INSERT INTO player_new (
    id, nationality_country_id, club_id, first_name, last_name, birth_date, squad_number,
    position_code, pace, stamina, strength, passing, shooting, tackling, dribbling,
    positioning, goalkeeping)
SELECT
    id, nationality_country_id, club_id, first_name, last_name, birth_date, squad_number,
    position_code, 10, 10, 10, 10, 10, 10, 10, 10, 10
FROM player;

DROP TABLE player;
ALTER TABLE player_new RENAME TO player;

CREATE INDEX idx_player_club ON player(club_id, squad_number, id);

PRAGMA foreign_keys = ON;
