# Coding agent handoff — build Ramp Wreckers v0.1.0

You are the engineer responsible for delivering a **complete, genuinely playable, end-to-end first release** of **Ramp Wreckers**, a 3D single-player stunt/destruction game for an enthusiastic 10-year-old. **Do the work; do not return only a plan.** Start from the supplied Godot project and assets. Follow the staged acceptance criteria below, commit verified milestones, and report evidence accurately.

## Fixed technology and non-negotiable requirements

- **Godot 4.7.2 .NET**, **C#**, **.NET 10**. No GDScript gameplay rewrites. Linux desktop first; provide a Windows export if the templates/environment permit.
- **Everything visible in 3D must be model-driven.** Use the shipped GLB meshes, and author further GLB mesh assets in Blender or another DCC when needed. No runtime procedural geometry; no visible `BoxMesh`, `SphereMesh`, `CylinderMesh`, `CapsuleMesh`, `PrismMesh`, `PlaneMesh`, `ArrayMesh`, `SurfaceTool`, `ImmediateMesh`, `CSG*` or default Godot primitives in final scenes. Invisible `Shape3D` colliders, raycasts, gameplay gizmos visible only in editor, UI controls, and particles are allowed.
- **No automatic fallback geometry.** Missing models must fail loudly during development and be fixed, not replaced with a coloured cube.
- Preserve **editable Godot `.tscn` scenes**, external authored models, asset provenance and a simple source-of-truth workflow. Avoid script-created world trees except for spawning instances of packed scenes and managed effects.
- Fully offline game. No accounts, telemetry, ads, purchases, user-generated online sharing, or violence against people.
- Proper gameplay first: the thrill of picking a truck, driving up the ramp, and physically smashing a statue. Avoid menus, dialogue and unrelated quests getting in the way.
- Do not silently substitute a fake damage bar for visibly broken structural geometry.

## Existing files to inspect first

1. `README.md`, `docs/GAME_DESIGN.md`, `docs/ART_AND_SCENES.md`, `docs/ACCEPTANCE.md`.
2. `project.godot`, `RampWreckers.csproj`, `scenes/Main.tscn`.
3. All `src/*.cs`, especially `VehicleController.cs` and `StatueController.cs`.
4. Inspect the geometry in `art/meshes/**/*.glb` and optionally `mesh_sources.zip` (editable OBJ authoring copies).

**Important:** Assets and scene references have been structurally checked, but this initial handoff was not compiled or run in Godot. Expect to fix issues. Do not misrepresent a static check as a runtime test.

## Delivery sequence

### P0 — Import, build and verify scene composition

- Import the project with Godot .NET 4.7.2 and run `dotnet restore && dotnet build`.
- Fix resource errors, renderer/import issues, C# API mismatches, inverted normals, incorrect transforms/collision alignments, or audio import issues.
- Get `Main.tscn` booting with **arena, ramp, three vehicle scenes, three statue scenes and HUD**; verify the camera can see the car, ramp and target.
- Open every vehicle and statue scene in the editor. All visual meshes must be instances of authored asset files.
- Add an automated static resource validation script: no broken `res://` references, no forbidden visible built-in primitive meshes, all nine authored challenge records reference valid statue parts.
- Commit only after these checks pass.

### P1 — Make the driving and the launch *actually enjoyable*

- Validate and improve the supplied arcade physics: grounded acceleration, rear/front wheel feel, steering, reversing, suspension, traction, airborne motion, collision stability and a predictable driving line.
- **The physical ramps and wheels must align with the visible models.** No invisible wall at the ramp foot or imaginary steep ramp.
- High speed impact with the statue must be achievable with each provided vehicle using ordinary player controls; no scripted teleport to the target.
- Vehicle differences must be obvious: rally car fast/light, pickup balanced, monster truck heavy/slower/large wheels.
- Add mouse camera orbit (right mouse drag or mouse movement option), sensible chase-camera smoothing, recovery when upside down, and a robust instant retry key.
- Add two camera presets or a cinematic impact replay that can be skipped; input remains responsive.
- Test long sustained driving, jumps, falls, flips and rapid resets without physics explosions or stuck vehicles.

