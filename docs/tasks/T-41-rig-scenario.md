---
description: >-
  A scenario finds planted skinning faults through get_weights and the bones and weights looks, in its own suite
---
# T-41 — Rig scenario

**Status:** Done

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-38, T-40

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Skinning faults are proved visible before export.

## Scope

- **`scenarios/Carve.Scenario.Rig`**, suite `scenario-rig`, on the gardener with planted faults: an unweighted patch, a hat vertex on the wrong arm, a bone out of place.
- Each fault is found through `get_weights` or a frame with `bones` or `weights`, then fixed through the tools, and the frame re-checked.
- `export` is refused while the unweighted patch remains, and succeeds once it is fixed.
- Frames are kept and looked at, with a pixel check for each fault before and after.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5), one agent.

- **Scenario** `scenarios/Carve.Scenario.Rig/RigScenario.cs`, suite `scenario-rig` (`tests.json`, `Carve.slnx`, CLAUDE.md layout), run by `run-scenario.ps1 rig`; no check project (the export is asserted in the scenario). `GardenerSpec.cs`, `CrateBuild.cs` and `CameraPixels.cs` are linked, not copied. The gardener is built with his hand weights less the planted faults:
  1. **Unweighted patch**: Body vertices 26-28 and 34-36 (torso rings at 0.90 and 1.02, front-left). Found by `get_weights` (`summary.unweightedVertices`) and the `weights: Spine_03` frame (magenta); `export` refused ("6 vertices have no weight", nothing written); fixed with `set_weights`, frame red.
  2. **Hat vertex 11 on Shoulder_L**: found by `get_weights bone Shoulder_L` on the Hat (one follower) and the `weights: Shoulder_L` hat close (red on the brim, blue elsewhere); fixed to Head, no followers, frame blue.
  3. **Elbow_L out of place** (0.3 m too high): found in the `bones` frame (magenta dot at (0.30, 1.10), none in the sleeve); fixed by `define_skeleton` with the same names and order; frame re-taken. Bones and weights are separate frames (both magenta).
  Then `export` succeeds (22 bones, nothing unweighted).
  4. **The fault `weigh` makes**: `weigh replace` on Body; `get_weights bone Elbow_R` lists torso vertices 19 and 20 (and others) as followers; the `weights: Elbow_R` frame shows the torso face between rings 0.62 and 0.76 not blue. Fixed with `define_group torso` (faces 8-31) and `set_weights group` Spine_02; read back with `get_weights` (vertices 8-39 Spine_02 wholly, no torso follower of either arm bone); frame blue. A plain `weigh` then reports 0 weighed, 320 kept, reads back the same, and its frame is byte-identical to the one before it. Done as described; the group is the torso's own faces, so the override also moves its shoulder-ring vertices (and the Shoulder bones' torso followers) to the spine.
- **Pixel checks**: each frame before and after, positive on the colour at a point worked from the geometry, plus its mirror (patch: the weighted face at +X is red; hat: the brim's +X mirror is blue; torso face at +X is blue; bones: nothing at the planted place's x mirror), and a boot reads blue (a y-flip puts the patch there). Limit: the fixed bones frame cannot tell a mirrored camera (Elbow_R is at the mirror); the planted frame does.
- **Byte-identical guard**: none to apply, the scenario is new and changes no tool or other scenario's frames.
- **Mutations** (local runs, restored):

| Mutation | First check that failed |
|---|---|
| patch fix leaves one vertex out | rig-patch-after: the torso face inside the patch reads (255,0,172), expected red |
| hat vertex left on Shoulder_L | get_weights bone Shoulder_L after the fix: still followed |
| skeleton not fixed | rig-bones-after: a bone where it was planted |
| override also weights Elbow_R | get_weights after set_weights: vertex 8 not Spine_02 wholly |
| camera mirrored in x (front) | rig-patch-before: the weighted face at the patch's mirror reads background |
| camera flipped in y (front) | rig-patch-before: the patch face reads blue, expected magenta |
| hat camera mirrored in x | rig-hat-before: brim near the stray vertex reads blue, expected red |

- **Review**: round-1 (`C:/Users/andre/artifacts/carve/M-17/T-41/round-1`, the eight before/after frames) approved by the person.
- **Not checked**: posed frames of the faults; clips are not defined in this scenario. Engine needs: none.
