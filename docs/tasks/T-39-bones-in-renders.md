---
description: >-
  render draws the skeleton over the model, with bone names available as labels
---
# T-39 — Bones in renders

**Status:** In Progress

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-44

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees the skeleton in place, so a bone in the wrong spot is obvious.

## Scope

- **`render` argument `bones`:** each bone a line from head to child, drawn over the model and not hidden by it, in the current pose, clip and time. `PositionVertex` line lists draw by the engine's docs.
- **`bone_labels`:** names beside the bones, if the engine's text path allows it without M-4's work. If not, stop and ask.
- **Tests:** a frame of the gardener with bones, and pixel checks that a bone's line is at its expected place in rest and in a pose.

## Notes

- M-4's T-24 and T-25 later grow `render` too. Keep new arguments independent, so those tasks rebase cleanly.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on Sonnet 5.5 (one agent).

**Arguments:** `render` takes `bones` (bool) and `bone_labels` (bool; needs `bones: true`, else a tool error). Both need a skeleton (tool error `no skeleton`). Independent of `labels`, `highlight`, `mode`, `mesh`, `views`; they follow the frame's `pose`/`clip`. The caption adds `bones` or `bones, bone labels` after labels/highlight, only when asked, so every existing caption is unchanged.

**Drawing:** `Skinning.Heads` (posed head of each bone, Core) and `BoneLines.Segments` (Core): a line from a bone's head to each child's head; a leaf is a line along its parent's direction for half the parent's length (the orchestrator's readings, matching T-37's leaf segment); a bone with no parent and no child is only its head. `BoneOverlay`/`BoneDraw` (Server) turn them into pixels. They are drawn with the engine's screen primitives (`IScreenPrimitives.DrawLine`/`DrawCircle`, depth-free, in the same pass as the id labels), in magenta, 3 px wide, 4 px dots, not as a `PositionVertex` line list: the docs say such a mesh depth-tests, so the model's own faces would hide a bone that runs inside it. `bone_labels` uses `LabelLayout` (new `BoneLabels`, the same text path as T-29's id labels): each name beside its head, up and right, no occlusion test; M-4's T-24/T-25 not needed. Framing stays on the rest pose.

**Checks:** crate scenario (`ChainBones.cs`), the two-bone chain from +Z: at rest magenta on the base-joint line and on the leaf, none past the leaf's tip or beside the box; with `base` turned an eighth about +Z, magenta along the left-leaning line and leaf, none at the x-mirror, the y-flip or the rest position; the frame without bones is byte-identical before and after bone frames. Gardener (`CharacterScenario.cs`, hence `scenario-character` added to Verify): Head, Elbow_L and Elbow_R heads at rest; posed, Elbow_R's head at the worked (-0.391, 0.911), none at its image mirror, its y-flip or where it rests. Unit: `BoneLinesTests`, `BoneOverlayTests` (pixels against `CameraPixels`), `BoneRenderToolTests` (refusals, caption), render schema list in `EditToolTests`.

**New checks shown failing** (local run of the crate scenario, mutation reverted):

| Mutation | First check that failed |
|---|---|
| overlay mirrored in x | chain-bones-bent-z: no bone line base to joint, leaning left (rest frames are symmetric, so only the posed frame tells) |
| overlay flipped in y | chain-bones-rest-z: a bone line past the leaf's tip |

**Hash guard:** the M-18 verify jobs' frames (J-20261110-220607-921c62 crate, -220614-c6df64 character) against this branch's: crate 44 and character 17 existing frames byte-identical; cube not compared (nothing in it uses a skeleton, and a render without `bones` takes the old path). The person approved the five new frames in `round-1` (`artifacts/carve/M-17/T-39/round-1`).

**Not checked:** `bones` with `views` sheets and several meshes by frame (code path shared); labels crowd on the gardener's clavicle/shoulder bones (seen in the frame; a names-only limitation); the root-leaf corner (a lone bone is a dot only) is my reading, D-6 does not say. No engine need recorded.
