-- Explicit compatibility metadata for the 30 hand-authored foundation players.
-- New generated players persist the exact NMG result instead of regenerating on load.
INSERT INTO player_name (
    player_id,
    given_name,
    additional_given_name,
    family_name,
    additional_family_name,
    full_name,
    common_name,
    shirt_name,
    scoreboard_name,
    culture_id,
    pack_version,
    algorithm_version)
SELECT
    id,
    first_name,
    NULL,
    last_name,
    NULL,
    first_name || ' ' || last_name,
    first_name || ' ' || last_name,
    upper(last_name),
    upper(last_name),
    'pt-BR',
    '0.0.0',
    'legacy-manual-v0'
FROM player
ORDER BY id;
