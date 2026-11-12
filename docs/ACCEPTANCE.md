# Release v0.1.0 — acceptance gates

**Do not mark an item complete based on reading code. Execute it in Godot, by automated fixture or manual play, and retain evidence.**

## Gate 0 — project validity

- [ ] Godot 4.7.2 .NET loads the project with no red broken resources.
- [ ] `dotnet restore` and `dotnet build -c Release` pass with 0 errors.
- [ ] Godot headless editor import exits 0 and prints no resource parse/import errors.
- [ ] Main scene runs with no fatal or repeated script errors for 60 seconds.
- [ ] Every vehicle scene opens and displays its authored GLB mesh.
- [ ] Every statue scene opens with correct model materials, location, origin and collider bounds.
- [ ] Automated static check: no visible built-in primitive meshes, `CSG*`, `ArrayMesh`, `ImmediateMesh`, or mesh-generation calls anywhere in shipping game code/scenes.

## Gate 1 — playable fundamentals

- [ ] From a fresh game, the pickup starts aligned with the runway, moves with W, steers with A/D and stops on Space.
- [ ] Sustained forward drive goes **up** the visible ramp (rather than through or under it), launches, and reaches the gnome.
- [ ] From ordinary playable inputs, each of the 3 vehicles can perform a useful statue strike without debug teleport/forced impacts.
- [ ] Vehicle velocity/steering/grip/weight differences have visible and measurable effects; car does not permanently flip at ramp foot.
- [ ] Ramp angle Q/E changes both visible ramp and invisible collider together; at every permitted angle vehicle entry remains smooth.
- [ ] Camera frames the vehicle and upcoming statue from start to impact; no sustained clipping inside geometry.
- [ ] R resets within 1 second and restores the statue to its exact intact position; repeat this 30 times.
- [ ] Repeated low-speed statue contact does not award damage; a solid fast impact can detach a part.

## Gate 2 — structural destruction

- [ ] Detaching left/right supports has meaningfully distinct behaviour; removing both causes a believable main-body collapse.
- [ ] Gnome hat, knight shield, robot armour and robot core are independently targetable and visibly detach.
- [ ] Robot armour meaningfully protects the core or forces a difficult alternate strike.
- [ ] Unbroken components do not randomly detach at the start of a stage.
- [ ] Components do not fly away at infinite speed or tunnel persistently through the floor.
- [ ] Damage does not double-count after a single hit or across callbacks.
- [ ] Debris and physics performance remain bounded after 20 consecutive retries.

## Gate 3 — 9 challenges + Free Smash

- [ ] Every one of the nine challenge objectives can be completed in ordinary play and records success once.
- [ ] Each challenge has clearly visible instructions and a meaningful progress indicator.
- [ ] The system assigns medals/stars in a consistent way and can replay completed challenges.
- [ ] Wreck Coins are rewarded exactly once per achievement, not every retry.
- [ ] Vehicle unlock or upgrade changes gameplay; cosmetics are optional.
- [ ] Free Smash can be started without finishing a campaign and cannot corrupt campaign rewards.
- [ ] Loading a save restores unlocked equipment and challenge progress.
- [ ] Corrupted or partially written saves recover sensibly; the game never claims a failed save succeeded.

## Gate 4 — shipping

- [ ] All original audio cues work, no missing fonts/textures/models, sensible volume.
- [ ] Gameplay can be fully controlled on keyboard and mouse with on-screen hints.
- [ ] Screenshot evidence shows every vehicle and target statue, ramp, HUD and mid-destruction state.
- [ ] Linux export runs. Windows export is provided if toolchain/template availability permits, with limitation explained otherwise.
- [ ] A final report includes exact tool versions, tests, pass counts, known issues, file or release paths and commit SHA.

## Deterministic tests to create

1. `ChallengeCatalog`: every required part ID exists in its specific statue asset and every award is positive.
2. Impact scorer: below threshold -> no break; above threshold -> exactly one break; repeated contact -> no duplicate reward.
3. Support graph: one/two support detachments, torso collapse, head/arm dependencies, armour/core ordering.
4. Transform regression: ramp at 10°, 16°, 27° retains a collision plane matching the visible deck.
5. Reset: 30 cycles return every part to its original position, collision state and reward state.
6. Save: roundtrip/version/corrupt JSON/empty file/write failure with honest feedback.
7. Performance: 20 crashes/retries with bounded debris and no unbounded allocations or script errors.
8. End-to-end: automated controlled route from spawn, across ramp, into representative target. Verify the physical vehicle actually travels there without teleport.

The handoff contains **no runtime test results**. These checks must be performed by the implementing agent.
