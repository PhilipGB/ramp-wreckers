# Repository agent instructions

Read `AGENT_HANDOFF.md` before development. The target is a playable Godot 4.7.2 .NET/C# 10 vehicle-destruction game, not a plan or mock-up.

**Core invariant:** zero visible procedural or Godot primitive mesh geometry in the final game. Visible assets are authored and imported as GLB (or equivalent). Invisible collision Shape3D nodes are allowed. Every 3D art asset should have a source file and a mesh asset. Validate Godot scenes and runtime, never replace missing GLB models with generated cubes.

Preserve authored scene editing; don't rebuild the scene tree in C# except for instantiating existing packed scenes. Run and report tests before declaring milestones done. Scope is nine challenges, three vehicles, three statues, one arena and free smash.
