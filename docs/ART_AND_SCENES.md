# Art and scene contract

## Absolute geometry policy

- **Permitted visible geometry:** imported `.glb` scenes from the authored art pack or newly authored Blender/DCC models, instanced and positioned in `.tscn` scenes. World geometry must be stored in assets, never built at runtime.
- **Forbidden visible geometry:** script-generated mesh buffers; Godot built-in `BoxMesh`, `SphereMesh`, `CylinderMesh`, `PlaneMesh`, `CapsuleMesh`, `ArrayMesh`, `ImmediateMesh`; CSG-generated level geometry; editor-only gizmos shipped as gameplay artwork; "just use cubes for now" fallbacks.
- **Permitted non-visual physics:** `BoxShape3D`, `SphereShape3D`, `CapsuleShape3D`, raycasts, constraints and collision geometry; this does not render a mesh. Improve these to match the models.
- Author new mesh files offline in DCC and commit the source as well as exported GLB. `mesh_sources.zip` is the current OBJ source archive. Imported GLB is editable in Blender, and its baked material palette is preserved there.
- Changing ramp angle rotates an existing `StaticBody3D` containing an authored model and physics collider. It does **not** regenerate any mesh vertices.

## Hierarchy and responsibilities

- `scenes/Main.tscn`: arena asset instance, invisible ground collider, single authored ramp mesh and invisible ramp collider, two safety cones, directional lighting, third-person camera, vehicle spawn marker, statue anchor, HUD and sound cues.
- `scenes/vehicles/*.tscn`: a `RigidBody3D` with modelled car GLB, invisible box collider and ground raycast, controlled by `VehicleController.cs`.
- `scenes/statues/*.tscn`: a non-physical root `StatueController`, static authored pedestal, and 7–8 independently detachable `StatuePart` bodies. All visible pieces are separate GLB scenes. While intact, each part keeps its authored shape as an invisible `Area3D` hit volume and has no solid collision; when broken it becomes a bounded RigidBody3D with solid debris collision. This is the arcade "smash through" choice.
- `src/GameRoot.cs`: runtime scene selection, challenge/reward flow, persistence, input and camera logic; no dynamic geometry.
- `src/StatueController.cs`: authored support/protection graph execution, collapse and reset policy.
- `src/ChallengeCatalog.cs`: nine authored objectives, not generated levels.

## Model register

| Directory | Count | Contents |
|---|---:|---|
| `art/meshes/vehicles/` | 3 | RallyCar, PickupTruck, MonsterTruck |
| `art/meshes/environment/` | 3 | Arena, Ramp, SafetyCone |
| `art/meshes/statues/gnome/` | 9 | Pedestal, both legs, torso, head, beard, hat, both arms |
| `art/meshes/statues/knight/` | 8 | Pedestal, both legs, torso, head, both arms, shield |
| `art/meshes/statues/robot/` | 9 | Pedestal, both legs, torso, core, head, both arms, armour |
| `art/meshes/effects/` | 2 | DustPuff, DebrisShard (offline-authored by `tools/make_assets.py`) |

34 runtime GLB models total, plus OBJ authoring copies in `art/source/` and the original
`mesh_sources.zip` archive. The generated engine loop is `art/audio/engine_loop.wav`.

## Direction for improvement

- Vehicle silhouettes should be distinct at distance: low long rally car, high-sided pickup, huge-tyred monster truck.
- Use a bright toy-like palette: teal, coral red, golden yellow/orange, grey steel, pale stone. Maintain dark tyre/readable body contrast.
- Model windscreens, lamps, bumpers and wheel hubs as actual asset geometry. Avoid flat unmarked bricks.
- Statues should read from a moving camera and have visually clear weak spots; add authored cracks, stripes, bolts, emblem and joint details when polishing.
- Use baked materials, visible crash debris and particles responsibly; focus on frame time and instant reset.

## Model origin and coordinate expectations

Godot coordinates: +Y up, vehicle forward -Z, stadium runway points -Z, statue at z≈-23. Ramp near pivot at z≈+10, rotates around X. Imported GLB models generally have local origins suitable for their parent scene; do **not** bake an additional 90° orientation rotation without checking in the editor.

The OBJ sources and GLB are authoring artefacts; the `.tscn` hierarchy and `.glb` files are the runtime source of truth. If you rework an asset, re-export it and check the attached collision shape.
