-- TACT-00: each club chooses how it sets up.
--
-- Before this, every club in the world played an identical hard-coded 4-4-2, because the shape
-- was a constant in code. Tactics are reference content like the rest of the club data: the
-- template ships a setup per club, and CLUB-00 will let a manager change it.
--
-- Instructions share the 1..20 scale used by player attributes, and carry the same CHECK
-- constraints so neither a bad loader nor hand-edited SQL can produce an unusable setup.
-- Formation names are constrained to the shapes the simulation actually knows.

CREATE TABLE club_tactics (
    club_id INTEGER PRIMARY KEY REFERENCES club(id),
    formation TEXT NOT NULL CHECK (formation IN ('4-4-2', '4-3-3', '5-3-2')),
    defensive_line_height INTEGER NOT NULL CHECK (defensive_line_height BETWEEN 1 AND 20),
    pressing_intensity INTEGER NOT NULL CHECK (pressing_intensity BETWEEN 1 AND 20),
    directness INTEGER NOT NULL CHECK (directness BETWEEN 1 AND 20)
);