### P2 — Real structural destruction, not arbitrary explosions

- Keep authored individual statue components; refine them into clear supports, midsection, head and detachable ornaments.
- Use **explicit structural dependency graphs or authored breakable constraints** per statue (scene data/resources). Each break must be justified by impact direction/strength or supported collapse, not global explosion upon contact.
- A one-leg impact can destabilise a statue; both legs gone should predictably topple its upper structure. The robot's chest armour must protect its core until removed or meaningfully bypassed. A direct weak-point strike should be rewarded.
- On high-energy collision: transfer impulse, detach appropriate parts, create debris/dust/sound, optionally brief slow motion; do not freeze gameplay for long.
- Enforce limits on active debris. Ensure reset restores the full authored statue pose, and repeated impacts cannot award duplicate rewards.
- Add runtime tests or deterministic fixture-mode tests that hit every target part and verify weak-point, support and protected-core behaviour.

### P3 — Nine fair challenges plus Free Smash

Implement all nine goals in `src/ChallengeCatalog.cs`, with proper start/end state, reward once, medal criteria and a visible objective prompt. Balance each goal against real physics, not debug impulses. The intended progression is laid out in `docs/GAME_DESIGN.md`.

- Show impact score, objective progress, replay/retry, challenge completion, vehicle and ramp angle.
- Give 1–3 stars (or bronze/silver/gold) based on understandable objectives; replaying should be worthwhile.
- Earn Wreck Coins; use them for vehicle unlocks, cosmetic paint, or upgrades with real trade-offs. Free Smash should allow all locally unlocked equipment and no punitive economics.
- Save and restore progress, unlocked vehicles, challenge medals, and settings. Store with versioned payload + atomic write / temp replace + safe recovery on corruption. Do not overwrite a good save with invalid data.
- If save fails, show failure honestly rather than claiming success.
- No grind or child-hostile monetisation.

### P4 — Art direction, clarity and polish

- Use the existing authored asset pack as a starting point, refine if necessary; **do not replace it with Godot cube/sphere primitives**. Bright distinctive silhouettes, consistent colours and readable destructible targets.
- Clear visible motion cues (speedometer, acceleration/brake, speed lines only if non-obstructive), takeoff sound, low-frequency crash sound, debris particles, dust clouds, and satisfying collapse.
- Make camera framing reliable, especially at the start line, at the ramp top, during flight, on impact and while inspecting fallen debris.
- Menus/title screen should be minimal and controller/mouse accessible; large font and low reading load.
- One playable arena, 3 vehicles, 3 statues, 9 campaign challenges and Free Smash is sufficient. Avoid additional biomes, multiplayer, level editor and dozens of cars until this is excellent.
- Keep typical desktop performance smooth; provide a performance configuration for less capable machines.

### P5 — Proof and packaging

- Automate build/import tests and gameplay core unit tests where practical.
- Run the acceptance checks in `docs/ACCEPTANCE.md` using the actual Godot runtime; don't claim a test ran if it didn't.
- Run editor headless import, .NET build, game headless boot, per-statue collision/destruction tests, retry/save/corrupt-save tests, then a human-readable manual gameplay audit.
- Document any known limitations. Ensure `.gitignore` prevents `.godot`, `bin`, `obj` and build outputs entering commits.
- Export a Linux build and Windows build if supported; run at least the Linux export.
- Use `gh` to create/push a repository or publish a release **only if the user has explicitly authorised those actions**. No unrequested remote repository creation.

## Required reporting format

Return: project status, version/commit, supported OS, exact tests and counts, evidence paths, playable controls, what you implemented, what you did not implement, and known issues with severity. Separate **statically verified**, **compiled**, **ran headless**, and **tested interactively**. Don't claim v0.1.0 complete unless acceptance passes.

## Success definition

A child can select the pickup truck, drive down the track, hit the angled ramp, fly into the stone gnome, see meaningful pieces break away, instantly retry, switch to the monster truck, then attempt the knight and robot. Nine authored objectives are attainable, progress persists, and crashing is fun enough to repeat without a story or a grind.
