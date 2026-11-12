# Verification record — Ramp Wreckers v0.1.1 polish branch

This is a verification record for the `feat/v0.1.1-visual-gameplay-polish` branch,
based on the published v0.1.0 commit `7aa507c`. It records commands that were run,
real rendered evidence, and limitations. It is not a release announcement; the branch
has not been merged, tagged or published as v0.1.1.

## Environment

| Item | Value |
|---|---|
| OS | Ubuntu 26.04 x86_64 |
| Engine | Godot 4.7.2 stable **mono** official, commit `ed1daf0bf` |
| .NET SDK | 10.0.115 |
| .NET runtime used by Godot | 10.0.15 |
| GPU | AMD Radeon 890M (radeonsi), Mesa 26.0.8, OpenGL 4.6 Compatibility |
| Export templates | Godot 4.7.2 mono Linux + Windows x86_64 installed |
| Audio device | **unavailable on this host**: PulseAudio library and an ALSA device could not be opened. Godot used its dummy driver. Audio import/player wiring was tested; audible playback was not. |

## Automated results

All commands below were run from the repository root. The full local equivalent is
`./tools/run_all_tests.sh`.

| Check | Result | Evidence |
|---|---|---|
| Static project contract | **1522 checks, 0 failures** | `artifacts/test-logs/static-validation.log` |
| Static bundle integrity | **PASS** — 8 scenes, 34 GLBs, 9 challenges | `artifacts/test-logs/bundle-integrity.log` |
| Authored GLB normals | **34 / 34 files** have POSITION + NORMAL | `artifacts/test-logs/glb-normals.log` |
| Authored material palette | **34 / 34 files** pass palette check | `artifacts/test-logs/material-palette.log` |
| `dotnet build -c Release -warnaserror` | **0 warnings, 0 errors** | `artifacts/test-logs/dotnet-build-release.log` |
| Pure .NET core tests | **33 passed, 0 failed** | `artifacts/test-logs/unit-tests.log` |
| Godot headless editor import | exit 0, no resource/import errors | `artifacts/test-logs/godot-import.log` |
| 60-second wall-clock headless boot | `[BOOT] 60.0s ... ok=True`; no leaked audio/resource warning | `artifacts/test-logs/godot-boot-60s.log` |
| Deterministic runtime tests | **145 passed, 0 failed** | `artifacts/test-logs/runtime-tests.log` |
| Linux export | built (156 MB) and headless-booted | `build/linux/RampWreckers.x86_64`; export log |
| Packaged Linux archive | built and booted after extraction | `dist/RampWreckers-v0.1.1-linux-x64.tar.gz` |
| Windows export | built (188 MB) and packaged; **not executed on this Linux host** | `dist/RampWreckers-v0.1.1-windows-x64.zip` |
| Rendered performance probe | **55–60 FPS** at 1600×900; 402 nodes, ~810 draws (High); **60 FPS**, 402 nodes, ~323 draws (Performance) | `artifacts/test-logs/visual-perf.log` |

Candidate package checksums (not published):

```text
ac6f866533558f9fd5878cd7cf1189e50264ef8c019ab53f83786ff278fd0a10  RampWreckers-v0.1.1-linux-x64.tar.gz
42b2f5fa0d759f06059c0dd4046e766abfcd52f3b7ab9fcc8683d6a5126a0db4  RampWreckers-v0.1.1-windows-x64.zip
95e223b0f88aef83080d824a4bcfbd1ef7770d72d2b721dc19ef158c7e984f86  RampWreckers-v0.1.1-linux-x64/RampWreckers.x86_64
9b1182fb3dbd13045a8b43e6d43a9ce2cb181d91abd54b30b9f335d6fe612e53  RampWreckers-v0.1.1-windows-x64/RampWreckers.exe
```

## P0 root cause: missing authored normals

The severe concentric/ripple pattern across the runway, grandstands, ramps and vehicles
was not a material tint, MSAA setting or shadow-bias bug. Inspecting the GLB binary JSON
showed that the supplied models contained **POSITION attributes but no NORMAL
attributes** on most primitives (for example, `Arena.glb` had 152 primitives and no
normals). The GL Compatibility renderer evaluated shadow and lighting terms without a
valid surface normal, producing the dense moiré/ripple pattern.

Diagnostics ruled out the other candidates: disabling the sun shadow, omni shadow,
all shadows, MSAA, fog, ambient or shadow casting still left the pattern; changing
shadow bias, atlas size, light angle, renderer and exposure changed its contrast but did
not remove it. The pattern disappeared when authored normals were present.

