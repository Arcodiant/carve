---
description: >-
  render and check take target weights, applied before skinning, and check reports a target that flips, collapses or cracks faces at weight 1
---
# T-103 — Render and check at target weights

**Status:** Open

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-102

**Verify:** server-unit, server-regression

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
