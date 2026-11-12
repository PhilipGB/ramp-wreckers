#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

GODOT_BIN="${GODOT_BIN:-godot}"
LOG_DIR="artifacts/test-logs"
mkdir -p "$LOG_DIR"

echo "== Static project contract =="
python3 tools/validate_project.py | tee "$LOG_DIR/static-validation.log"
python3 tools/verify_bundle.py | tee "$LOG_DIR/bundle-integrity.log"
python3 tools/add_normals_to_glb.py --check | tee "$LOG_DIR/glb-normals.log"
python3 tools/restyle_materials.py --check | tee "$LOG_DIR/material-palette.log"

echo "== .NET restore and Release build =="
dotnet restore | tee "$LOG_DIR/dotnet-restore.log"
dotnet build -c Release -warnaserror | tee "$LOG_DIR/dotnet-build-release.log"

echo "== Core unit tests (no engine) =="
dotnet run --project tests/UnitTests/UnitTests.csproj | tee "$LOG_DIR/unit-tests.log"

echo "== Godot headless editor import =="
"$GODOT_BIN" --headless --editor --quit --path . 2>&1 | tee "$LOG_DIR/godot-import.log"

echo "== Headless 60-second gameplay boot =="
"$GODOT_BIN" --headless --path . res://tests/BootProbe.tscn 2>&1 | tee "$LOG_DIR/godot-boot-60s.log"

echo "== Deterministic runtime acceptance tests =="
"$GODOT_BIN" --headless --path . res://tests/RuntimeTests.tscn 2>&1 | tee "$LOG_DIR/runtime-tests.log"
python3 tools/check_test_log.py "$LOG_DIR/runtime-tests.log" | tee "$LOG_DIR/runtime-log-check.log"

echo "ALL AUTOMATED CHECKS PASSED"
echo "Logs: $LOG_DIR"
