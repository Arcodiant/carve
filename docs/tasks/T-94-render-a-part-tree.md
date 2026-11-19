---
description: >-
  render draws every part where it is placed, isolates a subtree with part and draws a kept level with level, and get_mesh gives a part's placement
---
# T-94 — Render a part tree

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-93

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-cuts, scenario-gatehouse, scenario-uvs, scenario-materials, scenario-character, scenario-soft, scenario-rig, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees a part tree as the game will: every part at its placement, instances included. It can see one subtree on its own and the model at a kept level, as the person ruled at M-9's planning (2026-11-17).

## Scope

- **`render`** draws `Model.Combined` (T-91, world-placed) by default, so a model of untransformed top-level parts draws as today, byte for byte.
- **`part`** draws only that part's subtree, at world placement, framed on that subtree's world bounds. This replaces the framing that M-7 found frames on every mesh when `mesh` names one, but only when `part` is given: `mesh`'s framing is unchanged, so existing frames hold. An instance's subtree draws its source's geometry.
- **`level`** draws `CombinedAt(level)`: each part at its newest kept level ≤ N, and a part with none is not drawn. With `part`, the two combine. The caption names both: `part Axle_F, level 0`.
- **`target`** and `direction` are in model space (the world placement). The text says so where it said "the mesh's own coordinates".
- **`labels` and `highlight` with `mesh`** show the named part's own ids on its own occurrence, not on its instances.
- **`get_mesh`** gains the part's `parent` and its world transform, and takes `level` (the newest kept level ≤ N, naming which level was given). Coordinates stay part-local.
- **Content: the rover's look in the server tests.** It is built from `RoverSpec` through the tools. Frames: rover front at live and at level 0; `part: Axle_F` from `[1, 0.3, 0]`, showing both hub bosses outward; and `part: Wheel_FL, labels: faces`. Each frame has pixel checks worked from the geometry with the shared rules named below, including one a mirrored or wrong-up camera fails, worked from the framing's bounds (the rover is asymmetric: the cab at +X, the mast at −X). Plan the frame review as its own agent. That agent opens every frame itself, cropped or scaled, before it asks the person.
- **Tests:** the frames above (`regression`, they need the device); in-process `unit` tests of the argument rules (`part` unknown, `level` negative, `part` with `mesh` naming a part outside the subtree), and of `get_mesh`'s new fields.

## Notes

- **Shared check rules, named at planning** (M-5 and M-6 lessons): the Crate's `ColourRule.cs` for a face's shade; the Cuts ray test (`CutsGeometry.cs`) run on `Combined`, which is already world-placed, so it needs no part logic; `SeenNarrow` for faces a few pixels wide (wheel rims, the mast post); `Drawn` for edges over the background; the gatehouse's 3D in-sight test for occlusion. A new rule goes to the person before it lands.
- **The guard:** every existing frame byte-identical by hash before and after, taken from queued jobs fetched by job id (not `cairn test artifacts <suite>`'s latest, the M-4 and M-16 trap).
- **Opus**, with the frame review planned as a second agent (M-6 and M-7 lessons).
- **File ownership:** `RenderTool.cs`, `RenderSystem.cs`, `ViewFraming.cs`, `LookMeshes.cs`, `GetMeshTool.cs`. M-19 also changed `RenderTool` and `RenderSystem` (`render_glb`). This task starts after M-19 has landed and builds on it.
- **Open rules: stop and ask.** Whether `part` with `views` frames every tile on the subtree (recommendation: yes); any change to an existing frame or caption; a pixel check that cannot pass as specified.
- **Write down for dependents (As built):** the captions and the shared rules as used, for T-96.
- **Carried:** each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names; `finish` run inside the worktree, its output read whole and its `Merge task/T-94` line given; report a `verify` that waits more than a few minutes; submit the GPU suites early and keep working rather than hand off at "a real run still to come" (M-5).
