# Ramp Wreckers — first playable design specification

## High concept

Pick from distinctive cars and trucks, steer down a runway, use an adjustable stunt ramp, and crash into enormous destructive statues. Impact **location, vehicle momentum and structure** determine the collapse. Runs last ~30–90 seconds with instant reset.

**Audience:** age 8–14, tuned for a ten-year-old. **Player experience:** immediate fun with optional problem-solving depth. **Visual style:** stylised, low-poly, painted-metal vehicles, sculpted oversized statues, readable high-contrast details; no photorealistic gore, police chase or combat.

## Critical design values

1. A properly controlled vehicle is a toy worth driving in its own right. Do not automate the crash.
2. Destruction is a consequence of physics and connected structure, not just reducing a damage bar.
3. A weak-point strike beats blunt force; a heavier truck can win by momentum.
4. Every failure permits **R = instant retry** without repeating menus.
5. The player is always allowed to experiment in Free Smash.

## First-release levels

| # | Name | Target | Required action |
|---|---|---|---|
| 1 | First Crash | Gnome | Break any two parts |
| 2 | Hat Trick | Gnome | Knock off the hat |
| 3 | Gnome Down | Gnome | Break four components |
| 4 | Shield Breaker | Knight | Detach the shield |
| 5 | Knee Trouble | Knight | Break both legs |
| 6 | Knightfall | Knight | Break five components |
| 7 | Peel the Armour | Robot | Detach the chest armour |
| 8 | Core Shot | Robot | Break the core, with the chest protection meaningful |
| 9 | Robot Wreck | Robot | Break six components |

These are initial goals, not fixed difficulty numbers. Make sure every goal is feasible in the implemented physics, preferably with more than one vehicle/ramp combination. Tune ramp range and collision geometry to make elevated hits possible.

## Vehicle roles

| Vehicle | Role | Typical outcome |
|---|---|---|
| Rally Car | Fastest/least mass | Long/high jumps, precision hits |
| Pickup Truck | Balanced and forgiving | Good for introductory objectives |
| Monster Truck | Slowest/highest mass/large tyres | Shoves foundations and traverses wreckage |

The included models are based on these differences. The agent must make the vehicle *handling* reflect the role, not just set unique colours or names.

## Game modes

- **Campaign:** nine short challenges, medalled completion, coin rewards and permanent unlocks.
- **Free Smash:** open attempt mode using unlocked gear; no attempt counter, all available statues, experimentation encouraged.
- **Not in v0.1:** open-world driving, race opponents, destructible city, online play, user-uploaded models or enormous garage.

## Controls and UX

- W/S or arrows accelerate and reverse; A/D or arrows steer; Space brakes.
- Right-mouse drag orbits the chase camera; C cycles chase/wide/hood and skips the
  first-break close-up. R resets instantly, including during slow motion.
- 1–3 select vehicles. J/K select challenges. Q/E adjust ramp angle. F toggles Free Smash.
  G/Tab opens the garage. B/V adjust quality and volume. A gamepad mapping is included.
- Large, simple HUD: goal, challenge number, progress, speed, coins, crash result and
  repeat hint.
- Audio: engine note changes with throttle, short crash and reward sounds, no long unskippable victory sequence.

## Ramp engineering

The included ramp is **about 6.7 m wide and 12 m long**, based at the runway end. It pivots upward at the near end. Start with adjustable pitch 10–27 degrees. In the full version, optionally allow a small left/right offset and forward position through an intuitive UI, but don't make adjustments tedious.

The ramp physics collider must align with its rendered surface. Test that small-wheel vehicles drive smoothly onto the ramp and that a normal full-throttle approach produces a controllable jump into the statue.

## Destruction model

Each statue is assembled from separate authored GLB components. Distinguish:

- **Supports:** legs/feet, whose detachment destabilises the connected body.
- **Load-bearing centre:** torso, which supports arms and head.
- **Optional protection:** knight shield, robot chest armour.
- **High-value targets:** hat, knight head, robot reactor core.

A credible collision rule uses speed, mass, hit normal, local hit point, durability and support links. Weak-point detachments yield strong visual feedback. Avoid identical whole-statue explosions regardless of where hit.

## Rewards, progression, persistence

Coins earn vehicle upgrades, paint colours and optional unlocks; no purchase interaction beyond game currency. Award rewards **once per challenge completion** rather than per repeated reset, and never reward an accidental collider overlap. Save with versioned data and recovery paths. All campaign goals remain replayable.

## Performance and scope

Aim for stable play on a mid-range laptop with integrated graphics. Limit simultaneous dynamic debris and particle lifetimes. Use compact authored low-poly assets. One complete arena polished thoroughly is better than five unfinished areas.
