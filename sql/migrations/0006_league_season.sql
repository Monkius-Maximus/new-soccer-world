-- COMP-00: a career now plays a league season rather than isolated friendlies.
--
-- Three things change.
--
-- 1. simulation_run learns which fixture it settled. Until now every applied result was a
--    friendly, so a scoreline was enough. A league table has to know which competition a
--    result belongs to, and re-simulating one fixture has to know which one. competition_id
--    is NULL for a friendly rather than zero, so the foreign key stays real; the loader maps
--    NULL to zero, which is what AppliedSimulationRun.IsCompetitive reads.
--
-- 2. season records the competition in progress. Fixtures are deliberately NOT stored: they
--    are generated from the entrants and the start date, and a stored list that only ever
--    mirrored the generator would be a second source of truth. The table is constrained to a
--    single row because WorldState models exactly one season in progress; careers running
--    several competitions at once are CAREER-00's problem, and a table shaped for them today
--    would be structure nothing exercises.
--
-- 3. competition_type is constrained to the kinds the code can actually schedule. A typo used
--    to produce a competition that loads fine and can never be played.

PRAGMA foreign_keys = OFF;

CREATE TABLE simulation_run_new (
    ordinal INTEGER PRIMARY KEY,
    competition_id INTEGER REFERENCES competition(id),
    fixture_ordinal INTEGER NOT NULL DEFAULT 0 CHECK (fixture_ordinal >= 0),
    home_club_id INTEGER NOT NULL REFERENCES club(id),
    away_club_id INTEGER NOT NULL REFERENCES club(id),
    home_score INTEGER NOT NULL CHECK (home_score >= 0),
    away_score INTEGER NOT NULL CHECK (away_score >= 0),
    seed TEXT NOT NULL,
    simulation_version INTEGER NOT NULL,
    ticks INTEGER NOT NULL CHECK (ticks > 0),
    digest TEXT NOT NULL,
    CHECK (home_club_id <> away_club_id),
    -- A competitive result settles a numbered fixture; a friendly settles none. Neither half
    -- of that pair is meaningful on its own.
    CHECK ((competition_id IS NULL) = (fixture_ordinal = 0))
);

INSERT INTO simulation_run_new (
    ordinal, competition_id, fixture_ordinal, home_club_id, away_club_id,
    home_score, away_score, seed, simulation_version, ticks, digest)
SELECT
    ordinal, NULL, 0, home_club_id, away_club_id,
    home_score, away_score, seed, simulation_version, ticks, digest
FROM simulation_run;

DROP TABLE simulation_run;
ALTER TABLE simulation_run_new RENAME TO simulation_run;

CREATE TABLE competition_new (
    id INTEGER PRIMARY KEY,
    country_id INTEGER NOT NULL REFERENCES country(id),
    name TEXT NOT NULL,
    competition_type TEXT NOT NULL CHECK (competition_type IN ('friendly', 'league'))
);

INSERT INTO competition_new (id, country_id, name, competition_type)
SELECT id, country_id, name, competition_type FROM competition;

DROP TABLE competition;
ALTER TABLE competition_new RENAME TO competition;

CREATE TABLE season (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    competition_id INTEGER NOT NULL REFERENCES competition(id),
    start_date TEXT NOT NULL,
    -- Matchday numbering starts at 1; zero means the season has not kicked off yet.
    current_matchday INTEGER NOT NULL CHECK (current_matchday >= 0)
);

CREATE TABLE season_club (
    season_id INTEGER NOT NULL REFERENCES season(id),
    club_id INTEGER NOT NULL REFERENCES club(id),
    PRIMARY KEY (season_id, club_id)
);

PRAGMA foreign_keys = ON;
