CREATE TABLE schema_migrations (
    version INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE country (
    id INTEGER PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL
);

CREATE TABLE city (
    id INTEGER PRIMARY KEY,
    country_id INTEGER NOT NULL REFERENCES country(id),
    name TEXT NOT NULL
);

CREATE TABLE stadium (
    id INTEGER PRIMARY KEY,
    city_id INTEGER NOT NULL REFERENCES city(id),
    name TEXT NOT NULL,
    capacity INTEGER NOT NULL CHECK (capacity >= 0)
);

CREATE TABLE club (
    id INTEGER PRIMARY KEY,
    city_id INTEGER NOT NULL REFERENCES city(id),
    stadium_id INTEGER NOT NULL REFERENCES stadium(id),
    name TEXT NOT NULL,
    short_name TEXT NOT NULL
);

CREATE TABLE player (
    id INTEGER PRIMARY KEY,
    nationality_country_id INTEGER NOT NULL REFERENCES country(id),
    club_id INTEGER NOT NULL REFERENCES club(id),
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    birth_date TEXT NOT NULL,
    squad_number INTEGER NOT NULL CHECK (squad_number BETWEEN 1 AND 99),
    position_code TEXT NOT NULL
);

CREATE TABLE competition (
    id INTEGER PRIMARY KEY,
    country_id INTEGER NOT NULL REFERENCES country(id),
    name TEXT NOT NULL,
    competition_type TEXT NOT NULL
);

CREATE TABLE save_metadata (
    save_id TEXT PRIMARY KEY,
    game_version TEXT NOT NULL,
    schema_version INTEGER NOT NULL,
    content_version TEXT NOT NULL,
    created_at TEXT NOT NULL,
    last_played_at TEXT NOT NULL,
    career_seed TEXT NOT NULL
);

CREATE INDEX idx_player_club ON player(club_id, squad_number, id);
