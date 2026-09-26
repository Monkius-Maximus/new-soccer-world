#!/usr/bin/env bash
#
# Godot presentation smoke test.
#
# Proves that the Godot 4.7.1 .NET presentation layer, targeting net10.0, can open a real
# career database through the Application layer and watch a whole match. Runs headless, so CI
# gates it instead of relying on a developer opening the editor by hand.
#
# The assertion that matters is the digest: the headless runner and the rendered scene are
# compared on the same seed, so this fails if the scene is driving anything other than the
# simulation the rest of the project runs.
#
# Usage: scripts/godot-smoke-test.sh [path-to-godot-binary]
#   Falls back to $GODOT_BIN, then to `godot` on PATH.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT="${1:-${GODOT_BIN:-godot}}"

if ! command -v "$GODOT" >/dev/null 2>&1 && [ ! -x "$GODOT" ]; then
    echo "Godot binary not found: $GODOT" >&2
    echo "Pass it as an argument or set GODOT_BIN." >&2
    exit 2
fi

echo "==> Godot: $("$GODOT" --headless --version)"

cd "$REPO_ROOT"

echo "==> Building world template"
rm -rf artifacts saves
dotnet run --project tools/SoccerSim.WorldBuilder --configuration Release \
    -- --output artifacts/world_template.db

echo "==> Creating a career save and recording the headless digest for seed 1"
RUNNER_OUTPUT=$(dotnet run --project tools/SoccerSim.HeadlessRunner --configuration Release \
    -- --template artifacts/world_template.db --seed 1)

EXPECTED_DIGEST=$(sed -n 's/^Seed 1:.*digest=\([0-9A-F]\{16\}\).*$/\1/p' <<<"$RUNNER_OUTPUT" | head -1)
if [ -z "$EXPECTED_DIGEST" ]; then
    echo "Could not read the headless digest for seed 1." >&2
    echo "$RUNNER_OUTPUT" >&2
    exit 4
fi
echo "==> Headless digest for seed 1: $EXPECTED_DIGEST"

SAVE_DB="$REPO_ROOT/$(find saves -name world.db | head -1)"
if [ ! -f "$SAVE_DB" ]; then
    echo "No career world.db was produced." >&2
    exit 3
fi
echo "==> Career database: $SAVE_DB"

# Godot launches the game assembly from its Debug output directory.
echo "==> Building the Godot assembly"
dotnet build game/SoccerDreamGame/SoccerDreamGame.csproj --configuration Debug

cd "$REPO_ROOT/game/SoccerDreamGame"

echo "==> Importing the Godot project"
"$GODOT" --headless --import >/dev/null

# The scene watches a real match, so it is paced far above real time here - same 50 ms
# timestep, just more of them per second. `timeout` is a guard against a scene that never
# finishes; Godot's own exit code is not trusted, the markers are.
echo "==> Running Main.tscn (watching a full match)"
set +e
OUTPUT=$(SOCCER_SAVE_DB="$SAVE_DB" SOCCER_SMOKE_EXIT=1 SOCCER_MATCH_SEED=1 SOCCER_MATCH_SPEED=4000 \
    timeout 300 "$GODOT" --headless res://Main.tscn 2>&1)
STATUS=$?
set -e
echo "$OUTPUT"

if [ "$STATUS" -eq 124 ]; then
    echo "FAIL: the scene did not finish its match within 300s." >&2
    exit 1
fi

if ! grep -q "\[SOCCER-SMOKE\] clubs=2 players=30" <<<"$OUTPUT"; then
    echo "FAIL: scene did not reach the Application layer for the two seeded clubs." >&2
    exit 1
fi

if ! grep -q "\[SOCCER-SMOKE\] match .*digest=${EXPECTED_DIGEST}" <<<"$OUTPUT"; then
    echo "FAIL: the rendered match is not the headless match." >&2
    echo "      expected digest=${EXPECTED_DIGEST} for seed 1" >&2
    exit 1
fi

echo "==> Godot smoke test passed: the rendered match reproduced the headless digest."
