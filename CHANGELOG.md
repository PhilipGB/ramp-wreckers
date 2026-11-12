# Changelog

All notable changes to Ramp Wreckers are recorded here.

## Unreleased — v0.1.1 Visual and Gameplay Polish

### Fixed

- Removed the severe concentric/ripple rendering artifact: supplied GLBs had no
  authored NORMAL attributes. Added authored smooth-angle normals to all 34 runtime
  GLBs with `tools/add_normals_to_glb.py`; positions, triangles, transforms and
  materials are unchanged. Added a CI `--check`.
- Removed residual environment self-shadow acne on broad ledges with tuned shadow
  bias/normal bias, a shorter shadow range and a darker sky-independent fog.
- Corrected named-objective progress so unrelated breaks never appear as knee progress,
  and Core Shot stays incomplete until the reactor core itself breaks.
- Corrected hints that implied damage persists across a retry.
- Fixed the dummy-audio shutdown leak by configuring the imported stream in place and
  skipping playback when Godot reports the Dummy driver.

### Changed

- Authored arcade palette, sRGB→linear conversion, sky/fog/sun/tonemap/exposure and
  distinct statue palettes (silver/blue/gold knight, warm red gnome, mint/navy robot).
- Smaller, dimmer bounded dust; added capped sparks, chips and short flashes; weak
  points, resisted hits and collapses now read differently.
- Side impact cinematic with a one-frame snap; camera clamping and recovery retained;
  C still skips the close-up.
- Intact major statue parts are solid world obstacles with authored hit volumes; the
  knight shield is a non-blocking ornament so the low rally route can reach both knees.
  Broken parts move to the debris layer.
- New HUD hierarchy, named/total progress bars, speed/gear panel, contextual status,
  720p and 1600p layouts, improved garage button states and a controls/help modal.
- Recommended ramp per challenge; selecting a challenge sets the suggested angle.

### Verified

- Static validation: **1522 checks, 0 failures**; bundle: 8 scenes, 34 GLBs.
- Authored NORMALs: **34/34**; authored palette: **34/34**.
- Release build with warnings as errors: 0 errors.
- Pure .NET tests: **33 passed**; Godot runtime tests: **145 passed**.
- All nine challenges physically completed through ordinary vehicle controls.
- 60-second wall-clock boot: `ok=True`; no leaked audio resources.
- Rendered 1600×900 probe: **55–60 FPS**, 402 nodes, ~810 draws (High); **60 FPS**,
  402 nodes, ~323 draws (Performance). Screenshot capture runs are lower.
- Linux export built, packaged and booted after extraction; Windows export built
  (not run on this Linux host).

### Unverified / limitations

- Audible audio, because this host has no usable audio device.
- Windows runtime execution.
- Human child playtest and physical gamepad test.

## v0.1.0 — First Playable — 2026-11-12

First complete, playable release of the Godot 4.7.2 .NET / C# 10 game.

### Gameplay

- Arcade rigid-body driving for the rally car, pickup truck and monster truck,
  including reverse, braking, lateral grip, tyre upgrades, engine upgrades,
  anti-flip recovery and airborne control.
- Adjustable 10°–27° physical ramp whose invisible collider matches the authored
  deck, and a flush ramp foot for all three vehicles.
- Authored structural destruction for the garden gnome, iron knight and reactor
  robot. Supports lean and collapse, ornaments and weak points detach
  independently, and the robot's chest armour protects its core until peeled.
- Nine authored campaign challenges with medals, impact score, first-clear Wreck
  Coins and replayable objectives.
- Free Smash mode for unlimited experimentation without campaign rewards.
- Vehicle unlock, engine/tyre upgrades and paint cosmetics.
- Chase, wide, hood and mouse-orbit cameras; impact close-up and slow motion.
- Authored dust/debris effects, crash/launch/reward audio and a looping engine
  sample with speed/throttle pitch.
- High and performance quality presets plus master volume control.
- Keyboard, mouse and gamepad input mappings.

### Persistence and reliability

- Versioned save at `user://ramp_wreckers_save.json`.
- Atomic temp-file replacement, validated backup recovery and honest save-failure
  feedback.
- Save data protects starter vehicles and challenge rewards from corruption.
- Bounded debris and reset state.

### Verification

- Static project contract: 612 checks, 0 failures.
- Release build: 0 warnings, 0 errors.
- Pure .NET tests: 27 passed.
- Godot headless runtime tests: 90 passed.
- 60-second headless boot: no gameplay/script errors.
- Linux release exported and booted; Windows release exported.
- Rendered performance probe: 59–60 FPS at 1600×900, high quality.
- Rendered evidence committed under `artifacts/screenshots/`.

### Known verification gaps

- Audible audio could not be heard on the build host (no usable audio device);
  the WAV files and players were loaded successfully.
- Windows startup was not executed on the Linux build host.
- No human child playtest or physical gamepad test was performed.
