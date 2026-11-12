# Verification record — Ramp Wreckers v0.1.0

This file records what was actually executed for the first playable release. It is a
verification record, not a plan. The working tree is committed in stages; the final
commit SHA is reported with the release.

## Environment

| Item | Value |
|---|---|
| OS | Ubuntu 26.04 x86_64 |
| Engine | Godot 4.7.2 stable **mono** official, commit `ed1daf0bf` |
| .NET SDK | 10.0.115 |
| .NET runtime used by Godot | 10.0.15 |
| GPU | AMD Radeon 890M (radeonsi), Mesa 26.0.8, OpenGL 4.6 Compatibility |
| Export templates | Godot 4.7.2 mono Linux + Windows x86_64 installed |
| Audio device | **unavailable on this host**: PulseAudio library missing and ALSA device could not open. Godot fell back to its dummy audio driver. Audio assets and player wiring were tested, but audible playback was not. |

## Automated results

| Check | Result | Evidence |
|---|---|---|
| Static project contract | **550 checks, 0 failures** | `artifacts/test-logs/static-validation.log` |
| `dotnet build -c Release` | **0 warnings, 0 errors** | `artifacts/test-logs/dotnet-build-release.log` |
| Pure .NET core tests | **27 passed, 0 failed** | `artifacts/test-logs/unit-tests.log` |
| Godot headless editor import | exit 0, no resource/import errors | `artifacts/test-logs/godot-import.log` |
| 60-second headless boot | no script errors during play | `artifacts/test-logs/godot-boot-60s.log` |
| Deterministic runtime tests | **90 passed, 0 failed** | `artifacts/test-logs/runtime-tests.log` |
| Linux export | produced and booted headless | `artifacts/test-logs/export-linux.log`, `export-linux-boot.log` |
| Windows export | produced (not executable on this Linux host) | `artifacts/test-logs/export-windows.log` |
| Rendered performance probe | **60 FPS**, 383 object nodes, 223 draw calls at 1600×900, high quality | final interactive probe (see below) |

### Runtime tests covered

- Nine authored challenges; required part IDs exist on their specific statue.
- Impact energy thresholds: low-speed contact awards nothing; below-threshold energy is
  resisted; at-threshold breaks; duplicate contacts cannot double-count.
- Gnome one-leg lean, both-legs torso collapse, head/arm/hat dependencies.
- Knight shield detachment and both-legs collapse.
- Robot armour blocks a direct core strike, transfers damage to armour, exposed core
  weak-point strike breaks it, and a collapsed torso detaches the core intact.
- All nine goals can be completed from real part breaks using collision-scale energy.
- 30 break/reset cycles restore exact authored transforms and state.
- Ramp collider at 10°, 16°, 27°: flush foot and correctly rising deck.
- Save roundtrip, version marker, corrupt JSON recovery from backup, empty file,
  invalid payload refusal, write failure with honest error.
- First-clear coins exactly once; replay improves medal without duplicating coins.
- Audio assets load with usable length.
- Real keyboard input smoke: G opens/closes garage, F toggles Free Smash, Free Smash
  destruction cannot grant campaign coins, C cycles camera presets.
- 30 retries in 1014 ms total (about 34 ms each), with bounded state.
- Physics-driven end-to-end route: all three vehicles drive from spawn over the ramp
  and damage the gnome, with no teleport or forced impulse.
- `First Crash` is completed and its first-clear reward saved during the automated route.

## Rendered evidence

All screenshots are real 1600×900 viewport captures from the running Godot game in
`artifacts/screenshots/`. The `artifacts/.gdignore` file keeps evidence out of the
Godot resource import/export pipeline.

| File | Shows |
|---|---|
| `pickup_gnome_01_start.png` | Pickup, start line, HUD, ramp and gnome framing |
| `pickup_gnome_02_ramp.png` | Physical ramp approach and HUD/speedometer |
| `pickup_gnome_04_impact.png` | Car at the gnome, challenge banner, destruction |
| `pickup_gnome_05_debris.png` | Gnome mid-destruction and dust/debris |
| `rally_knight_01_start.png` | Rally car and Iron Knight |
| `rally_knight_04_impact.png` | Knight impact, camera, HUD progress |
| `rally_knight_05_debris.png` | Knight collapse/debris |
| `monster_robot_01_start.png` | Monster truck and Reactor Robot |
| `monster_robot_04_impact.png` | Robot impact and protected-core feedback |
| `monster_robot_05_debris.png` | Robot debris state |
| `garage_garage.png` | Garage UI: vehicles, upgrades, paint, quality and volume |

