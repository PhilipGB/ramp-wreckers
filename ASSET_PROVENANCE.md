# Asset provenance

Ramp Wreckers is fully offline and ships only locally authored or supplied assets.

## Supplied starter art pack

The following assets were supplied with the Ramp Wreckers coding handoff:

- `art/meshes/vehicles/` — RallyCar, PickupTruck, MonsterTruck GLB models.
- `art/meshes/environment/` — Arena, Ramp, SafetyCone GLB models.
- `art/meshes/statues/gnome|knight|robot/` — independently detachable statue
  component GLB models.
- `art/audio/crash.wav`, `launch.wav`, `reward.wav` — original supplied cues.
- `mesh_sources.zip` — original editable OBJ authoring copies.

The supplied pack includes no third-party libraries, fonts, textures, music or
licences requiring attribution. The repository does not redistribute downloaded
assets from asset stores.

## Added during implementation

- `art/meshes/effects/DustPuff.glb` and `DebrisShard.glb` were authored for this
  project by the offline generator in `tools/make_assets.py`.
- `art/source/effects/*.obj` are the editable authoring sources for those two
  meshes.
- `art/audio/engine_loop.wav` is an original two-second engine sample generated
  offline by `tools/make_assets.py`.
- All gameplay code, scene hierarchy, collision layout, UI and tests in this
  repository were written for Ramp Wreckers.
- The supplied/imported GLBs had POSITION data but no NORMAL attributes.
  `tools/add_normals_to_glb.py` computes authored smooth-angle normals (45° hard-edge
  threshold) offline and writes POSITION+NORMAL accessors. It does not change vertex
  positions, triangles, node transforms or materials. All 34 runtime GLBs pass its
  `--check`.
- `tools/restyle_materials.py` applies the authored arcade palette (sRGB→linear) and
  material response. It changes material factors only; it does not add geometry. All
  34 GLBs pass its `--check`.

## Geometry policy

Every visible 3D object is an authored GLB asset instantiated in an editable
`.tscn`. The shipping project contains no visible Godot primitive meshes, CSG
geometry, `ArrayMesh`, `ImmediateMesh` or runtime mesh generation. Invisible
collision shapes, raycasts, `Area3D` hit volumes, UI and short-lived effect
instances are used for gameplay only.
