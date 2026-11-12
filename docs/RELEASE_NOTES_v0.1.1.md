## Ramp Wreckers v0.1.1 — Visual and Gameplay Polish

A visual and playability polish pass over the published v0.1.0 game. The nine
challenges, three vehicles, three statues, one arena, adjustable ramp, Free Smash,
garage and save format are retained.

### Rendering fix (the big one)

The severe concentric/ripple pattern on the runway, grandstands, ramps and vehicles
came from supplied GLBs that had **no authored NORMAL attributes**. Lighting and shadow
terms were evaluated without valid surface normals. The fix is offline and asset-level:
`tools/add_normals_to_glb.py` adds authored smooth-angle normals to all 34 runtime GLBs
without changing positions, triangles, transforms or materials. The GL Compatibility
renderer now renders clean surfaces at both quality presets. See
`artifacts/screenshots/p0_before_after.png`.

### Visuals

- Authored arcade palette with sRGB→linear conversion, distinct vehicle colours and a
  silver/royal-blue/gold Iron Knight, warm red gnome and mint/navy reactor robot.
- Sky, fog, sun, ambient, tonemap, exposure and shadow bias retuned for readable
  separation between roadway, ramp, vehicles, statues and sky.
- High preset: shadows, fog, 4× MSAA. Performance preset: no sun shadow/fog/MSAA.
- Smaller, dimmer, shorter dust with sparks, chips and a short flash; strict caps and
  cleanup for every temporary effect.
- Side-on impact cinematic with a one-frame camera snap so the hit and statue are
  visible; C still skips it. Camera clamp/recovery and reduced shake remain.

### Collision and destruction

- Intact major statue components are solid world obstacles, so the car no longer looks
  like it is passing through an intangible model. Authored `Area3D` hit volumes still
  drive the arcade break; broken parts become bounded rigid-body debris.
- The knight shield is an authored non-blocking ornament hit volume so a low rally
  route can reach both knees instead of being stopped by a thin decoration.
- Stronger contact slowdown/rebound, per-vehicle impact throttling, authored four-knee
  grouping, robot armour→core follow-through, and clear weak-point/collapse feedback.

### HUD and progress

- New kid-readable layout: objective/challenge/goal/required progress/total progress/
  status at top-left; coins/medal/ramp at top-right; speed and gear bottom-right;
  contextual hints below the objective.
- `ObjectiveProgress` model separates named `RequiredParts` from
  `MinimumBrokenParts`. Knee Trouble now shows `BOTH KNEES 0 / 2` (not unrelated broken
  pieces); Core Shot shows `REACTOR CORE 0 / 1` until the core actually breaks.
- Garage retains vehicles, upgrades, paint, quality and volume with clearer selected,
  locked, owned and buy states; 1600×900 and 1280×720 layouts; H help modal.

### Verification

- Static project contract: **1522 checks, 0 failures**.
- Authored GLB normals: **34 / 34**; palette: **34 / 34**.
- Release build with warnings as errors: 0 errors.
- Pure .NET tests: **33 passed**. Godot runtime tests: **145 passed**.
- All nine challenges completed through ordinary driving in the runtime suite.
- 60-second wall-clock headless boot: `ok=True`; no leaked audio resources.
- Rendered performance at 1600×900: **55–60 FPS**, 402 nodes, ~810 draws (High);
  **60 FPS**, 402 nodes, ~323 draws (Performance). Screenshot captures report 35–53 FPS.
- Linux export built and booted; Windows export built (not run on this Linux host).

### Known limitations

- Audible audio was not verified: the build host has no PulseAudio/ALSA device, so Godot
  used its dummy driver. Assets and wiring were tested.
- The Windows binary was not executed on this Linux host.
- No human child playtest or physical gamepad test was possible.
- No save migration path yet (version 1 remains compatible).
