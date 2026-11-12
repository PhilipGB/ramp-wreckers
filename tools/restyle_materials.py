#!/usr/bin/env python3
"""Apply the authored Ramp Wreckers arcade palette to imported GLB materials.

The models were delivered with very pale base colours. This offline tool deepens
and separates the palette without changing geometry, materials' PBR structure or
the editable OBJ sources. Vehicle body panels are still tinted at runtime by paint.

Usage:
    python3 tools/restyle_materials.py
    python3 tools/restyle_materials.py --check
"""
from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942

# old sRGB base colour -> new sRGB base colour. Values are authored palette picks,
# not generated tints. Unknown colours are left untouched.
PALETTE = {
    (0.38823529411764707, 0.5294117647058824, 0.5450980392156862): (0.24, 0.37, 0.43),   # runway slate
    (0.2627450980392157, 0.3254901960784314, 0.41568627450980394): (0.13, 0.20, 0.29),   # deep steel
    (0.19215686274509805, 0.23529411764705882, 0.3215686274509804): (0.07, 0.11, 0.18),  # shadow steel
    (1.0, 0.9568627450980393, 0.8705882352941177): (0.84, 0.88, 0.85),                   # warm white
    (0.9725490196078431, 0.8352941176470589, 0.4196078431372549): (0.95, 0.66, 0.14),    # chevron gold
    (0.2235294117647059, 0.2823529411764706, 0.3607843137254902): (0.10, 0.15, 0.23),    # barrier navy
    (0.7098039215686275, 0.796078431372549, 0.8470588235294118): (0.50, 0.63, 0.72),    # light metal
    (0.9607843137254902, 0.6352941176470588, 0.30196078431372547): (0.94, 0.45, 0.12),  # safety orange
    (0.4588235294117647, 0.5254901960784314, 0.6039215686274509): (0.27, 0.35, 0.46),   # bluish steel
    (0.30980392156862746, 0.8235294117647058, 0.7686274509803922): (0.03, 0.72, 0.66),  # reactor cyan
    (0.9647058823529412, 0.7764705882352941, 0.35294117647058826): (0.96, 0.61, 0.10),  # brass
    (0.9294117647058824, 0.3843137254901961, 0.3607843137254902): (0.86, 0.20, 0.17),   # gnome coral
    (0.5647058823529412, 0.4588235294117647, 0.796078431372549): (0.39, 0.25, 0.72),    # violet
    (0.9372549019607843, 0.7176470588235294, 0.6470588235294118): (0.93, 0.53, 0.37),   # gnome skin
    (0.9098039215686274, 0.8862745098039215, 0.8352941176470589): (0.88, 0.82, 0.68),   # warm beard
    (0.12549019607843137, 0.5490196078431373, 0.592156862745098): (0.02, 0.44, 0.48),   # gnome teal
    (0.7725490196078432, 0.8156862745098039, 0.8392156862745098): (0.62, 0.70, 0.76),   # knight silver
    (0.6235294117647059, 0.6627450980392157, 0.7137254901960784): (0.40, 0.47, 0.57),   # knight leg steel
    (0.40000000000000002, 0.45098039215686275, 0.5254901960784314): (0.22, 0.28, 0.38),  # dark trim
    (0.30196078431372547, 0.5490196078431373, 0.9098039215686274): (0.06, 0.30, 0.82),  # shield blue
    (0.13333333333333333, 0.14901960784313725, 0.20392156862745098): (0.055, 0.07, 0.11), # tyre black
    (0.43137254901960786, 0.8196078431372549, 0.8745098039215686): (0.12, 0.66, 0.80),  # cyan glass
}
def srgb_to_linear(value: float) -> float:
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


EMISSIVE = {
    "art/meshes/statues/robot/Core.glb": (0.0, 0.30, 0.25),
}


def parse(path: Path):
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from("<III", raw, 0)
    if magic != GLB_MAGIC or version != 2:
        raise ValueError(f"not a GLB: {path}")
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
        raise ValueError(f"missing chunk in {path}")
    return js, binary


def write(path: Path, js: dict, binary: bytearray) -> None:
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


def key(color) -> tuple[float, float, float]:
    return tuple(round(float(c), 6) for c in color[:3])


LINEAR_PALETTE = {
    key(new): tuple(srgb_to_linear(c) for c in new)
    for new in PALETTE.values()
}

