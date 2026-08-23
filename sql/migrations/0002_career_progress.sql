-- Career progress written at explicit checkpoints.
--
-- Reference data (country/city/club/stadium/player/competition) is static content shipped in
-- world_template.db. This table is the first thing a career accumulates on top of it, and it
-- exists so the checkpoint boundary has a real dirty aggregate to flush rather than metadata
-- alone. It records applied simulation results, not football events; MATCH will add those.

CREATE TABLE simulation_run (
    ordinal INTEGER PRIMARY KEY,
    home_club_id INTEGER NOT NULL REFERENCES club(id),
    away_club_id INTEGER NOT NULL REFERENCES club(id),
    seed TEXT NOT NULL,
    simulation_version INTEGER NOT NULL,
    ticks INTEGER NOT NULL CHECK (ticks > 0),
    digest TEXT NOT NULL,
    CHECK (home_club_id <> away_club_id)
);
