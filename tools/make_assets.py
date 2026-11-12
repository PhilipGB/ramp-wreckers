#!/usr/bin/env python3
"""Author the small effect meshes and the looping engine sample used by Ramp Wreckers.

Everything here is an *offline authoring* tool.  The game only ever loads the
resulting GLB/WAV files; it never builds geometry or audio at runtime.

Run from the project root:
    python3 tools/make_assets.py
"""
from __future__ import annotations

import json
import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


# ---------------------------------------------------------------------------
# A tiny GLB writer (positions + normals + triangles, one PBR material).
# ---------------------------------------------------------------------------
def _accessor(buffer: bytearray, values, component_type: int, type_name: str,
              min_values=None, max_values=None) -> dict:
    while len(buffer) % 4:
        buffer.append(0)
    offset = len(buffer)
    for value in values:
        if isinstance(value, (tuple, list)):
            buffer.extend(struct.pack("<" + "f" * len(value), *value))
        else:
            buffer.extend(struct.pack("<I", value))
    count = len(values)
    accessor = {
        "bufferView": 0,
        "componentType": component_type,
        "count": count,
        "type": type_name,
    }
    if min_values is not None:
        accessor["min"] = min_values
    if max_values is not None:
        accessor["max"] = max_values
    return {"_offset": offset, "_length": len(buffer) - offset, **accessor}


def write_glb(path: Path, name: str, vertices, normals, triangles, color,
              alpha: float = 1.0) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    positions = [tuple(float(c) for c in v) for v in vertices]
    norms = [tuple(float(c) for c in n) for n in normals]
    indices = [int(i) for tri in triangles for i in tri]

    bin_buffer = bytearray()
    pos = _accessor(bin_buffer, positions, 5126, "VEC3",
                    [min(v[i] for v in positions) for i in range(3)],
                    [max(v[i] for v in positions) for i in range(3)])
    nor = _accessor(bin_buffer, norms, 5126, "VEC3")
    idx = _accessor(bin_buffer, indices, 5125, "SCALAR")

    views = []
    accessors = []
    for acc in (pos, nor, idx):
        views.append({
            "buffer": 0,
            "byteOffset": acc["_offset"],
            "byteLength": acc["_length"],
        })
        clean = {k: v for k, v in acc.items() if not k.startswith("_")}
        clean["bufferView"] = len(views) - 1
        accessors.append(clean)

    gltf = {
        "asset": {"version": "2.0", "generator": "Ramp Wreckers offline authoring"},
        "scene": 0,
        "scenes": [{"nodes": [0], "name": "world"}],
        "nodes": [{"name": name, "mesh": 0}],
        "meshes": [{
            "name": name,
            "primitives": [{
                "attributes": {"POSITION": 0, "NORMAL": 1},
                "indices": 2,
                "material": 0,
            }],
        }],
        "materials": [{
            "name": name + "_material",
            "pbrMetallicRoughness": {
                "baseColorFactor": [*color, alpha],
                "metallicFactor": 0.0,
                "roughnessFactor": 0.85,
            },
            "alphaMode": "BLEND" if alpha < 1.0 else "OPAQUE",
            "doubleSided": True,
        }],
        "buffers": [{"byteLength": len(bin_buffer)}],
        "bufferViews": views,
        "accessors": accessors,
    }
    if any(len(bin_buffer) % 4 for _ in [0]):
        pass

    json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "
    while len(bin_buffer) % 4:
        bin_buffer.append(0)
    total = 12 + 8 + len(json_bytes) + 8 + len(bin_buffer)
    out = bytearray(struct.pack("<III", 0x46546C67, 2, total))
    out += struct.pack("<II", len(json_bytes), 0x4E4F534A) + json_bytes
    out += struct.pack("<II", len(bin_buffer), 0x004E4942) + bin_buffer
    path.write_bytes(out)


def write_obj(path: Path, name: str, vertices, triangles) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [f"# Ramp Wreckers authored {name} source", f"o {name}"]
    lines += [f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}" for v in vertices]
    lines += [f"f {a + 1} {b + 1} {c + 1}" for a, b, c in triangles]
    path.write_text("\n".join(lines) + "\n")


