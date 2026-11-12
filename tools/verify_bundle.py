#!/usr/bin/env python3
"""Static, stdlib-only integrity check; NOT a substitute for Godot import/build/run."""
import re
import struct
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
errors = []
scene_files = sorted((ROOT / 'scenes').rglob('*.tscn'))
models = sorted((ROOT / 'art' / 'meshes').rglob('*.glb'))
source = (ROOT / 'src')

if len(scene_files) != 8:
    errors.append(f'Expected 8 Godot scenes; found {len(scene_files)}')
if len(models) != 34:
    errors.append(f'Expected 34 authored GLB models; found {len(models)}')

for scene in scene_files:
    data = scene.read_text(encoding='utf-8')
    for rel in re.findall(r'path="(res://[^\"]+)"', data):
        path = ROOT / rel.removeprefix('res://')
        if not path.is_file():
            errors.append(f'{scene.relative_to(ROOT)} -> missing {rel}')
    for forbidden in (r'\[node[^\n]+type="CSG', r'\[sub_resource[^\n]+type="(?:BoxMesh|SphereMesh|CylinderMesh|PlaneMesh|PrismMesh|CapsuleMesh|ArrayMesh|ImmediateMesh)"', r'\[node[^\n]+type="MeshInstance3D"'):
        if re.search(forbidden, data):
            errors.append(f'Forbidden visible primitive or raw mesh node in {scene.relative_to(ROOT)}')

for cs in source.glob('*.cs'):
    contents = cs.read_text(encoding='utf-8')
    for forbidden in ('new BoxMesh', 'new SphereMesh', 'new CylinderMesh', 'new ArrayMesh',
                      'new SurfaceTool', 'new ImmediateMesh', 'new CsgBox3D'):
        if forbidden in contents:
            errors.append(f'Forbidden geometry construction {forbidden} in {cs.relative_to(ROOT)}')

for model in models:
    with model.open('rb') as f:
        header = f.read(12)
    if len(header) != 12 or header[:4] != b'glTF':
        errors.append(f'Bad GLB header: {model.relative_to(ROOT)}')
    else:
        magic, version, length = struct.unpack('<III', header)
        if version != 2 or length != model.stat().st_size:
            errors.append(f'Bad GLB length/version: {model.relative_to(ROOT)}')

statue_parts = {}
for target in ('Gnome','Knight','Robot'):
    data = (ROOT / 'scenes' / 'statues' / f'{target}.tscn').read_text()
    statue_parts[target] = set(re.findall(r'^PartId = "(\w+)"', data, re.M))
    if len(statue_parts[target]) < 7:
        errors.append(f'Insufficient detachable pieces: {target}')

challenge_src = (source / 'ChallengeCatalog.cs').read_text()
for index, line in enumerate(challenge_src.splitlines(), start=1):
    if 'new("' not in line or not re.search(r', [012], ', line):
        continue
    m = re.search(r', ([012]), (?:Array.Empty<string>\(\)|new\[\]\{([^}]*)\})', line)
    if not m:
        errors.append(f'Challenge parse failed at source line {index}')
        continue
    statue = ('Gnome','Knight','Robot')[int(m.group(1))]
    for part in re.findall(r'"(\w+)"', m.group(2) or ''):
        if part not in statue_parts[statue]:
            errors.append(f'Unknown {statue} part {part} at challenge line {index}')

source_zip = ROOT / 'mesh_sources.zip'
if not source_zip.exists():
    errors.append('Missing editable OBJ source archive')
else:
    with zipfile.ZipFile(source_zip) as z:
        obj_count = len([n for n in z.namelist() if n.endswith('.obj')])
        if obj_count != 32:
            errors.append(f'Expected 32 editable OBJ sources; found {obj_count}')

if errors:
    for error in errors:
        print('FAIL:', error)
    sys.exit(1)
print(f'PASS: static assets and resource references; {len(scene_files)} scenes, {len(models)} GLBs, 9 authored challenges')
print('NOTE: Godot import, C# build, gameplay and exports are NOT tested by this tool.')
