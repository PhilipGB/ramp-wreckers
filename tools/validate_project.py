#!/usr/bin/env python3
"""Static contract validation for Ramp Wreckers.

Checks that the shipping Godot project obeys the model-driven art rules and that
the authored challenge/statue data is internally consistent. This is a static
check only; `tests/RuntimeTests.tscn` exercises the same data in the engine.

Run from the project root:
    python3 tools/validate_project.py
"""
from __future__ import annotations

import json
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ERRORS: list[str] = []
CHECKS = 0


def check(condition: bool, message: str) -> None:
    global CHECKS
    CHECKS += 1
    if not condition:
        ERRORS.append(message)


def tscn_files() -> list[Path]:
    return sorted(p for p in ROOT.glob("**/*.tscn") if ".godot" not in p.parts)


def cs_files() -> list[Path]:
    return sorted(p for p in ROOT.glob("**/*.cs") if ".godot" not in p.parts)


def all_res_paths(text: str) -> list[str]:
    return re.findall(r'path="(res://[^"]+)"', text)


def parse_blocks(text: str, node_type: str) -> list[dict[str, str]]:
    """Return node blocks of one type, with simple key=value properties."""
    blocks = []
    current = None
    for line in text.splitlines():
        if line.startswith("[node ") and f'type="{node_type}"' in line:
            if current is not None:
                blocks.append(current)
            current = {"_header": line, "_body": ""}
        elif line.startswith("[") and current is not None:
            blocks.append(current)
            current = None
        elif current is not None:
            current["_body"] += line + "\n"
    if current is not None:
        blocks.append(current)
    for block in blocks:
        for key, value in re.findall(r"^([A-Za-z0-9_]+) = (.+)$", block["_body"], re.M):
            block[key] = value.strip()
        block["_name"] = re.search(r'name="([^"]+)"', block["_header"]).group(1)
    return blocks


def validate_resources_and_instances() -> None:
    referenced_glbs: set[str] = set()
    for scene in tscn_files():
        text = scene.read_text()
        for res in all_res_paths(text):
            check((ROOT / res.removeprefix("res://")).exists(),
                  f"{scene.relative_to(ROOT)} references missing resource {res}")
            if res.startswith("res://art/meshes/") and res.endswith(".glb"):
                referenced_glbs.add(res)

        # Every ext_resource PackedScene instance must resolve. Parse line by
        # line because Godot does not guarantee field order.
        ext = {}
        for line in text.splitlines():
            if line.startswith("[ext_resource "):
                type_match = re.search(r'type="([^"]+)"', line)
                path_match = re.search(r'path="([^"]+)"', line)
                id_match = re.search(r'id="([^"]+)"', line)
                if type_match and path_match and id_match:
                    ext[id_match.group(1)] = (type_match.group(1), path_match.group(1))
        for instance_id in re.findall(r'instance=ExtResource\("([^"]+)"\)', text):
            check(instance_id in ext,
                  f"{scene.relative_to(ROOT)} instances missing ExtResource id {instance_id}")
            if instance_id in ext:
                kind, path = ext[instance_id]
                check(kind in {"PackedScene", "AudioStream"} or path.endswith(".tscn"),
                      f"{scene.relative_to(ROOT)} instances non-scene resource {path} as a node")

    for glb in sorted(ROOT.glob("art/meshes/**/*.glb")):
        res = "res://" + str(glb.relative_to(ROOT)).replace("\\", "/")
        check(res in referenced_glbs, f"authored GLB is not referenced by any scene: {res}")

    for source in ROOT.glob("art/source/**/*.obj"):
        glb = ROOT / "art" / "meshes" / str(source.relative_to(ROOT / "art" / "source")).replace(".obj", ".glb")
        check(glb.exists(), f"authored mesh source has no GLB output: {source.relative_to(ROOT)}")


def glb_json(path: Path) -> dict:
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from("<III", raw, 0)
    check(magic == 0x46546C67 and version == 2 and total == len(raw),
          f"{path.relative_to(ROOT)} is not a valid GLB 2.0 file")
    offset = 12
    while offset < len(raw):
        length, kind = struct.unpack_from("<II", raw, offset)
        data = raw[offset + 8:offset + 8 + length]
        offset += 8 + length
        if kind == 0x4E4F534A:
            return json.loads(data.decode("utf-8").rstrip("\x00 "))
    return {}


def validate_glb_surface_data() -> None:
    for glb in sorted(ROOT.glob("art/meshes/**/*.glb")):
        js = glb_json(glb)
        primitives = [p for m in js.get("meshes", []) for p in m.get("primitives", [])]
        check(bool(primitives), f"{glb.relative_to(ROOT)} has no mesh primitives")
        for primitive in primitives:
            attrs = primitive.get("attributes", {})
            check("POSITION" in attrs,
                  f"{glb.relative_to(ROOT)} primitive has no POSITION")
            # GL Compatibility self-shadow bias needs authored normals; dropping
            # them caused the v0.1.0 concentric moire regression.
            check("NORMAL" in attrs,
                  f"{glb.relative_to(ROOT)} primitive has no NORMAL attribute")
        check(len(js.get("materials", [])) > 0,
              f"{glb.relative_to(ROOT)} has no authored materials")


