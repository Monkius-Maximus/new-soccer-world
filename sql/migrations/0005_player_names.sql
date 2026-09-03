-- NMG-003: persisted person-name components and presentation forms.
--
-- The legacy player.first_name/player.last_name columns remain during this compatibility step.
-- The repository verifies they equal given_name/family_name, so divergence fails loudly instead
-- of creating two silent sources of truth. A later breaking migration may remove the legacy pair.

CREATE TABLE player_name (
    player_id INTEGER PRIMARY KEY REFERENCES player(id) ON DELETE CASCADE,
    given_name TEXT NOT NULL
        CHECK (length(given_name) BETWEEN 1 AND 64 AND given_name = trim(given_name)),
    additional_given_name TEXT
        CHECK (additional_given_name IS NULL OR
               (length(additional_given_name) BETWEEN 1 AND 64 AND additional_given_name = trim(additional_given_name))),
    family_name TEXT NOT NULL
        CHECK (length(family_name) BETWEEN 1 AND 64 AND family_name = trim(family_name)),
    additional_family_name TEXT
        CHECK (additional_family_name IS NULL OR
               (length(additional_family_name) BETWEEN 1 AND 64 AND additional_family_name = trim(additional_family_name))),
    full_name TEXT NOT NULL
        CHECK (length(full_name) BETWEEN 1 AND 80 AND full_name = trim(full_name)),
    common_name TEXT NOT NULL
        CHECK (length(common_name) BETWEEN 1 AND 24 AND common_name = trim(common_name)),
    shirt_name TEXT NOT NULL
        CHECK (length(shirt_name) BETWEEN 1 AND 16 AND shirt_name = trim(shirt_name)),
    scoreboard_name TEXT NOT NULL
        CHECK (length(scoreboard_name) BETWEEN 1 AND 18 AND scoreboard_name = trim(scoreboard_name)),
    culture_id TEXT NOT NULL CHECK (length(culture_id) BETWEEN 4 AND 32),
    pack_version TEXT NOT NULL CHECK (length(pack_version) BETWEEN 5 AND 32),
    algorithm_version TEXT NOT NULL CHECK (length(algorithm_version) BETWEEN 1 AND 64)
);
