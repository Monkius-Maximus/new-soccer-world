#!/usr/bin/env bash
#
# Foundation Godot export test.
#
# Goes one step further than scripts/godot-smoke-test.sh: instead of running the project from
# the editor, it exports release builds for both 1.0 desktop targets and then runs the exported
# Linux binary. That is what actually ships, so it is what gets verified — the editor working
# is not evidence that a packaged build does.
#
# Requires the matching export templates to be installed (see docs/ARCHITECTURE.md).
#
# Usage: scripts/godot-export-test.sh [path-to-godot-binary]
#   Falls back to $GODOT_BIN, then to `godot` on PATH.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT="${1:-${GODOT_BIN:-godot}}"

if ! command -v "$GODOT" >/dev/null 2>&1 && [ ! -x "$GODOT" ]; then
    echo "Godot binary not found: $GODOT" >&2
    exit 2
fi

echo "==> Godot: $("$GODOT" --headless --version)"

cd "$REPO_ROOT"

echo "==> Building world template and a career save"
rm -rf artifacts saves
dotnet run --project tools/SoccerSim.WorldBuilder --configuration Release \
    -- --output artifacts/world_template.db
RUNNER_OUTPUT=$(dotnet run --project tools/SoccerSim.HeadlessRunner --configuration Release \
    -- --template artifacts/world_template.db --seed 1)

SAVE_DB="$REPO_ROOT/$(find saves -name world.db | head -1)"
[ -f "$SAVE_DB" ] || { echo "No career world.db was produced." >&2; exit 3; }

# The digest the packaged build has to reproduce. Launching is not evidence that the artifact
# runs the same football; matching this is.
EXPECTED_DIGEST=$(sed -n 's/^Seed 1:.*digest=\([0-9A-F]\{16\}\).*$/\1/p' <<<"$RUNNER_OUTPUT" | head -1)
if [ -z "$EXPECTED_DIGEST" ]; then
    echo "Could not read the headless digest for seed 1." >&2
    echo "$RUNNER_OUTPUT" >&2
    exit 4
fi
echo "==> Headless digest for seed 1: $EXPECTED_DIGEST"

# Godot refuses to export into a directory that does not already exist.
mkdir -p artifacts/export/linux artifacts/export/windows

cd "$REPO_ROOT/game/SoccerDreamGame"
"$GODOT" --headless --import >/dev/null

for preset in "Linux x86_64" "Windows x86_64"; do
    echo "==> Exporting release: $preset"
    # Godot exits 0 even when the .NET half of the export fails, so the log is the real signal.
    LOG=$("$GODOT" --headless --export-release "$preset" 2>&1) || true
    if grep -qiE "^ERROR|no solution file" <<<"$LOG"; then
        echo "$LOG" >&2
        echo "FAIL: export of '$preset' reported errors." >&2
        exit 1
    fi
done

cd "$REPO_ROOT"

LINUX_BIN="artifacts/export/linux/SoccerDreamGame.x86_64"
WINDOWS_BIN="artifacts/export/windows/SoccerDreamGame.exe"

for artifact in "$LINUX_BIN" "$WINDOWS_BIN"; do
    [ -s "$artifact" ] || { echo "FAIL: missing export artifact $artifact" >&2; exit 1; }
done

# The .NET assemblies ship beside the binary. Their absence is how a broken export presents:
# the executable is produced, looks fine, and segfaults on launch.
for data_dir in artifacts/export/linux/data_* artifacts/export/windows/data_*; do
    [ -d "$data_dir" ] || { echo "FAIL: no .NET data directory next to the export." >&2; exit 1; }
    [ -f "$data_dir/SoccerDreamGame.dll" ] || {
        echo "FAIL: $data_dir does not contain the game assembly." >&2; exit 1; }
done

# The scene watches a whole match, so it has to be paced far above real time or this step
# would wait ninety minutes for one. Same 50 ms timestep, just more steps per second.
# `timeout` is the guard against an exported build that starts and then never finishes.
echo "==> Running the exported Linux release build (watching a full match)"
set +e
OUTPUT=$(cd artifacts/export/linux && SOCCER_SAVE_DB="$SAVE_DB" SOCCER_SMOKE_EXIT=1 \
    SOCCER_MATCH_SEED=1 SOCCER_MATCH_SPEED=4000 \
    timeout 300 ./SoccerDreamGame.x86_64 --headless 2>&1)
STATUS=$?
set -e
echo "$OUTPUT"

if [ "$STATUS" -eq 124 ]; then
    echo "FAIL: the exported build did not finish its match within 300s." >&2
    exit 1
fi

if ! grep -q "\[SOCCER-SMOKE\] clubs=2 players=30" <<<"$OUTPUT"; then
    echo "FAIL: the exported build did not reach the Application layer." >&2
    exit 1
fi

# The packaged artifact has to run the same football as everything else, not merely launch.
if ! grep -q "\[SOCCER-SMOKE\] match .*digest=${EXPECTED_DIGEST}" <<<"$OUTPUT"; then
    echo "FAIL: the exported build did not reproduce the headless digest for seed 1." >&2
    echo "      expected digest=${EXPECTED_DIGEST}" >&2
    exit 1
fi

# The Windows build cannot run here; verify it is at least a real PE binary for the right arch.
if command -v file >/dev/null 2>&1; then
    echo "==> Windows artifact: $(file -b "$WINDOWS_BIN")"
    file -b "$WINDOWS_BIN" | grep -q "PE32+ executable" || {
        echo "FAIL: the Windows export is not a PE32+ x86-64 executable." >&2; exit 1; }
fi

echo "==> Godot export test passed."