def validate_hud_contract() -> None:
    hud = ROOT / "scenes/Hud.tscn"
    check(hud.exists(), "missing redesigned HUD scene")
    if not hud.exists():
        return
    text = hud.read_text()
    for node in [
        'name="ObjectivePanel"', 'name="Required"', 'name="RequiredBar"',
        'name="Total"', 'name="TotalBar"', 'name="Status"', 'name="Speed"',
        'name="SpeedBar"', 'name="Stats"', 'name="BannerLabel"',
        'name="HelpPanel"', 'name="HelpClose"', 'name="Garage"',
        'name="RallyButton"', 'name="PickupButton"', 'name="MonsterButton"',
        'name="EngineButton"', 'name="GripButton"', 'name="PaintButton"',
        'name="QualityButton"', 'name="VolumeButton"',
    ]:
        check(node in text, f"HUD is missing {node}")
    game_root = (ROOT / "src/GameRoot.cs").read_text()
    check("ObjectiveProgress progress = _statue.GetObjectiveProgress(challenge)" in game_root,
          "HUD does not use the named ObjectiveProgress model")
    check("ToggleHelp()" in game_root and '"help"' in game_root,
          "H key control help is not wired")
    check("CountsAsBrokenWhenUnsupported" in
          (ROOT / "scenes/statues/Gnome.tscn").read_text(),
          "statue collapse policy is not authored in scenes")
    check("SolidWhenIntact" in (ROOT / "src/StatuePart.cs").read_text(),
          "intact statue parts do not author a solid-vs-hit-volume choice")
    knight = (ROOT / "scenes/statues/Knight.tscn").read_text()
    check('PartId = "Shield"\nSolidWhenIntact = false' in knight,
          "knight shield is not authored as a non-blocking ornament hit volume")


def validate_no_visible_primitives() -> None:
    forbidden_nodes = [
        "BoxMesh", "SphereMesh", "CylinderMesh", "CapsuleMesh", "PrismMesh",
        "PlaneMesh", "QuadMesh", "ArrayMesh", "ImmediateMesh", "TextMesh",
        "TorusMesh", "RibbonTrailMesh", "PointMesh", "PrimitiveMesh",
    ]
    for scene in tscn_files():
        text = scene.read_text()
        for node in forbidden_nodes:
            check(f'type="{node}"' not in text,
                  f"{scene.relative_to(ROOT)} contains forbidden visible mesh node {node}")
        for csg in re.findall(r'type="(CSG[A-Za-z0-9_]*)"', text):
            check(False, f"{scene.relative_to(ROOT)} contains forbidden CSG node {csg}")
        for mesh_instance in parse_blocks(text, "MeshInstance3D"):
            check("mesh = SubResource(" not in mesh_instance.get("_body", ""),
                  f"{scene.relative_to(ROOT)} creates a MeshInstance3D from a built-in subresource")

    forbidden_code = [
        r"\bnew\s+(?:BoxMesh|SphereMesh|CylinderMesh|CapsuleMesh|PrismMesh|PlaneMesh|QuadMesh|ArrayMesh|ImmediateMesh|TextMesh|TorusMesh|RibbonTrailMesh|PointMesh|PrimitiveMesh)\b",
        r"\bSurfaceTool\b",
        r"\bAddSurfaceFromArrays\b",
        r"\bCreateTrimeshShape\b",
        r"\bCSG[A-Za-z0-9_]*\b",
    ]
    for source in cs_files():
        text = source.read_text()
        for pattern in forbidden_code:
            for match in re.finditer(pattern, text):
                line = text[:match.start()].count("\n") + 1
                # Comments explaining the policy are allowed.
                line_text = text.splitlines()[line - 1].strip()
                if line_text.startswith("//") or line_text.startswith("///"):
                    continue
                check(False, f"{source.relative_to(ROOT)}:{line} uses forbidden mesh generation: {match.group(0)}")


