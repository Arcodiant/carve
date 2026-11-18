---
description: >-
  samples/arm.carve, an arm with Bulk and Lean targets, built, subdivided, rigged, checked and seen at its weights through the tools in scenario-morph
---
# T-105 — The arm with dials

**Status:** In Progress

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-100, T-101, T-103

**Verify:** core-unit, scenario-morph

## Goal

The milestone's content: an original arm with two dials, built through the tools as fps-arms builds its arms, and
seen in the seven frames the person approved (2026-11-17).

## Scope

The arm, exactly as approved (sketch: M-21's *Decided at planning*):

- **The cage:** a tube along +X, shoulder at x = 0 and wrist at x = 0.6, on the axis y = 0, z = 0. There are 11
  rings at x = 0.00, 0.06, … 0.60, each with 8 sides, the first vertex at angle 0 in the YZ plane, counter-clockwise
  seen from +X. Ring radii are .050 .055 .058 .055 .048 .040 .046 .048 .042 .034 .028, with 8-gon caps. That
  gives 88 vertices, 168 edges and 82 faces.
- **Targets on the cage:**
  - `Bulk`, a sparse `positions` set: rings 1–3 and 6–8 at 1.35× radius, 48 vertices;
  - `Lean`, from `file` `samples/arm-lean.carve`: the same cage with every ring at 0.8× radius. The scenario
    builds that file through the tools first and saves it, the way fps-arms re-runs its cage script.
- **Shading and rig:** `set_shading smooth`, then `subdivide` 1, giving 338 vertices and 336 quads (672
  triangles). Bones Shoulder (0,0,0) > Elbow (0.3,0,0) > Wrist (0.6,0,0). Weights set on the cage before
  subdividing: rings 0–4 Shoulder, ring 5 half and half, rings 6–10 Elbow. Clip `Curl`: Elbow 0° to 90° about
  +Z over 1 s.
- **Planted fault:** target `Bad`, ring 2's three vertices with y > 0 moved to y = -0.08. `check` must report
  `target-flips-face` for it. Take frame 7 in the normals look, then `delete_target Bad`. After that, `check`
  reports no target problem.
- **Frames** (stages after T-103's cylinder):
  1. rest, `+z`;
  2. Bulk 1, `+z`;
  3. Bulk 0.5 + Lean 0.5, `+z`;
  4. Lean 1, `+z`;
  5. Bulk 1, `front`;
  6. `Curl` at t = 1 with Bulk 1, `+z`;
  7. the planted `Bad` at 1, normals look, `+z`.
- **Checks:**
  - First, as `GatehouseGeometry` did, a Core test working the counts and, for each frame's weights, the
    silhouette half-height at x = 0.12 and x = 0.42 from the morphed subdivided positions. Ring factor at ring 2:
    Bulk 1.35, Lean 0.8, mixed 1.075.
  - Every pixel check in the scenario is worked from `get_mesh`'s positions by the scenario's ray test, sampled
    near corners as well as middles. Frame 6 checks the forearm is above the elbow (+Y).
  - Mirrored-camera and weights-ignored breaks each fail a check of their own, and each guard point is worked
    from the framing's bounds.
- **Samples:** `save` gives `samples/arm.carve` (subdivided, rigged, with `Curl`, Bulk and Lean) and
  `samples/arm-lean.carve`, each kept equal by content to what the scenario builds. Reopen, and frames 1–6 must
  be byte-identical.
- **Frame review is its own agent**, as in T-103. Update CLAUDE.md's layout table (the Morph scenario, the two
  samples).

## Notes

- `scenario-morph` does not exist until T-103 adds it to `tests.json`. Add it to this item's `**Verify:**` line once it is there (`cairn lint` refuses an unknown suite before then).
- Evidence: `~\artifacts\carve\M-21\T-105\`.
- Work the numbers through every check before the scenario is written (M-4 and M-7 retrospectives).
- **Goes back to the person, stop and ask:** any change to the agreed arm, targets, rig or frame list; a pixel
  check that cannot pass as specified; any existing frame that changes.
- Look-heavy: plan two agents. Sonnet for the build, with the review agent separate.

### As built

Built by Claude Sonnet 5.5 (the implementer). The frames are judged by a separate review agent (see Hand-off).

- **What T-106 reads.** `samples/arm.carve` (a version 8 file, one mesh `mesh`: 338 vertices, 336 quads, smooth, targets `Bulk` and
  `Lean`, skeleton Shoulder > Elbow > Wrist with the Wrist at x 0.6 in the world (rest positions are local: 0, 0.3, 0.3), clip `Curl`:
  Elbow `rotation` LINEAR from identity at 0 s to 90 degrees about +Z, `[0, 0, 0.70711, 0.70711]`, at 1 s, not looping) and
  `samples/arm-lean.carve` (the cage at 0.8 times every ring, no targets, no skeleton). Weights in the file: rings 0 to 4 Shoulder, ring 5
  half and half, rings 6 to 10 Elbow, set on the cage and blended by subdivide. The weights the frames use: rest; Bulk 1; Bulk .5 with
  Lean .5; Lean 1; Bulk 1 from `front`; Bulk 1 with `Curl` at t = 1. The silhouette half-heights worked in Core (`ArmGeometry`), seen from +Z at x = 0.12 and 0.42:
  rest 0.05306 and 0.04356, Bulk 1 x1.35 (0.07163, 0.05880), mixed x1.075 (0.05704, 0.04683), Lean x0.8 (0.04245, 0.03485). `Bad` is not in the sample.
- **The scenario** (`scenarios/Carve.Scenario.Morph/ArmStages.cs`, `ArmPlan.cs`; suite `scenario-morph`, added to Verify): the arm runs **first**,
  on the empty model, then the cylinder (whose `define_mesh` gained `drop_targets: true`, and the arm's clip `Curl` is deleted at its end so that the cylinder's
  own skeleton can be defined). Reason: there is no way to empty a model (`undo` stops after 6 steps; an empty `define_skeleton` is refused), and the
  samples must hold the arm alone. The cylinder's 12 frames are byte-identical (hashed against the untouched code in a throwaway worktree).
  Order: `define_mesh` Lean's cage, `save` `work/arm-lean.carve`; `define_mesh` the arm's cage; `define_target Bulk` (sparse `positions`, 48) and
  `Lean` (`file`); `set_shading smooth`; skeleton; `set_weights` x3; `define_clip Curl`; `subdivide` 1; `check` with no target problem at rest and at weights;
  frames 1 to 6; `save`, both files compared byte for byte with the committed samples (`<Content>` in the csproj copies them beside the assembly: they are
  exactly what the scenario saves, so a change that alters them fails the run until they are copied again); `open` (with `drop_targets`) and frames 1 to 6 again,
  byte-identical; `define_target Bad` (ring 2's three vertices with y > 0, ids 16, 17, 23, to y -0.08, on the subdivided mesh, where original vertices keep their ids),
  `check` names exactly `target-flips-face` on `targets['Bad']`, frame 7 (normals), `delete_target Bad`, `check` with no target problem; the range refusals again.
- **The numbers first** (`tests/Carve.Core.Tests/ArmGeometry.cs`, 6 tests): counts (88 V, 168 E, 82 F; 338 V, 336 quads, 672 triangles), the ring factors at x 0.12
  and 0.42 (1, 1.35, 1.075, 0.8), `Bad` flips faces and Bulk and Lean break nothing, every silhouette probe of the four side frames sure to the ray test, a mirrored camera disagreeing
  at 28 to 32 of 76 probes per frame, weights ignored disagreeing at 28, 18 and 38 probes (frames 2 to 4), the curl guards, a bend the other way and morph after skin failing them.
- **Pixel checks** (all from `get_mesh`'s positions: the rest, Bulk's and Lean's absolute positions, the Elbow weights from `get_weights`): the four side frames are orthographic
  from +z; 76 silhouette probes each (19 x positions, a point 2 mm inside and 2 mm outside the top and the bottom, sure by the ray test at half a pixel; in the normals look only the
  arm is asserted, as in T-103), plus a sweep of every 8th pixel the ray test is sure of at one pixel (700 to 880 pixels per frame). Frame 5 (corner view, perspective) is the sweep alone.
  Frame 6 holds six hand guards (a forearm above the elbow at (0.30, 0.24) and (0.30, 0.12), none where it lay at rest (0.45, 0), none beside it or below the elbow) and the sweep. Frame 7 holds the silhouette probes the ray
  test is sure of next to the spike (the silhouette there is too steep for a 2 mm margin) and a hand guard 9 mm under the rest silhouette at x 0.12.
  The mixed frame's margin (2 mm) lies inside the 4 mm by which Bulk .5 + Lean .5 exceeds the rest, so the weights-ignored break fails there too.
- **Broken products shown** (each fails the scenario, restored afterwards):

  | Broken | Caught by |
  |---|---|
  | mirrored camera (`CARVE_MORPH_BREAK=mirror`, the side views asked from -z) | `arm-1-rest-z: x 0.03, y 0.0467, inside the top (half-height 0.04866): pixel (62, 222) should show the arm, and is background` |
  | weights ignored (`CARVE_MORPH_BREAK=weights`, no `targets` sent) | `arm-2-bulk-1-z: x 0.03, y 0.0556, inside the top (half-height 0.05758): pixel (62, 216) should show the arm, and is background` |
  | morph after skin (`Skinning.Posed` edited to add the morph's deltas after the turn, then restored) | `arm-6-curl-1-bulk-1-z: pixel (228, 100) by the ray test ... should show the arm, and is background` (a sweep pixel; the hand guards alone do not tell it) |

- **Existing frames**: the cylinder's 12 morph frames are byte-identical to the untouched code's (`sha256` of each file); the other scenarios were not run (no tool or product code changed).
- Engine needs: none. Files outside the worktree: none (evidence: `~\artifacts\carve\M-21\T-105\`).
- Not checked: the frames by eye (the review agent's); `render_glb` of the arm (T-106); the scenario with the whole `server-regression` suite.

### Hand-off

Model: Claude Sonnet 5.5. Code is committed and `cairn task T-105 verify` passed (server-unit, core-unit, scenario-morph); the branch is rebased on origin/milestone/M-21 (T-108 landed, no conflicts) and what is left is the frame review by a separate agent, then `finish`.

- **Frames to judge** (13 files, none opened by the implementer): `/c/Users/andre/artifacts/carve/M-21/T-105/run` (copied from `artifacts/scenarios/morph/4/frames/`): `arm-1-rest-z`, `arm-2-bulk-1-z`, `arm-3-bulk-half-lean-half-z`, `arm-4-lean-1-z`, `arm-5-bulk-1-front`, `arm-6-curl-1-bulk-1-z`, `arm-7-bad-1-normals-z`, and the six `-reopened` copies (byte-equal to 1 to 6, asserted by the scenario). To look for: Bulk thickens upper arm and forearm but not the elbow (rings 4 and 5) or the wrist (ring 9, 10); Lean is thinner all along; the mixed frame between them; the curled forearm stands up from the elbow (+Y) with Bulk's thickness; in frame 7 a spike under the arm at x 0.12 and the normals look; the corner view is the same arm from `front`. Then send the set to the person with `mcp__waystone__review` (copy to `G:\My Drive\artifacts\carve\M-21\T-105\round-1`).
- **Done**: arm, targets, rig, clip, fault, frames as agreed (nothing in the agreed arm, targets, rig or frame list changed); samples; `ArmGeometry` Core test; CLAUDE.md layout rows; tests.json inputs; Verify line.
- **Deviations to know** (not changes to the agreed items): the arm stage runs before the cylinder in the same scenario (a model cannot be emptied), the cylinder's `define_mesh` takes `drop_targets`, and the cylinder's frames are byte-identical; Wrist's local rest position is 0.3 (world 0.6); the +z frames are orthographic like T-103's; frame 7's silhouette probes next to the spike are filtered to the ray test's sure ones.
- **Left**: frame review; `cairn lint`; `cairn task T-105 finish`. Not run: `server-regression`.