`tools/add_normals_to_glb.py` now computes **authored smooth-angle normals** (45° hard
edge threshold), de-indexes primitives into flat attribute arrays, and writes
POSITION + NORMAL accessors. It does not alter vertex positions, triangles, node
transforms or materials. It has a `--check` mode used by CI and `run_all_tests.sh`.
`tools/restyle_materials.py` applies the authored arcade palette with sRGB→linear
conversion and a `--check` mode. No visible geometry was generated at runtime.

Residual horizontal stripes on the broad grandstand ledges were environment
self-shadow acne, investigated separately with environment cast-shadow and bias probes.
They are removed in the final High preset by a combination of a larger normal bias,
a shorter directional shadow range and a darker, sky-independent fog colour. The
Performance preset disables the sun shadow entirely and is also clean.

## Runtime tests covered

- Nine authored challenges and their exact required part IDs, statue inventories and
  recommended ramp angles.
- Objective progress: named targets use `RequiredParts` and `MinimumBrokenParts`
  independently. Five unrelated breaks never display as knees; Core Shot remains
  0/1 after only the armour is gone and reaches 1/1 after the core breaks.
- Impact energy thresholds, resisted hits, duplicate contact rejection.
- Gnome lean/collapse, knight knee/shield graph, robot armour→core ordering and
  exposed-core weak point.
- **All nine challenges through ordinary driving** (see below), with real vehicle
  physics, the authored ramp, real statue hit volumes and real component breaks.
  No `ResolveImpact` calls, teleports or forced impulses are used by the route runner.
- Knee Trouble uses the rally car because its low nose reaches both knees; the pickup
  can still smash the same statue, but its higher nose hits the torso at full ramp
  speed. This is an authored route/vehicle choice, not a scripted break.
- Camera recovery after impact, C-key cinematic skip, camera never below the arena,
  bounded dust/chips/sparks/flash lifetimes, and cleanup after a scene cycle.
- 30 break/reset cycles restore exact authored transforms; 30 retries do not grow the
  node count (402→402), leave `Engine.TimeScale == 1`, and complete in ~1 s total.
- Save v1 literal JSON compatibility, roundtrip, corrupt/empty file recovery, backup
  fallback, valid-payload rejection, write-failure messaging.
- First-clear rewards exactly once; replay improves medal without duplicating coins;
  Free Smash reward isolation.
- Solid collision presentation: intact legs are world-layer obstacles; the knight
  shield is an authored non-blocking ornament hit volume; broken parts move to the
  debris layer.
- Keyboard smoke: G garage, F Free Smash, C camera; audio assets load.

### Physical challenge routes (runtime log)

Each line is a fresh spawn via the normal challenge selector, then ordinary
`drive_forward` input. The route runner may reverse for another pass. All nine report
`complete=True`:

```text
[ROUTE] challenge=1 First Crash    vehicle=1 ramp=10 score=325 complete=True
[ROUTE] challenge=2 Hat Trick      vehicle=0 ramp=27 score=67  complete=True
[ROUTE] challenge=3 Gnome Down     vehicle=1 ramp=10 score=325 complete=True
[ROUTE] challenge=4 Shield Breaker vehicle=1 ramp=18 score=115 complete=True
[ROUTE] challenge=5 Knee Trouble   vehicle=0 ramp=10 score=235 complete=True
[ROUTE] challenge=6 Knightfall     vehicle=1 ramp=10 score=349 complete=True
[ROUTE] challenge=7 Peel the Armour vehicle=2 ramp=10 score=71 complete=True
[ROUTE] challenge=8 Core Shot      vehicle=2 ramp=10 score=250 complete=True
[ROUTE] challenge=9 Robot Wreck    vehicle=2 ramp=10 score=537 complete=True
```

## Rendered evidence

All images are real viewport captures from the running Godot renderer, saved under
`artifacts/screenshots/`. `artifacts/.gdignore` keeps evidence out of the Godot import
pipeline. The before image is the v0.1.0 baseline at the same spawn/ramp view; the after
image is the v0.1.1 High preset.

