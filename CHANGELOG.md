# Changelog

All notable changes to Ramp Wreckers are recorded here.

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
