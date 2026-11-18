---
description: >-
  render and check take target weights, applied before skinning, and check reports a target that flips, collapses or cracks faces at weight 1
---
# T-103 — Render and check at target weights

**Status:** Done

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-102

**Verify:** server-unit, server-regression, scenario-morph

## Goal

An agent sees a mesh at any weights, posed or at rest, in every look. `check` tells it when a target breaks the
mesh before it ever reaches a game.

## Scope

- **`render`** takes `targets`: an object of name to weight, applied to every mesh that has that name (T-99's
  rule). It works with `pose`, `clip`/`time`, `views` sheets and every look (wireframe, normals, weights, bones,
  labels, highlight, checker, UV layout). The morph comes before skinning. Framing stays on the rest bounds, as
  for a pose (D-6). Normals are worked out from the morphed positions by `Shading`. Refusals: a name no mesh has
  (list the names), a weight outside 0 to 1 or not finite. With no `targets`, every existing frame stays
  byte-identical.
- **`check`** takes the same `targets` and checks the mesh at those weights. With or without it, `check` also
  checks **every target at weight 1** against the rest mesh and reports:
  - `target-flips-face`: a face whose normal at weight 1 has a negative dot product with its rest normal;
  - `target-collapses-face`: a face zero-area at weight 1 by `PolygonMesh.ZeroAreaTolerance` that is not
    zero-area at rest;
  - `target-opens-mesh`: two vertices that are `coincident-vertices` at rest (check's existing rule) and apart at
    weight 1.

  Each names the target and the first ids, folded at 10 as M-19's T-72 folds not-planar warnings.
- **Placeholder content**: create `scenarios/Carve.Scenario.Morph` (suite `scenario-morph`, joined to
  `Carve.slnx` and `tests.json`) with a first stage. The stage is the M-18 hard-rimmed 8-sided cylinder, given a
  `Bulge` target that scales its middle ring 1.4×, seen from `+z` at 0, 0.5 and 1, once in the normals look at 1,
  and once with a `Pinch` target that pushes the middle ring through the axis so that `check` reports a flip.
  Every pixel check is worked from the geometry (the silhouette half-height at the middle ring from `get_mesh`'s
  morphed positions, sampled near the corners as well as the middle). T-105 adds the arm as later stages.
- **Tests:** server-unit for `check`'s three rules and the refusals. Render frames through the scenario. Every
  existing frame's hash compared before and after. Each new check shown failing on a broken product: weights
  ignored, morph after skin, a mirrored camera. Record the table.
- **Frame review is its own agent**: a fresh agent opens each frame itself (cropped or scaled) and then sends
  them to the person with `mcp__waystone__review`. "Commented" is not approval.

## Notes

- `scenario-morph` does not exist until T-103 adds it to `tests.json`. Add it to this item's `**Verify:**` line once it is there (`cairn lint` refuses an unknown suite before then).
- Owns `RenderTool.cs`, `CheckTool.cs`, `MeshHealth.cs`, `tests.json` and `Carve.slnx` in its wave. T-104 runs
  beside it in other files; a conflict that only adds is settled by keeping both sides.
- Evidence: `~\artifacts\carve\M-21\T-103\` (absolute; `artifacts/` is ignored and `finish` does
  not carry it).
- **Goes back to the person, stop and ask:** the 0 to 1 weight range (the planner's default, not yet the
  person's ruling); any threshold in the three rules beyond those named; any existing frame that changes.
- Look-heavy: plan two agents, the implementer and the review agent.

### As built

Built by Claude Sonnet 5.5 (the implementer). The frames are judged by a separate review agent (see Frame review below).

- **`render` and `check` take `targets`**: an object of target name to weight (`src/Carve.Server/TargetWeightsArgument.cs`
  reads it; `TargetWeights.cs` is the value the frame thread compares). Each mesh with a target of that name is morphed,
  before the pose or clip skins it: `RenderPump.ShowMesh` calls `Skinning.Posed(model, pose, weights)` (T-99), and with no pose
  `Skinning.Morphed` (made public) alone, so a morph with no pose does not skin the rest. A change of weights puts the mesh in
  the scene again, as a change of pose does. Framing stays on the rest bounds (`ShowMesh(posed, drawn)`). `targets: {}` is no
  targets. Not with `mode: "uvs"` (refused with the other arguments the layout cannot use). The caption is unchanged.
- **The weight range, the person's ruling of 2026-11-18**: `render` and `check` refuse a weight outside [0, 1] or not finite,
  naming the target (`targets.Bulge: weight 1.5 is outside 0 to 1`). Carve stores no range, and a game drives the exported
  morphs however it likes. Also refused: a name no mesh has (`no mesh has a target named 'X'; the targets are 'A', 'B'`, or
  that the model has no targets) and a value that is not a number.
- **`check`** (`MeshHealth.Create(mesh, materials, rest)`): for each target of the mesh, at weight 1 (its other targets at 0)
  against the rest mesh, one problem per target and rule that finds anything, element `targets['Pinch']`, ids folded at
  10 (`faces[8], ..., and 2 more`):
  - `target-flips-face`: dot product of the Newell normals, rest against weight 1, below 0;
  - `target-collapses-face`: zero-area at weight 1 by `PolygonMesh.HasArea` (`ZeroAreaTolerance` 1e-9 x the face's
    bounds diagonal squared) and not zero-area at rest (a face that collapses is not also a flip);
  - `target-opens-mesh`: two vertices the rest mesh's `coincident-vertices` rule pairs (exact floats), that differ at weight 1;
    each reported as `vertices[4] from vertices[1]`.
  With `targets`, the stats and every other check are of the mesh morphed to those weights; the target rules still compare weight 1
  with the rest mesh. No threshold beyond those named. Problems make `ok` false.
- **Placeholder content**: `scenarios/Carve.Scenario.Morph` (suite `scenario-morph`, in `Carve.slnx` and `tests.json`, added to
  this member's Verify). The M-18 cylinder (8 sides, 3 rings, both rims and one side edge hard, smooth) **made 2 m tall**
  (rings at y 0, 1, 2, radius 0.5): at 1 m tall the framing, which stays on the rest bounds, holds a half-width of 0.595, so
  Bulge's 0.7 would run out of the frame. `Bulge` scales the middle ring 1.4x. Frames from +z (orthographic): rest, Bulge at 0,
  0.5 and 1, Bulge at 1 and at rest in the normals look, a two-view wireframe sheet at 1, a pose (Top bent 45 degrees about z) with
  and without Bulge at 1 and with Bulge at 0, and Pinch at 1 shaded and in normals (12 frames). Pixel checks are worked from
  `get_mesh`'s morphed positions: the silhouette's half-width at the middle ring and at heights 0.5, 1.5 and 0.08 and 1.92 (near the
  rim corners), 0.03 m inside drawn and, in the shaded frames, 0.03 m outside background. Bulge at 0 equals the rest frame
  byte for byte, and so does the pose with Bulge at 0.
  **Pinch is not a radial push through the axis**: a radial move, even through the axis, cannot turn a face (the Newell normal keeps its
  outward part; computed by hand and shown by `check` finding nothing on that first try). Pinch pulls the middle ring in to half
  and lifts it to y 2.4, above the top rim, so the eight faces between it and the rim turn: `check` names
  `targets['Pinch']`, 8 faces (faces 8 to 15). T-105's arm adds its own stage.
- **Tests**: `TargetWeightsTests` (server-unit, 13 cases): each rule on its own mesh, the collapse not also a flip, a seam kept
  shut by equal deltas, the fold at 10 (12 faces, "and 2 more"), `check` at weights against rest, no targets means as before,
  and the refusals (unknown name, 1.5, -0.25, a string, 1e999, an array, no targets, the uvs look) for both tools;
  `RenderPumpTests` draws the morphed mesh and the rest again. Two existing definition tests gained the `targets` argument
  (`render`'s schema list, `check`'s schema and unknown-argument message). Core unit 1060 and server unit 777 pass.
- **Broken products shown** (each fails the scenario or a test, restored afterwards):

  | Broken | Caught by |
  |---|---|
  | weights ignored (`render` posts no targets) | scenario: `morph-bulge-half-z: the surface should be drawn at x -0.57, y 1 ..., the silhouette's half-width being 0.6` |
  | a rule left out of `check` (each of flips, collapses, opens) | the matching `TargetWeightsTests` case (the rules are separate cases, none overlaps) |
  | morph after skin | **not shown here**: the pump calls T-99's `Skinning.Posed(model, pose, weights)`, whose order T-99's Core tests pin; the posed frames here only differ from the unposed ones, and are for the review. T-105's arm bends with Bulge at 1 and judges it |
  | a mirrored camera | **not shown**: the cylinder is its own mirror image from +z, so a mirror draws the same frame. T-105's arm is asymmetric |

- **Existing frames**: scenario frame hashes of the untouched code (HEAD, a throwaway worktree under `artifacts/baseline`) against
  this change: 13 scenarios (cube, crate, rig, soft, materials, uvs, cuts, shed, character, gatehouse, orcward, sky, export), 419 frames, each scenario's frame set hashed by file: **every one identical**. With no `targets` the pump takes the old path (the morph argument is null). The scenarios not run: acceptance, and the ones that need another process.
- Engine needs: none. Files outside the worktree: none.
- **Frame review**: a separate review agent (Claude Sonnet 5.5) opened the 10 distinct frames (the 12 hold two pairs of byte-equal files) and found each as the spec and As built say; then sent all 12 to the person with `mcp__waystone__review` (copied to `G:/My Drive/artifacts/carve/M-21/T-103/round-1`). **Verdict: approved**, including the two deviations (a 2 m cylinder rather than 1 m; Pinch reshaped so it can flip faces). The Pinch frames run off the top of the frame because framing stays on the rest bounds, as designed.
- Not checked: morph-after-skin and a mirrored camera (the cylinder cannot show either; T-105's arm does).
