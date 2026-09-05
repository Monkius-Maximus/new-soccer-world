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

# MATCH-01: the visual view steps the simulation itself rather than replaying a recording, so
# the digest it produces must equal the one the console runner produced for the same seed. If
# these ever diverge, the screen is showing football that never happened.
MATCH_SEED=123456790

cd "$REPO_ROOT"
echo "==> Console match, seed $MATCH_SEED"
CONSOLE=$(dotnet run --project tools/SoccerSim.HeadlessRunner --configuration Release \
    -- --template artifacts/world_template.db --seed $MATCH_SEED)
CONSOLE_DIGEST=$(grep -oP "^Seed $MATCH_SEED:.*digest=\K[0-9A-F]+" <<<"$CONSOLE" | head -1)

if [ -z "$CONSOLE_DIGEST" ]; then
    echo "$CONSOLE" >&2
    echo "FAIL: could not read a digest from the console runner." >&2
    exit 1
fi
echo "    console digest = $CONSOLE_DIGEST"

cd "$REPO_ROOT/game/SoccerDreamGame"
echo "==> Visual match, same seed"
MATCH_OUTPUT=$(SOCCER_SAVE_DB="$SAVE_DB" SOCCER_SMOKE_EXIT=1 SOCCER_MATCH_SPEED=8000 \
    SOCCER_MATCH_SEED=$MATCH_SEED "$GODOT" --headless res://Match.tscn 2>&1)
echo "$MATCH_OUTPUT"

VIEW_DIGEST=$(grep -oP "\[SOCCER-MATCH\] fulltime .*digest=\K[0-9A-F]+" <<<"$MATCH_OUTPUT" | head -1)

if [ -z "$VIEW_DIGEST" ]; then
    echo "FAIL: the match view did not reach full time." >&2
    exit 1
fi

if [ "$VIEW_DIGEST" != "$CONSOLE_DIGEST" ]; then
    echo "FAIL: the rendered match diverged from the console match." >&2
    echo "      console=$CONSOLE_DIGEST  view=$VIEW_DIGEST" >&2
    exit 1
fi

# The digest proves the simulation matched. It says nothing about whether the view reported it
# correctly, so compare the goal times too: an earlier version read them off the frame that
# noticed the goal rather than off the event, which moved them by minutes at high speed.
CONSOLE_GOALS=$(grep -oP "^\s+\K\d+(?=')" <<<"$CONSOLE" | sort -n | tr '\n' ' ')
VIEW_GOALS=$(grep -oP "\[SOCCER-MATCH\] goal \K\d+(?=')" <<<"$MATCH_OUTPUT" | sort -n | tr '\n' ' ')

if [ "$CONSOLE_GOALS" != "$VIEW_GOALS" ]; then
    echo "FAIL: goal times differ between the console and the view." >&2
    echo "      console=[$CONSOLE_GOALS] view=[$VIEW_GOALS]" >&2
    exit 1
fi

echo "==> Goal times agree: [${VIEW_GOALS:-none}]"
echo "==> Rendered and console matches agree: $VIEW_DIGEST"

# COMP-00: the same argument one level up. The console runner played the league season out of
# the save it created; Season.tscn plays the same season out of the same save through the same
# use case. Both print one record line per standing, and they have to be identical — a table is
# just as capable of being wrong on screen as a scoreline is.
CONSOLE_TABLE=$(grep -oP "\[SOCCER-TABLE\] \K.*" <<<"$CONSOLE" | sort)
CONSOLE_SEASON=$(grep -oP "\[SOCCER-SEASON\] \K.*" <<<"$CONSOLE" | head -1)

if [ -z "$CONSOLE_TABLE" ]; then
    echo "FAIL: the console runner produced no league table." >&2
    exit 1
fi

cd "$REPO_ROOT/game/SoccerDreamGame"
echo "==> Season table, same career"
SEASON_OUTPUT=$(SOCCER_SAVE_DB="$SAVE_DB" SOCCER_SMOKE_EXIT=1 "$GODOT" --headless res://Season.tscn 2>&1)
echo "$SEASON_OUTPUT"

VIEW_TABLE=$(grep -oP "\[SOCCER-TABLE\] \K.*" <<<"$SEASON_OUTPUT" | sort)
VIEW_SEASON=$(grep -oP "\[SOCCER-SEASON\] \K.*" <<<"$SEASON_OUTPUT" | head -1)

if [ "$VIEW_TABLE" != "$CONSOLE_TABLE" ]; then
    echo "FAIL: the rendered league table diverged from the console table." >&2
    diff <(echo "$CONSOLE_TABLE") <(echo "$VIEW_TABLE") >&2 || true
    exit 1
fi

if [ "$VIEW_SEASON" != "$CONSOLE_SEASON" ]; then
    echo "FAIL: the rendered season summary diverged from the console one." >&2
    echo "      console=$CONSOLE_SEASON  view=$VIEW_SEASON" >&2
    exit 1
fi

echo "==> Rendered and console league tables agree ($CONSOLE_SEASON)"
echo "==> Godot smoke test passed."
