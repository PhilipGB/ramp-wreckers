#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

GODOT_BIN="${GODOT_BIN:-godot}"
LOG_DIR="artifacts/test-logs"
mkdir -p "$LOG_DIR" build/linux build/windows

echo "== Linux export =="
rm -rf build/linux
mkdir -p build/linux
"$GODOT_BIN" --headless --path . --export-release "Linux" build/linux/RampWreckers.x86_64 2>&1 |
    tee "$LOG_DIR/export-linux.log"

echo "== Linux export headless boot =="
./build/linux/RampWreckers.x86_64 --headless --quit-after 180 2>&1 |
    tee "$LOG_DIR/export-linux-boot.log"

echo "== Windows export =="
rm -rf build/windows
mkdir -p build/windows
"$GODOT_BIN" --headless --path . --export-release "Windows" build/windows/RampWreckers.exe 2>&1 |
    tee "$LOG_DIR/export-windows.log"

ls -lh build/linux build/windows
echo "Exports complete. Windows was exported but cannot be executed on this Linux host."