# Final authored linear colours for the arena. These are deliberately lifted
# from the very dark linear conversions so the runway reads as midnight slate,
# not black, while keeping enough separation from the bright vehicles.
ENV_FINAL = {
    (0.046964, 0.112805, 0.154872): (0.13, 0.23, 0.30),   # runway
    (0.015325, 0.033105, 0.068385): (0.05, 0.09, 0.15),    # deep stand steel
    (0.005981, 0.011645, 0.027212): (0.025, 0.04, 0.075),  # shadow steel
    (0.673860, 0.748414, 0.692071): (0.72, 0.76, 0.72),    # warm white
    (0.890005, 0.393123, 0.017389): (0.95, 0.55, 0.02),    # chevron gold
    (0.010023, 0.019607, 0.043234): (0.035, 0.06, 0.11),   # barrier navy
    (0.214041, 0.354692, 0.476999): (0.34, 0.46, 0.58),    # light metal
    (0.868899, 0.170645, 0.013412): (0.93, 0.34, 0.03),    # safety orange
    (0.059254, 0.100482, 0.178868): (0.16, 0.25, 0.36),    # bluish steel
    (0.002322, 0.476999, 0.393123): (0.01, 0.62, 0.52),    # accent teal
    (0.911408, 0.330344, 0.010023): (0.95, 0.55, 0.02),    # brass
    (0.710566, 0.033105, 0.024515): (0.80, 0.12, 0.06),    # coral
    (0.125967, 0.050876, 0.476999): (0.34, 0.12, 0.68),    # violet
    (0.090196, 0.149020, 0.211765): (0.09, 0.14, 0.21),    # ramp charcoal
}


def restyle(path: Path) -> int:
    js, binary = parse(path)
    rel = str(path.relative_to(ROOT))
    palette = {key(old): new for old, new in PALETTE.items()}
    palette.update(LINEAR_PALETTE)
    changed = 0
    for material in js.get("materials", []):
        pbr = material.setdefault("pbrMetallicRoughness", {})
        base = pbr.get("baseColorFactor", [1, 1, 1, 1])
        new = ENV_FINAL.get(key(base)) if rel.startswith("art/meshes/environment") else None
        if new is None and "/statues/knight/" in rel:
            # Distinct forged-steel knight: pale steel plates, dark blue joints,
            # royal-blue shield and gold trim. Keeps the knight readable next to
            # the mint/navy robot and the red gnome.
            knight_overrides = {
                (0.342392, 0.447988, 0.538236): (0.52, 0.58, 0.68),
                (0.12549, 0.211765, 0.376471): (0.16, 0.22, 0.32),
                (0.279412, 0.379412, 0.559412): (0.30, 0.37, 0.48),
                (0.011765, 0.156863, 0.74902): (0.02, 0.22, 0.75),
                (0.819608, 0.219608, 0.066667): (0.95, 0.55, 0.06),
                (0.16, 0.22, 0.3): (0.58, 0.63, 0.70),
                (0.059, 0.1, 0.179): (0.12, 0.17, 0.26),
                (0.133, 0.187, 0.285): (0.26, 0.33, 0.44),
                (0.005, 0.073, 0.638): (0.02, 0.16, 0.82),
                (0.911, 0.33, 0.01): (0.95, 0.55, 0.06),
            }
            new = knight_overrides.get(key(base))
        if new is None and "/statues/robot/" in rel and key(base) == (0.342392, 0.447988, 0.538236):
            new = (0.24, 0.30, 0.36)   # gunmetal
        if new is None:
            new = palette.get(key(base))
        if new is not None:
            pbr["baseColorFactor"] = [new[0], new[1], new[2], base[3] if len(base) > 3 else 1.0]
            changed += 1
        # Keep surfaces matte enough to read the low-poly faceting, but with enough
        # response to separate vehicles and statues from the arena.
        pbr["metallicFactor"] = 0.05 if rel.startswith("art/meshes/environment") else 0.12
        pbr["roughnessFactor"] = 0.70
        if rel in EMISSIVE and key(base) == (0.30980392156862746, 0.8235294117647058, 0.7686274509803922):
            material["emissiveFactor"] = list(EMISSIVE[rel])
    if changed:
        write(path, js, binary)
    return changed


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    files = sorted((ROOT / "art" / "meshes").rglob("*.glb"))
    total = 0
    for path in files:
        if args.check:
            js, _ = parse(path)
            rel = str(path.relative_to(ROOT))
            pale = []
            for material in js.get("materials", []):
                base = material.get("pbrMetallicRoughness", {}).get("baseColorFactor", [1, 1, 1, 1])
                if rel.startswith("art/meshes/environment"):
                    continue
                if key(base) in PALETTE or key(base) in LINEAR_PALETTE:
                    pale.append(base)
            if pale:
                print(f"PALE: {path.relative_to(ROOT)} ({len(pale)} materials)")
                total += len(pale)
            continue
        count = restyle(path)
        if count:
            print(f"restyled {count:2} materials in {path.relative_to(ROOT)}")
    if args.check:
        if total:
            raise SystemExit(f"{total} materials still use the old pale palette")
        print(f"OK: {len(files)} GLB files use the authored arcade palette")
    else:
        print("Material restyle complete.")


if __name__ == "__main__":
    main()
