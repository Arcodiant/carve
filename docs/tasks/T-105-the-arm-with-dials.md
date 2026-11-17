---
description: >-
  samples/arm.carve, an arm with Bulk and Lean targets, built, subdivided, rigged, checked and seen at its weights through the tools in scenario-morph
---
# T-105 — The arm with dials

**Status:** Open

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-100, T-101, T-103

**Verify:** core-unit

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
- Evidence: `C:\Users\andre\artifacts\carve\M-21\T-105\`.
- Work the numbers through every check before the scenario is written (M-4 and M-7 retrospectives).
- **Goes back to the person, stop and ask:** any change to the agreed arm, targets, rig or frame list; a pixel
  check that cannot pass as specified; any existing frame that changes.
- Look-heavy: plan two agents. Sonnet for the build, with the review agent separate.