| File | Shows |
|---|---|
| `p0_before_after.png` | Side-by-side P0 evidence: dense surface ripples → clean authored-normal surfaces |
| `before_v010_runway.png` | v0.1.0 baseline start view (ripples, washed-out palette) |
| `final_quality0_01_start.png` | v0.1.1 High preset: clean runway, ramp, stands, vehicle, statue |
| `final_quality1_01_start.png` | v0.1.1 Performance preset: shadows/fog/MSAA off |
| `final_gnome_01_start.png`, `final_gnome_04_impact.png`, `final_gnome_05_debris.png` | Pickup / garden gnome start, impact and detaching hat/beard with dust/sparks |
| `final_knight_01_start.png`, `final_knight_04_impact.png`, `final_knight_05_debris.png` | Pickup / iron knight: shield removal, side impact camera, debris |
| `final_robot_01_start.png`, `final_robot_04_impact.png`, `final_robot_05_debris.png` | Monster truck / reactor robot: armour/core hit, sparks, debris |
| `final_knee_01_start.png`, `final_knee_04_impact.png`, `final_knee_05_debris.png` | Rally car / both-knee objective showing `BOTH KNEES 2 / 2` |
| `final_hud720_01_start.png` | Revised HUD at 1280×720 |
| `final_garage1600_garage.png`, `final_garage720_garage.png` | Revised garage at 1600×900 and 1280×720 |
| `final_help720_help.png` | Controls/help modal at 1280×720 |
| `statue_gnome.png`, `statue_knight.png`, `statue_robot.png` | Close statue showcase under revised lighting/palette |

Screenshot-capture runs report 35–53 FPS because each `ViewportTexture.GetImage()` +
`SavePng()` stalls the render thread. The uncaptured performance probe measured 55–60 FPS
(High) and 60 FPS (Performance) at 1600×900 on this host while unrelated processes were
active; the lower end should be treated as a conservative host measurement.

## What was not verified

- **Audible audio**: no usable audio device on this host. The dummy driver is detected
  and engine playback is skipped; WAV import, stream configuration and player wiring
  were tested. A listening pass is still required on real hardware.
- **Windows runtime**: the Windows executable was exported and packaged but cannot be
  run on this Linux host.
- **Human child playtest / physical gamepad**: no child or gamepad was available.
  Keyboard/mouse and synthetic Godot input were tested; UI controls expose normal focus
  and accept/back actions for gamepad navigation.
- **Multi-hour memory profile**: state is bounded over 30 retries and repeated scene
  reloads, but no multi-hour session was profiled.
- **Audio-device shutdown**: after the dummy-driver check was added, the verbose
  headless boot no longer reported leaked `AudioStreamWAV`/playback instances. The
  remaining dummy-driver limitation is environmental, not a claim of audible success.

## Known issues and severity

| Severity | Issue | Notes / mitigation |
|---|---|---|
| Low (environment) | No audio device; dummy driver used. | Engine playback is skipped when the driver is Dummy. Audio must be heard on another host. |
| Low | Windows binary was not executed. | Linux archive was extracted and booted; Windows export completed and is packaged. |
| Low | Large components are solid world obstacles; the shield and some ornaments are hit-only so a low route is not blocked by a thin decoration. | Major statues stop the car instead of letting it look intangible. Once a part fails it becomes bounded rigid-body debris and the car can follow through. Extreme-speed overlap is still possible for a frame. |
| Low | Camera can still be briefly filled by a large debris piece flying at it. | Clearance-tested cinematic candidates, debris-aware lift, C skip and chase recovery are implemented. |
| Low | Save format is version 1 with no migration path yet. | Unknown versions are refused safely; corrupt/empty files recover from backup or defaults. |
| Low | The ramp is fixed width and centred; only angle is adjustable. | Q/E covers 10°–27°; no lateral adjustment is in scope. |
| Low | Statue collision volumes are authored boxes matching low-poly bounds. | Invisible collider approximation is allowed; visible geometry remains authored GLB. |
| Low | The game has no title screen and starts directly in the arena. | Intentional for the target player; garage/modes are one key away. |

## Acceptance gate mapping

- **P0 rendering:** missing GLB normals identified and fixed offline; before/after at
  the same view; no visible procedural geometry; static GLB normal check in CI.
- **P1 lighting/materials/destruction/collision:** authored palette and sky/fog/sun
  tuning; smaller/dimmer bounded effects; side cinematic camera with snap; solid major
  statue components and authored shield exception; all bounded.
- **P2 HUD/progress/playability:** `ObjectiveProgress` model, named vs total independent
  progress, 720p and 1600p layouts, all nine physical routes, corrected hints.
- **P3 cleanup:** dummy-audio detection and no leaked audio instances in the 60 s boot;
  bounded effects/scene nodes; Release build with warnings as errors; save v1 compatibility.

Two acceptance items remain outside this host: audible audio and Windows execution.
They are reported as unverified rather than passed.