def icosphere(subdivisions: int = 1):
    t = (1.0 + math.sqrt(5.0)) / 2.0
    verts = [
        (-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0),
        (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
        (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1),
    ]
    faces = [
        (0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
        (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
        (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
        (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
    ]
    verts = [tuple(c / math.sqrt(sum(x * x for x in v)) for c in v) for v in verts]
    for _ in range(subdivisions):
        cache = {}
        new_faces = []

        def midpoint(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                va, vb = verts[a], verts[b]
                m = tuple((va[i] + vb[i]) / 2 for i in range(3))
                length = math.sqrt(sum(c * c for c in m))
                verts.append(tuple(c / length for c in m))
                cache[key] = len(verts) - 1
            return cache[key]

        for a, b, c in faces:
            ab, bc, ca = midpoint(a, b), midpoint(b, c), midpoint(c, a)
            new_faces += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        faces = new_faces
    return verts, faces


def make_dust() -> None:
    verts, faces = icosphere(1)
    # Squash and dent the blob so each puff reads as a cloud rather than a ball.
    jitter = [0.17, -0.11, 0.08, -0.06, 0.12, -0.15, 0.05, 0.19, -0.09, 0.03, -0.13, 0.07]
    shaped = []
    for i, (x, y, z) in enumerate(verts):
        r = 0.28 * (3.8 + jitter[i % len(jitter)]) / 3.8
        shaped.append((x * r * 1.25, y * r * 0.75 + 0.06, z * r * 1.25))
    normals = []
    for v in shaped:
        length = math.sqrt(sum(c * c for c in v)) or 1.0
        normals.append(tuple(c / length for c in v))
    write_glb(ROOT / "art/meshes/effects/DustPuff.glb", "dust_puff",
              shaped, normals, faces, (0.86, 0.80, 0.66), 0.62)
    write_obj(ROOT / "art/source/effects/DustPuff.obj", "dust_puff", shaped, faces)


def make_shard() -> None:
    verts = [
        (-0.12, -0.08, -0.05), (0.13, -0.06, -0.07), (0.02, -0.09, 0.12),
        (0.01, 0.16, 0.01), (0.04, 0.02, -0.14),
    ]
    faces = [(0, 1, 3), (1, 2, 3), (2, 0, 3), (0, 2, 1), (0, 4, 1), (1, 4, 2),
             (2, 4, 0), (0, 3, 4), (1, 3, 4), (2, 3, 4)]
    norms = []
    for v in verts:
        length = math.sqrt(sum(c * c for c in v)) or 1.0
        norms.append(tuple(c / length for c in v))
    write_glb(ROOT / "art/meshes/effects/DebrisShard.glb", "debris_shard",
              verts, norms, faces, (0.56, 0.55, 0.52))
    write_obj(ROOT / "art/source/effects/DebrisShard.obj", "debris_shard", verts, faces)


def make_engine() -> None:
    """A seamless two-second low-frequency engine loop."""
    sample_rate = 22050
    seconds = 2.0
    frames = int(sample_rate * seconds)
    samples = []
    for i in range(frames):
        t = i / sample_rate
        # Integer cycle counts over the loop keep the loop seam quiet.
        base = 55.0
        phase = 2 * math.pi * base * t
        saw = 2.0 * ((base * t) % 1.0) - 1.0
        thump = math.sin(phase) * 0.55 + math.sin(phase * 2) * 0.22
        rasp = math.sin(2 * math.pi * 220.0 * t) * 0.08
        amplitude = 0.55 + 0.10 * math.sin(2 * math.pi * 2.0 * t)
        value = (thump + saw * 0.28 + rasp) * amplitude
        value = max(-1.0, min(1.0, value))
        # Gentle fade around the seam is unnecessary because cycles are integral.
        samples.append(int(value * 20000))
    data = struct.pack("<" + "h" * len(samples), *samples)
    path = ROOT / "art/audio/engine_loop.wav"
    path.parent.mkdir(parents=True, exist_ok=True)
    header = b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVEfmt "
    header += struct.pack("<IHHIIHH", 16, 1, 1, sample_rate, sample_rate * 2, 2, 16)
    header += b"data" + struct.pack("<I", len(data))
    path.write_bytes(header + data)


if __name__ == "__main__":
    make_dust()
    make_shard()
    make_engine()
    print("Authored effect meshes and engine loop.")
