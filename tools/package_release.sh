#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${1:-0.1.0}"
BASE="RampWreckers-v${VERSION}"
DIST="dist"
LINUX_DIR="$DIST/${BASE}-linux-x64"
WIN_DIR="$DIST/${BASE}-windows-x64"

mkdir -p "$LINUX_DIR" "$WIN_DIR"

if [ ! -x "build/linux/RampWreckers.x86_64" ]; then
    echo "Missing build/linux/RampWreckers.x86_64; run ./tools/export_game.sh first." >&2
    exit 1
fi
if [ ! -f "build/windows/RampWreckers.exe" ]; then
    echo "Missing build/windows/RampWreckers.exe; run ./tools/export_game.sh first." >&2
    exit 1
fi

cp build/linux/RampWreckers.x86_64 "$LINUX_DIR/"
cp build/windows/RampWreckers.exe "$WIN_DIR/"

cat > "$LINUX_DIR/README.txt" <<'EOF'
Ramp Wreckers v0.1.0
====================

Run:
    chmod +x RampWreckers.x86_64
    ./RampWreckers.x86_64

This is a self-contained Linux x86_64 build with the Godot runtime, .NET runtime,
game assembly and PCK embedded in the executable.

Controls:
    W/S, Up/Down      accelerate / reverse
    A/D, Left/Right  steer
    Space             brake
    R                 instant retry
    1/2/3             rally / pickup / monster truck
    J/K               previous / next challenge
    Q/E               lower / raise ramp
    F                 Free Smash
    C                 cycle camera / skip impact close-up
    right mouse drag  orbit camera, wheel zooms
    G or Tab          garage
    P                 paint
    B/V               quality / volume
    H                 control hint

Requirements: Linux x86_64 with a working OpenGL 3.3+ driver. Fully offline.
Progress is stored in Godot's user directory, not beside this executable.
EOF

cat > "$WIN_DIR/README.txt" <<'EOF'
Ramp Wreckers v0.1.0
====================

Run:
    RampWreckers.exe

This is a self-contained Windows x86_64 build with the Godot runtime, .NET
runtime, game assembly and PCK embedded in the executable.

Controls:
    W/S, Up/Down      accelerate / reverse
    A/D, Left/Right  steer
    Space             brake
    R                 instant retry
    1/2/3             rally / pickup / monster truck
    J/K               previous / next challenge
    Q/E               lower / raise ramp
    F                 Free Smash
    C                 cycle camera / skip impact close-up
    right mouse drag  orbit camera, wheel zooms
    G or Tab          garage
    P                 paint
    B/V               quality / volume
    H                 control hint

Requirements: Windows 10/11 x86_64 with a working OpenGL 3.3+ driver. Fully offline.
Progress is stored in Godot's user directory, not beside this executable.
EOF

tar -czf "$DIST/${BASE}-linux-x64.tar.gz" -C "$DIST" "${BASE}-linux-x64"
(
    cd "$DIST"
    zip -q -r "${BASE}-windows-x64.zip" "${BASE}-windows-x64"
)

(
    cd "$DIST"
    sha256sum \
        "${BASE}-linux-x64.tar.gz" \
        "${BASE}-windows-x64.zip" \
        "${BASE}-linux-x64/RampWreckers.x86_64" \
        "${BASE}-windows-x64/RampWreckers.exe" > SHA256SUMS.txt
)

echo "Packaged:"
ls -lh "$DIST/${BASE}-linux-x64.tar.gz" "$DIST/${BASE}-windows-x64.zip" "$DIST/SHA256SUMS.txt"

# Prove the published Linux archive extracts and boots, not just the build tree.
SMOKE_DIR="$(mktemp -d)"
trap 'rm -rf "$SMOKE_DIR"' EXIT
tar -xzf "$DIST/${BASE}-linux-x64.tar.gz" -C "$SMOKE_DIR"
"$SMOKE_DIR/${BASE}-linux-x64/RampWreckers.x86_64" --headless --quit-after 180
echo "Packaged Linux archive booted successfully."
