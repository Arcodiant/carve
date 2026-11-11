---
description: >-
  render shows one bone's weights as a heat look on the model, so a stray or missing weight can be seen
---
# T-40 — Weights look

**Status:** In Progress

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-39

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees what one bone moves, on the model itself.

## Scope

- **`render` argument `weights`:** a bone name. Each face corner is shaded from the bone's weight, dark blue at 0 through green to red at 1, unlit; a vertex with no weight at all is magenta.
- **Tool description:** says how to read the colours, and that magenta needs fixing.
- **Tests:** frames with known weights and pixel checks at 0, 0.5 and 1; magenta on a planted unweighted vertex; an unknown bone is a tool error listing the names.

## Notes

- Per-vertex colour on a plain mesh is in the engine's materials guide. The look draws on the static path, not the skinned one.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5), one agent.

**Argument:** `render` takes `weights`, a bone name. It sets the look to the new `Look.Weights` and replaces `mode`; with `mode` wireframe or normals it is a tool error (shaded is allowed). Like `bones`, it follows the frame's `pose`/`clip` (drawn on the posed copy; the weight is read from the model, whose posed copy drops weights) and works with `views`, `labels`, `highlight`, `mesh`. A highlighted face is not recoloured (the heat wins; orange lines and stars still draw). Errors: no skeleton, and an unknown bone (`no bone named 'X'; the bones are 'a', 'b'`). The caption has `weights <bone>` where `normals`/`wireframe` would be, so no existing caption changes.

**Colours (the spec's, linear, unlit):** 0 = (0, 0, 0.5) dark blue, 0.5 = (0, 1, 0) green, 1 = (1, 0, 0) red, straight lerps between; a vertex with no weight at all is (1, 0, 1) magenta. A vertex weighted only to other bones is 0 (blue). The weight is the sum of the bone's influences at the vertex. A mesh with no weights is all magenta. Per face corner, from the vertex, by `LookMeshes.HeatColour` / `Compose(..., weights)` on the static path (`Material.SolidColor`, per-vertex colours, as the normals look). `WeightHeat.Of` (Server) gives the per-vertex array over `Model.Combined`; the pump holds the bone as part of what is shown (as the pose), so a new bone re-puts the mesh. Transient: with no weights to hand the look falls back to shaded for the one put the pump does under the previous look.

**bones with weights:** both can be given. A bone line (3 px, dots at heads) and an unweighted vertex are both magenta, so they are NOT told apart by colour; only by shape (thin lines with joint dots over the model against a patch of a face). The tool description says so. Not changed, since the spec keeps both colours.

**Checks:** crate scenario `ChainWeights.cs` (after `ChainBones`): four flat quads, positive pixel checks at weight 0 (dark blue 0,0,188), 0.5 (green) and 1 (red) for both bones; the planted unweighted corner magenta, with red at the other three corners (an x-mirror or y-flip fails); the posed frame (joint a quarter turn: top quad red at its carried place, background where it rested and where a y-flip puts it, left quad unmoved); the plain frame byte-identical before and after the weights frames; tool errors for the unknown bone and for mode wireframe. Unit: `WeightsLookTests` (heat stops, corner order and unlit, refusals, caption), render schema list in `EditToolTests`, `WeightHeat.cs` linked into the server tests.

**New checks shown failing** (local crate runs, mutation reverted):

| Mutation | First check that failed |
|---|---|
| heat reversed (1 - w) | weights-base-z: left quad weight 1 is (0,0,188), expected red |
| unweighted drawn blue | weights-base-z: unweighted corner not magenta |
| corner colours taken from the wrong vertex | weights-base-z: unweighted corner is red |
| posed weights frame drawn at rest | weights-joint-posed-z: top quad carried round joint's head is (48,53,56), expected red |

**Hash guard:** local runs on this machine, tree stashed (before) and applied (after): crate 50 of 50 existing frames byte-identical (55 after: five new), cube 32 of 32, character 21 of 21. The person approved three frames (`artifacts/carve/M-17/T-40/round-1`).

**Not checked:** `weights` on a sheet of `views` or several meshes by frame (shared code path); the gardener model (no frame added, so `scenario-character` is not in Verify); a pose rotation that swings mesh parts through a mirrored x position (the x-mirror is covered by the static frames). No engine need recorded.

- Landing (rebase, verify, finish) ran on Sonnet 5.5 (claude-sonnet-5-5).
