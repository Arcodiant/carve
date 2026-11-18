---
description: >-
  render_glb draws a .glb at chosen target weights through the engine's MorphWeights, and the model viewer takes starting weights and, in its window, a live weight slider
---
# T-108 — Target weights in render_glb and the viewer

**Status:** Open

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-104

**Verify:** server-regression, viewer-cube

## Goal

The exported file is drawn at chosen weights the way a game draws it: the engine's loader, its `MorphWeights`
component and its GPU morph-before-skin. A person can drag a dial live.

## Scope

- **`render_glb`** (M-19's T-47) takes `targets`, name to weight, 0 to 1. It resolves each name once with
  `MorphTargetSet.IndexOfTarget` and writes `MorphWeights` on the drawn entity, with `clip`/`time` as before.
  Refusals: a name the file lacks (list the file's), weights given for a file with no targets. Without `targets`,
  every existing `render_glb` frame is byte-identical.
- **The model viewer** (`spikes/Carve.ModelViewer`):
  - `--targets Bulk=0.5,Lean=0` sets the starting weights in headless and window runs;
  - in window mode (`--Empyrean:Graphics:VirtualSurface=false`), the slider panel of M-21's agreed sketch: a
    top-left text panel, one row per target with `>` on the selected one and its weight to 2 places. Keys: Tab
    for the next target, Left/Right for -/+0.05, Space to play or stop the file's first clip, 0 to reset. The
    panel is not drawn in headless captures.
- **Content (placeholder)**: T-103's cylinder with `Bulge`, exported, drawn by `render_glb` at 0 and 1 and
  compared with `render` under D-6's frame rule (Orcward's, M-19). Also drawn by the viewer headless with
  `--targets`.
- **Tests:** render_glb refusals and frames (regression), the viewer's `--targets` frame (suite `viewer-cube`'s
  project), each shown failing with weights ignored.
- The person's look at the window panel happens in T-106 on the arm. Here, check the panel by one window-mode
  screenshot of your own, and say so.

## Notes

- Owns `RenderGlbTool.cs`, `GlbScene.cs` (the render side) and the viewer.
- Evidence: `~\artifacts\carve\M-21\T-108\`.
- **Goes back to the person, stop and ask:** any difference beyond D-6's frame rule between `render_glb` and
  `render` at the same weights (only the person loosens it); a key or panel change from the sketch.
- Sonnet.
