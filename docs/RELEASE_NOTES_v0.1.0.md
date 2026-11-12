## Ramp Wreckers v0.1.0 — First Playable

The first complete, playable release of Ramp Wreckers: a model-driven 3D
stunt/destruction game for Godot 4.7.2 .NET, C# and .NET 10.

Pick the rally car, pickup or monster truck; drive down the arena runway; raise
the ramp; and smash the garden gnome, iron knight or reactor robot into their
individually modelled components. Nine authored campaign challenges and a Free
Smash mode are included. No accounts, telemetry, purchases or online features.

## Highlights

- **Model-driven world.** Every visible object is an authored GLB asset
  instanced in editable Godot scenes. No visible primitive meshes, CSG geometry
  or runtime mesh generation.
- **Physical ramp and three distinct vehicles.** The 10°–27° ramp collider tracks
  the authored deck. Rally car, pickup and monster truck have different speed,
  mass, grip and wheel sizes, plus permanent engine/tyre upgrades.
- **Real structural destruction.** Statues are separate authored parts with
  support and protection links. One leg gone leans the statue; both gone topples
  the upper body. The gnome hat, knight shield and robot core are independent
  targets, and the robot's chest armour protects its core until peeled.
- **Nine challenges + Free Smash.** Medals, impact score, first-clear Wreck
  Coins, unlocks, paint and replayable objectives. Free Smash cannot grant or
  corrupt campaign rewards.
- **Persistence you can trust.** Versioned save, atomic temp-file replacement,
  validated backup recovery and honest save-failure feedback.
- **Polished arcade presentation.** Chase/wide/hood/orbit cameras, debris-aware
  framing, speedometer, objective progress, dust/chips, crash/launch/reward
  audio, looping engine note, slow-motion weak-point payoff, keyboard/mouse and
  gamepad mappings, high/performance quality presets.

## Downloads

- **Linux x64:** [`RampWreckers-v0.1.0-linux-x64.tar.gz`](https://github.com/PhilipGB/ramp-wreckers/releases/download/v0.1.0/RampWreckers-v0.1.0-linux-x64.tar.gz) (63 MB)
- **Windows x64:** [`RampWreckers-v0.1.0-windows-x64.zip`](https://github.com/PhilipGB/ramp-wreckers/releases/download/v0.1.0/RampWreckers-v0.1.0-windows-x64.zip) (73 MB)
- **Checksums:** [`SHA256SUMS.txt`](https://github.com/PhilipGB/ramp-wreckers/releases/download/v0.1.0/SHA256SUMS.txt)

### Run on Linux

```bash
tar -xzf RampWreckers-v0.1.0-linux-x64.tar.gz
cd RampWreckers-v0.1.0-linux-x64
chmod +x RampWreckers.x86_64
./RampWreckers.x86_64
```

### Run on Windows

Extract the ZIP and run `RampWreckers.exe`. The Godot runtime, .NET runtime,
game assembly and PCK are embedded in the executable.

### Controls

`W/S` drive, `A/D` steer, `Space` brake, `R` instant retry, `1–3` vehicles,
`J/K` challenges, `Q/E` ramp, `F` Free Smash, `C` camera, right-mouse drag orbit,
`G`/`Tab` garage, `B`/`V` quality/volume, `P` paint.

## Verification

- Static project contract: **612 checks, 0 failures**.
- Release build: **0 warnings, 0 errors**.
- Pure .NET tests: **27 passed, 0 failed**.
- Godot headless runtime acceptance tests: **90 passed, 0 failed**.
- Headless import: exit 0; 60-second gameplay boot: no gameplay/script errors.
- Physics-driven route passed for all three vehicles over the ramp into the gnome.
- All nine objectives completable from real part breaks; save corruption, backup
  recovery, write failure, duplicate rewards, Free Smash isolation and 30 reset
  cycles covered.
- Linux release exported and booted; the packaged Linux archive was extracted,
  booted again and passed.
- Rendered performance probe: **59–60 FPS**, 387 object nodes, 236 draw calls at
  1600×900 on high quality (AMD Radeon 890M).
- Rendered evidence committed in `artifacts/screenshots/`; logs in
  `artifacts/test-logs/`.

## Known verification gaps

- **Audible audio is NOT VERIFIED.** The build host had no usable PulseAudio/ALSA
  device, so Godot fell back to its dummy driver. All WAV assets and audio players
  load correctly, but a listening pass on a machine with working audio is still needed.
- **Windows startup is NOT VERIFIED.** The Windows executable was exported but
  cannot be run on the Linux build host.
- No human child playtest or physical gamepad test was performed.
- Intact statue parts are deliberate arcade `Area3D` hit volumes; a fast vehicle
  can overlap the statue silhouette for the impact frame before parts become solid
  debris. This enables satisfying smash-through impacts.
- Save schema is version 1 with no future-version migration yet; unknown/corrupt
  saves are refused or recovered safely.

## SHA-256

```text
14b45298090061687504e92a237bf89539ab3dfc227e02d39409893485a6393d  RampWreckers-v0.1.0-linux-x64.tar.gz
c0b06bd695e8d06423569a359abfef81df3b868e0843903c9e426c35dc081d94  RampWreckers-v0.1.0-windows-x64.zip
be33cecf3d2c71d1f50abd7df880a80f1ca4f1985696c944d3c81a414586d060  linux/RampWreckers.x86_64
7abddcfa2156a232b5c386a1d9f802dbced4f7d7ac34eeabbe4a94342ba41d7f  windows/RampWreckers.exe
```
