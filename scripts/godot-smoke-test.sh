#!/usr/bin/env bash
#
# Foundation Godot smoke test.
#
# Proves that the Godot 4.7.1 .NET presentation layer, targeting net10.0, can open a real
# career database through the Application layer and render both seeded clubs. Runs headless,
# so CI gates it instead of relying on a developer opening the editor by hand.
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

echo "==> Creating a career save"
dotnet run --project tools/SoccerSim.HeadlessRunner --configuration Release \
    -- --template artifacts/world_template.db --seed 1 >/dev/null

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

echo "==> Running Main.tscn"
OUTPUT=$(SOCCER_SAVE_DB="$SAVE_DB" SOCCER_SMOKE_EXIT=1 "$GODOT" --headless res://Main.tscn 2>&1)
echo "$OUTPUT"

if ! grep -q "\[SOCCER-SMOKE\] clubs=2 players=30" <<<"$OUTPUT"; then
    echo "FAIL: scene did not render the two seeded clubs and 30 players." >&2
    exit 1
fi

echo "==> Godot smoke test passed."
