#!/usr/bin/env bash
set -euo pipefail

GODOT="${1:?Usage: godot-match-smoke-test.sh /path/to/godot}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS="$REPO_ROOT/artifacts/match-smoke"
TEMPLATE_DB="$ARTIFACTS/world_template.db"
SAVES_ROOT="$REPO_ROOT/saves"
SAVE_DB=""
PROJECT="$REPO_ROOT/game/SoccerDreamGame"

rm -rf "$ARTIFACTS" "$SAVES_ROOT"
mkdir -p "$ARTIFACTS"

dotnet run --project "$REPO_ROOT/tools/SoccerSim.WorldBuilder" --configuration Release -- --output "$TEMPLATE_DB"
dotnet run --project "$REPO_ROOT/tools/SoccerSim.HeadlessRunner" --configuration Release -- --template "$TEMPLATE_DB" --saves "$SAVES_ROOT" --seed 123456789 >/dev/null

SAVE_DB="$(find "$SAVES_ROOT" -name world.db | head -1)"
if [ ! -f "$SAVE_DB" ]; then
    echo "No career world.db was produced." >&2
    exit 3
fi

dotnet build "$PROJECT/SoccerDreamGame.csproj" --configuration Debug

cd "$PROJECT"
"$GODOT" --headless --import >/dev/null

OUTPUT=$(SOCCER_SAVE_DB="$SAVE_DB" SOCCER_MATCH_SEED=123456789 SOCCER_MATCH_SMOKE_EXIT=1   "$GODOT" --headless res://MatchPlayback.tscn 2>&1)
echo "$OUTPUT"

if ! grep -Eq "\[SOCCER-MATCH-SMOKE\] frames=[1-9][0-9]* players=22 score=[0-9]+-[0-9]+ seed=123456789" <<<"$OUTPUT"; then
    echo "MATCH-01 smoke marker not found." >&2
    exit 1
fi
