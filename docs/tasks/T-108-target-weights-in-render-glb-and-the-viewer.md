---
description: >-
  render_glb draws a .glb at chosen target weights through the engine's MorphWeights, and the model viewer takes starting weights and, in its window, a live weight slider
---
# T-108 — Target weights in render_glb and the viewer

**Status:** In Progress

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

### As built (so far; the render comparison and finish are left, see Hand-off)

Built by Claude Sonnet 5.5.

- **`render_glb` `targets`** (`RenderGlbTool.cs`, `GlbScene.cs`, `RenderSystem.cs`): an object of name to weight. `GlbScene.Prepare`
  checks each name against `ParsedModel.MorphTargets.TargetNames`; `RenderSystem.ShowGlb` resolves each name once with
  `MorphTargetSet.IndexOfTarget` and writes a dense `MorphWeights` on the drawn entity beside `SkinnedPose`, so the engine blends
  morph before skin, with or without `clip`. Without `targets` no component is added, and a frame is byte-identical (a test holds it,
  and that `{"Bulge":0}` draws the same pixels).
- **Range.** The person ruled (2026-11-18) that `render` and `check` refuse weights outside [0, 1], naming the target. `render_glb`
  takes the same range so the tools agree: `targets.Bulge: weight 1.5 is outside 0 to 1`. A non-number, a name the file lacks
  (the file's names are listed) and weights for a file with no targets are refused in the same `targets.<name>:` form that T-103's
  `TargetWeightsArgument` uses.
- **Viewer** (`spikes/Carve.ModelViewer`): `--targets Bulk=0.5,Lean=0` (each 0 to 1, a name once) sets the starting weights in
  headless and window runs, through `MorphWeights` resolved by `IndexOfTarget`; a name the file lacks, a file with no targets or a
  weight outside 0 to 1 stops the run with a message. `view.json` gains `targets` only when the option is given. `run-viewer.ps1`
  passes the word after `--targets` as it is. In window mode (`--Empyrean:Graphics:VirtualSurface=false`) the captures are made first,
  with no panel, then the window stays open with `WeightPanel`: a top-left panel, a row per target (`>` on the selected one, a 20-cell
  bar as in the sketch, the weight to 2 places), keys Tab, Left/Right (-/+0.05), Space (adds an `Animator` on the file's first clip and
  toggles `Playing`), 0 (all weights to 0). The viewer's project gained `Empyrean.Ui` and `Empyrean.Animation`.
- **Tests.** `RenderGlbTargetsTests` (regression: T-103's cylinder with `Bulge` exported; middle-row silhouette width ratio 1.2 and 1.4,
  caps unmoved; unchanged frame; refusals) and `ViewerTargetsTests` (viewer-cube: a cube whose top `Sink`s 0.3, points checked at weights
  0, 0.5 and 1; refusals; `view.json`), `WeightPanelTests`. Each fails with the `MorphWeights` line removed (shown: render_glb ratio 1
  against 1.36 to 1.44; the viewer's half-weight points stayed wall).

### Hand-off

Checkpoint (about 60 calls). Model: Sonnet 5.5. Code is committed; `cairn task T-108 verify` ran every suite green except clean-tree
(before this commit).

Done: everything under As built.

Left:
1. **Compare with `render` under D-6's frame rule.** T-103 had not landed on `milestone/M-21` (its commit 911c081 is on `task/T-103`), so
   `render`'s `targets` does not exist in this branch. After `git rebase origin/milestone/M-21`, add a test drawing the cylinder with
   `render` and `render_glb` at `Bulge` 0 and 1 (luma past 4 of 255, a frame fails past 0.5 % or a patch past 50; `Comparison` in the
   Orcward scenario is the rule, `ComparisonPng.cs`). Any difference beyond that rule goes to the person.
2. Rebase, rebuild, `verify`, fix what the rebase broke in a commit of its own, T-106 call list (below), `cairn lint`, `finish`.
3. **The person's look**: the window panel. My one window-mode run (`artifacts/window/`, the exported cylinder, `--targets Bulge=0.6`)
   shows the panel drawn top left with the bar and 0.60 and the key line, but another window covered part of the screenshot, so I
   did not judge the layout, and no key was pressed (no input is scripted); Tab, arrows, Space and 0 are checked only through
   `WeightPanel`'s tests. The person's live try is T-106's.

Surprising: `scripts/run-viewer.ps1` cannot pass `--Empyrean:Graphics:VirtualSurface=false` (PowerShell takes `--Empyrean:` as a
parameter and the viewer says `unknown argument "--Empyrean"`); I ran the viewer's dll with `dotnet exec` instead. Not fixed.

T-106 calls: `render_glb` with `targets` (`{"Bulk":0.5}`), and `dotnet exec spikes/Carve.ModelViewer/bin/Debug/net10.0/Carve.ModelViewer.dll
--content <folder> --model models/arm.glb --out <out> --Empyrean:Graphics:VirtualSurface=false --targets Bulk=0.5` for the slider.
