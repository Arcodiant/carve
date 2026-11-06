---
description: >-
  An agent sets per-vertex weights and renders the model in a given pose, skinned by Carve.Core
---
# T-32 — Explicit weights and posing

**Status:** Open

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-30, T-31

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent weights vertices by hand and sees the model bend, so skinning can be judged before there is any animation.

## Scope

- **Core:** per-mesh vertex weights, as D-6's *Weights* says; `Pose` (per-bone local rotation and translation, rest where not given); and linear blend skinning to posed positions and normals, in double, narrowed once.
- **Tool:** `set_weights(mesh, vertices, weights)`, with weights by bone name. The result says if influences were capped.
- **Render:** a `pose` argument, drawn as a plain CPU-posed mesh through `MeshBridge`. Framing: confirm D-6's working assumption of rest-pose bounds, and record the outcome in D-6.
- **Sample:** two boxes on a two-bone chain, bent at the joint.
- **Tests:** `unit` for skinning against hand-worked values, the rest pose returning rest positions exactly, and normalisation; a frame of the bent chain whose pixels a flipped bend would fail.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
