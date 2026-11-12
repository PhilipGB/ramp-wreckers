#!/usr/bin/env python3
"""Add authored smooth-angle normals to Ramp Wreckers GLB meshes.

The original low-poly GLBs contain POSITION only. Godot's GL Compatibility
shadow path mis-biases those surfaces, producing severe self-shadow moire.
The editable OBJ sources contain normals, but the GLB conversion dropped the
NORMAL attribute. This offline tool restores normals without changing any
visible vertex position, triangle, node transform or material assignment.

Usage:
    python3 tools/add_normals_to_glb.py            # all art/meshes/**/*.glb
    python3 tools/add_normals_to_glb.py --check    # verify every GLB has normals

Normals are averaged across faces that meet below --angle degrees, so curved
low-poly wheels/heads stay smooth while hard box edges stay hard.
"""
from __future__ import annotations

import argparse
import json
import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942


def parse_glb(path: Path):
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from("<III", raw, 0)
    if magic != GLB_MAGIC or version != 2 or total != len(raw):
        raise ValueError(f"not a valid GLB 2.0 file: {path}")
    offset = 12
    js = None
    binary = None
    while offset < len(raw):
        length, kind = struct.unpack_from("<II", raw, offset)
        data = raw[offset + 8:offset + 8 + length]
        offset += 8 + length
        if kind == JSON_CHUNK:
            js = json.loads(data.decode("utf-8").rstrip("\x00 "))
        elif kind == BIN_CHUNK:
            binary = bytearray(data)
    if js is None or binary is None:
        raise ValueError(f"GLB is missing JSON or BIN chunk: {path}")
    return js, binary


def write_glb(path: Path, js: dict, binary: bytearray) -> None:
    while len(binary) % 4:
        binary.append(0)
    js["buffers"][0]["byteLength"] = len(binary)
    encoded = json.dumps(js, separators=(",", ":")).encode("utf-8")
    while len(encoded) % 4:
        encoded += b" "
    total = 12 + 8 + len(encoded) + 8 + len(binary)
    out = bytearray(struct.pack("<III", GLB_MAGIC, 2, total))
    out += struct.pack("<II", len(encoded), JSON_CHUNK) + encoded
    out += struct.pack("<II", len(binary), BIN_CHUNK) + binary
    path.write_bytes(out)


def read_accessor(js: dict, binary: bytes, index: int):
    accessor = js["accessors"][index]
    view = js["bufferViews"][accessor["bufferView"]]
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    components = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[accessor["type"]]
    fmt = {5126: "f", 5125: "I", 5123: "H", 5121: "B"}[accessor["componentType"]]
    item_size = struct.calcsize("<" + fmt) * components
    stride = view.get("byteStride", item_size)
    out = []
    for i in range(accessor["count"]):
        values = struct.unpack_from("<" + fmt * components, binary, start + i * stride)
        out.append(values if components > 1 else values[0])
    return out


def add_accessor(js: dict, binary: bytearray, payload: bytes, count: int,
                 min_values=None, max_values=None) -> int:
    while len(binary) % 4:
        binary.append(0)
    offset = len(binary)
    binary += payload
    view_index = len(js["bufferViews"])
    js["bufferViews"].append({
        "buffer": 0,
        "byteOffset": offset,
        "byteLength": len(payload),
        "target": 34962,
    })
    accessor = {
        "bufferView": view_index,
        "componentType": 5126,
        "count": count,
        "type": "VEC3",
    }
    if min_values is not None:
        accessor["min"] = min_values
        accessor["max"] = max_values
    js["accessors"].append(accessor)
    return len(js["accessors"]) - 1