def validate_statue_scenes() -> dict[str, set[str]]:
    inventories: dict[str, set[str]] = {}
    for scene_path in ["scenes/statues/Gnome.tscn", "scenes/statues/Knight.tscn", "scenes/statues/Robot.tscn"]:
        scene = ROOT / scene_path
        check(scene.exists(), f"missing statue scene {scene_path}")
        if not scene.exists():
            continue
        text = scene.read_text()
        controller = parse_blocks(text, "Node3D")
        root = next((b for b in controller if b["_name"].endswith("Statue")), {})
        check(root.get("DisplayName") is not None, f"{scene_path} statue has no DisplayName")
        check(root.get("RequiredBrokenParts") is not None, f"{scene_path} statue has no RequiredBrokenParts")

        parts = parse_blocks(text, "RigidBody3D")
        check(len(parts) >= 7, f"{scene_path} has only {len(parts)} breakable parts (expected 7+)")
        ids: set[str] = set()
        supports = 0
        for part in parts:
            part_id = part.get("PartId", "").strip('"')
            check(bool(part_id), f"{scene_path} has a statue part without PartId")
            check(part_id not in ids, f"{scene_path} has duplicate PartId {part_id}")
            ids.add(part_id)
            threshold = part.get("BreakThreshold")
            check(threshold is not None and float(threshold) > 0,
                  f"{scene_path}/{part_id} has no positive BreakThreshold")
            if part.get("IsSupport") == "true":
                supports += 1
            # Every visible part must be an instance of an authored GLB scene.
            check(f'[node name="Art" parent="{part["_name"]}" instance=' in text,
                  f"{scene_path}/{part_id} has no authored Art instance")
            check(f'[node name="HitArea" type="Area3D" parent="{part["_name"]}"]' in text,
                  f"{scene_path}/{part_id} has no authored gameplay HitArea")
            check(f'[node name="HitShape" type="CollisionShape3D" parent="{part["_name"]}/HitArea"]' in text,
                  f"{scene_path}/{part_id} has no authored HitArea collision shape")
            art_lines = [line for line in text.splitlines()
                         if line.startswith(f'[node name="Art" parent="{part["_name"]}" instance=')]
            check(bool(art_lines) and "ExtResource" in art_lines[0],
                  f"{scene_path}/{part_id} Art is not an external authored asset")
        check(supports >= 2, f"{scene_path} needs at least two support parts (found {supports})")

        for part in parts:
            part_id = part.get("PartId", "").strip('"')
            supported = part.get("SupportedByCsv", "").strip('"')
            for support in [s.strip() for s in supported.split(",") if s.strip()]:
                check(support in ids,
                      f"{scene_path}/{part_id} is supported by missing part {support}")
            protected = part.get("ProtectedByCsv", "").strip('"')
            for protector in [s.strip() for s in protected.split(",") if s.strip()]:
                check(protector in ids,
                      f"{scene_path}/{part_id} is protected by missing part {protector}")

        # No authored visible primitive geometry anywhere in the statue.
        check("BoxMesh" not in text and "SphereMesh" not in text,
              f"{scene_path} contains a primitive mesh")
        inventories[scene_path] = ids
    return inventories


def validate_challenges(inventories: dict[str, set[str]]) -> None:
    source = (ROOT / "src/ChallengeCatalog.cs").read_text()
    entries = re.findall(
        r'new\(\s*"([^"]+)",\s*"([^"]+)",\s*(\d+),\s*'
        r'(Array\.Empty<string>\(\)|new\[\]\s*\{([^}]*)\}),\s*'
        r'(\d+),\s*(\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)f?,\s*(\d+),\s*"',
        source,
    )
    check(len(entries) == 9, f"expected 9 challenge records, found {len(entries)}")
    statues = ["scenes/statues/Gnome.tscn", "scenes/statues/Knight.tscn", "scenes/statues/Robot.tscn"]
    for entry in entries:
        name, _goal, statue_index, required_token, required_csv, minimum, reward, silver, gold, seconds, ramp = entry
        statue_index = int(statue_index)
        check(0 <= statue_index < len(statues), f"challenge {name} has invalid statue index")
        if not (0 <= statue_index < len(statues)):
            continue
        required = [p.strip().strip('"') for p in required_csv.split(",") if p.strip()]
        for part in required:
            check(part in inventories.get(statues[statue_index], set()),
                  f"challenge {name} requires {part}, missing from {statues[statue_index]}")
        check(int(minimum) > 0, f"challenge {name} has a non-positive part count")
        check(int(reward) > 0, f"challenge {name} has no coin reward")
        check(int(gold) >= int(silver), f"challenge {name} has invalid medal scores")
        check(float(seconds) > 0, f"challenge {name} has an invalid gold time")
        check(10 <= int(ramp) <= 27, f"challenge {name} has an out-of-range recommended ramp")


def validate_policy_files() -> None:
    project = (ROOT / "project.godot").read_text()
    check('run/main_scene="res://scenes/Main.tscn"' in project, "main scene is not configured")
    check("C#" in project, "project does not declare C# support")
    check("RampWreckers" in project, "project has no .NET assembly name")
    gitignore = (ROOT / ".gitignore").read_text()
    for ignored in [".godot/", "**/bin/", "**/obj/"]:
        check(ignored in gitignore, f".gitignore is missing {ignored}")
    for scene in tscn_files():
        text = scene.read_text()
        check("res://.godot" not in text, f"{scene.relative_to(ROOT)} references generated .godot files")


def main() -> int:
    validate_resources_and_instances()
    validate_glb_surface_data()
    validate_no_visible_primitives()
    inventories = validate_statue_scenes()
    validate_challenges(inventories)
    validate_hud_contract()
    validate_policy_files()

    print(f"Static validation: {CHECKS} checks, {len(ERRORS)} failure(s).")
    if ERRORS:
        for error in ERRORS:
            print("FAIL:", error)
        return 1
    print("PASS: resource references, normals, model-driven art, statue graphs, HUD and nine challenges.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
