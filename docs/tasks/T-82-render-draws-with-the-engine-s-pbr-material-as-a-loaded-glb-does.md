---
description: >-
  render draws with the engine's PBR material, as a loaded .glb does
---
# T-82 — render draws with the engine's PBR material, as a loaded .glb does

**Status:** Open

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-47, T-84

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Carve's preview looks as the game will. T-47 found `render_glb` frames uniformly darker than `render`'s, about 0.73 of the brightness, with about 12 % of pixels past 8/255: the exported file's material loads as the engine's PBR material (metallic 0, roughness 1), while `render` draws with `Material.LitMesh`. The person ruled (2026-11-12) that `render` goes PBR: the preview draws with the same PBR material the game gets.

## Scope

- **`render`** (every look that draws the model's surfaces in colour) draws with the same PBR material, and the same parameters, that the engine's loader makes from Carve's exported `.glb`, so a `render` and a `render_glb` of the same model at the same pose agree in shading. Pose, framing and the CPU skinning stay as they are (D-6, *Two samplers*).
- **Measure** the agreement: a `render` and a `render_glb` of the gardener at rest and at one clip time, with the table of differing pixels under D-6's rule (8/255, 0.5 %). T-48 owns the full comparison; this shows the shading gap is gone.
- **Existing frames:** the byte-identical guard is lifted once, for this task only. Every scenario's frames will change; re-measure each pixel check that reads a colour from the model (worked from the geometry and the new material, not copied from the new frames), keep every check's intent, and keep each frame's positive and mirrored/flipped checks.
- **Review:** put the new baselines to the person, before and after, through `mcp__waystone__review`.
- **D-6 and `CLAUDE.md`:** record the ruling where they say how renders and the preview draw.

## Notes

- The person's ruling, 2026-11-12, on T-47's finding: "render goes PBR".
- Looks that are not the model's own colour (wireframe, normals, UVs, highlight, bones, weights, labels) keep their own materials unless the person rules otherwise; ask if one cannot.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