def angle_normals(positions, indices, angle_degrees: float):
    """Return one normal per triangle corner, smooth below the angle threshold."""
    face_normals = []
    triangles = []
    for t in range(0, len(indices), 3):
        ia, ib, ic = indices[t], indices[t + 1], indices[t + 2]
        a, b, c = positions[ia], positions[ib], positions[ic]
        u = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        v = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
        n = (u[1] * v[2] - u[2] * v[1],
             u[2] * v[0] - u[0] * v[2],
             u[0] * v[1] - u[1] * v[0])
        length = math.sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2])
        if length < 1e-12:
            n = (0.0, 1.0, 0.0)
        else:
            n = (n[0] / length, n[1] / length, n[2] / length)
        face_normals.append(n)
        triangles.append((ia, ib, ic))

    # Merge faces that share a position with a similar face normal. Position keys
    # are quantized only for lookup; written positions stay bit-exact.
    incident: dict[tuple[int, int, int], list[int]] = {}
    for face, tri in enumerate(triangles):
        for vi in tri:
            p = positions[vi]
            key = (round(p[0] * 100000), round(p[1] * 100000), round(p[2] * 100000))
            incident.setdefault(key, []).append(face)

    threshold = math.cos(math.radians(angle_degrees))
    corner_normals = []
    for face, tri in enumerate(triangles):
        fn = face_normals[face]
        for vi in tri:
            p = positions[vi]
            key = (round(p[0] * 100000), round(p[1] * 100000), round(p[2] * 100000))
            sx = sy = sz = 0.0
            for other in incident[key]:
                on = face_normals[other]
                if fn[0] * on[0] + fn[1] * on[1] + fn[2] * on[2] >= threshold:
                    sx += on[0]
                    sy += on[1]
                    sz += on[2]
            length = math.sqrt(sx * sx + sy * sy + sz * sz)
            corner_normals.append((sx / length, sy / length, sz / length)
                                  if length > 1e-12 else fn)
    return corner_normals


def add_normals(path: Path, angle: float) -> bool:
    js, binary = parse_glb(path)
    changed = False
    for mesh in js.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            attributes = primitive.setdefault("attributes", {})
            if "NORMAL" in attributes:
                continue
            positions = read_accessor(js, binary, attributes["POSITION"])
            if "indices" in primitive:
                indices = read_accessor(js, binary, primitive["indices"])
            else:
                indices = list(range(len(positions)))
            if len(indices) % 3:
                raise ValueError(f"non-triangle primitive in {path}")
            corner_normals = angle_normals(positions, indices, angle)
            corner_positions = [positions[i] for i in indices]

            pos_payload = b"".join(struct.pack("<fff", *p) for p in corner_positions)
            norm_payload = b"".join(struct.pack("<fff", *n) for n in corner_normals)
            mins = [min(p[i] for p in positions) for i in range(3)]
            maxs = [max(p[i] for p in positions) for i in range(3)]
            attributes["POSITION"] = add_accessor(
                js, binary, pos_payload, len(corner_positions), mins, maxs)
            attributes["NORMAL"] = add_accessor(
                js, binary, norm_payload, len(corner_normals))
            primitive.pop("indices", None)
            primitive["mode"] = 4
            changed = True
    if changed:
        write_glb(path, js, binary)
    return changed


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--angle", type=float, default=45.0)
    args = parser.parse_args()

    files = sorted((ROOT / "art" / "meshes").rglob("*.glb"))
    missing = []
    for path in files:
        js, _ = parse_glb(path)
        has_normals = all("NORMAL" in p.get("attributes", {})
                          for m in js.get("meshes", []) for p in m.get("primitives", []))
        if args.check:
            if not has_normals:
                missing.append(path)
            continue
        changed = add_normals(path, args.angle)
        print(f"{'updated' if changed else 'kept':7} {path.relative_to(ROOT)}")

    if args.check:
        if missing:
            for path in missing:
                print(f"MISSING NORMALS: {path.relative_to(ROOT)}")
            raise SystemExit(1)
        print(f"OK: all {len(files)} GLB files have authored NORMAL attributes")


if __name__ == "__main__":
    main()
