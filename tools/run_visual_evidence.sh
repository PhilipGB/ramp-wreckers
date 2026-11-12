#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

GODOT_BIN="${GODOT_BIN:-godot}"
LOG_DIR="artifacts/test-logs"
mkdir -p "$LOG_DIR" artifacts/screenshots

run_capture() {
    local name="$1" vehicle="$2" challenge="$3" angle="$4" garage="${5:-0}" free="${6:-0}"
    echo "Capturing $name..."
    RW_VISUAL_TAG="$name" \
    RW_VISUAL_VEHICLE="$vehicle" \
    RW_VISUAL_CHALLENGE="$challenge" \
    RW_VISUAL_ANGLE="$angle" \
    RW_VISUAL_GARAGE="$garage" \
    RW_VISUAL_FREE="$free" \
    "$GODOT_BIN" --path . res://tests/VisualRun.tscn 2>&1 |
        tee "$LOG_DIR/visual-$name.log" | grep -E '\[VISUAL\]|ERROR' || true
}

run_capture pickup_gnome 1 0 16
run_capture rally_knight 0 4 20
run_capture monster_robot 2 7 18
run_capture garage 2 8 16 1

echo "Screenshots written to artifacts/screenshots"