The earlier animation/movie capture at 1600×900 reported 1.25 ms CPU and 2.34 ms GPU
render time per frame on this host (Movie Maker mode, 30 FPS recording).

## What was not verified

- **Audible audio** could not be heard on this host because Godot had no usable audio
  device. The WAV files import, the players are wired, and the engine loop is configured
  in code. A machine with a working audio device should be used for a listening pass.
- **Windows runtime**: `build/windows/RampWreckers.exe` was exported successfully but
  cannot be executed on this Linux host.
- **Human child playtest**: no child was available. Gameplay was exercised through the
  real physics-driven route, synthetic keyboard events through Godot's input system, and
  rendered screenshots. Balance judgments are based on those runs and the authored
  targets, not on a human play session.
- **Gamepad navigation**: actions are mapped and UI controls accept standard gamepad
  focus/accept input, but no gamepad was connected for a physical test.
- **Long-session memory profiling**: state is bounded over 30 retries and 20 smashed
  statues, but no multi-hour session profile was run.

## Known issues and severity

| Severity | Issue | Notes / mitigation |
|---|---|---|
| Low (environment) | Godot reports `1 resources still in use at exit` and two leaked audio objects during shutdown, and logs an audio-device `ERR_CANT_OPEN`, when using the dummy driver. | The host has no PulseAudio/ALSA device. The game stops and clears audio players on exit. No in-game error or functional impact. A machine with working audio should be used for the listening pass. |
| Medium (design) | Intact statue parts are `Area3D` hit volumes, not solid obstacles. A fast vehicle can overlap the statue silhouette for the impact frame, then parts become solid rigid-body debris. | Deliberate arcade choice so a high-speed strike can break multiple components and feel like smashing through the statue. It avoids the car stopping dead on a decorative hat while a 2-tonne truck is moving at 25 m/s. |
| Low | Camera avoidance handles most debris, but a large component flying directly at the camera can briefly fill the view. | The first-break camera picks clearance-checked positions and stays on the statue for the collapse; C switches preset/skips the close-up. |
| Low | The ramp is fixed width and centered; no lateral/forward adjustment. | Optional in the design; Q/E covers the required angle range. |
| Low | Save format is version 1 with no migration path yet. | Unknown/future versions are refused safely; corrupt/empty files recover from backup or defaults. |
| Low | Windows export was not launched. | Linux export was launched and booted successfully. |
| Low | No title screen; the game starts directly in the arena. | Intentional to minimise friction for the target player. Garage/modes are one key/click away. |
| Low | UI gamepad focus/accept was not physically tested. | Keyboard and mouse paths are tested; gamepad actions are mapped. |
| Low | Statue collision volumes are simple boxes matching the low-poly part bounds, not per-triangle meshes. | Invisible collider approximation is allowed by the art contract; visible geometry remains authored GLB. |

## Acceptance gate mapping (summary)

- **Gate 0:** static validation, Release build, headless import, 60 s boot, every GLB
  referenced, no forbidden visible primitives.
- **Gate 1:** input smoke, three physical vehicle routes over the ramp, ramp collider
  regression at 10/16/27°, 30 fast retries, low-speed damage rejection.
- **Gate 2:** structural graph tests, support collapse, armour/core ordering, bounded
  debris, 30 reset cycles, duplicate-hit rejection.
- **Gate 3:** nine challenge feasibility checks, medals, first-clear rewards, saved
  progress, corrupt/empty/write-failure tests, Free Smash reward isolation.
- **Gate 4:** audio asset load, keyboard/mouse controls and on-screen hints, rendered
  screenshots, Linux export boot, Windows export file, performance probe.

Two acceptance items cannot be fully closed on this host: audible audio validation and
running the Windows binary. Both are called out above rather than claimed as passed.
