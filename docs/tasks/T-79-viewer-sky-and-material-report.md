---
description: >-
  The model viewer takes --sky to light a model with Carve's fixed sky, and view.json reports every PBR field and texture of each material
---
# T-79 — Viewer sky and material report

**Status:** Open

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-78

**Verify:** viewer-cube, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

The game stand-in lights a model as Carve's render does when asked, and reports every material field it loaded, so T-80 can check the textured barrel in it.

## Scope

- **`--sky`** in `spikes/Carve.ModelViewer`: adds Carve's fixed sky to the viewer's scene. It links `src/Carve.Server/RenderSky.cs` (T-78) rather than copying it, with the sun along the viewer's own sun. With the switch off, the viewer is unchanged: `viewer-cube`'s frame and the export scenario's viewer frames are byte-identical.
- **`run-viewer.ps1`** passes `--sky` through, as it passes any `--` word today. Check that it does, and add an example.
- **`view.json`** lists, per material:
  - every `Material.Pbr` field (base colour, metallic, roughness, emissive, occlusion, double-sided);
  - for each texture slot, whether it is set and the texture's size;
  - the flags.

  Material names stay absent: engine need 3.
- **Tests** in `tests/Carve.ModelViewer.Tests`: the textured fixture cube (made by T-76's code, or committed beside the viewer's fixture), viewed with and without `--sky`. The first reports every field; the second shows a metallic face lighter with the sky than without, by pixels.

## Notes

- **Sonnet.** Small, and on the spike only.
- In its wave it owns `spikes/Carve.ModelViewer`, `scripts/run-viewer.ps1` and `tests/Carve.ModelViewer.Tests`.
- **Write down for the dependants**, in this file's As built: the switch, the `view.json` shape, and the hash of every existing viewer frame before and after.
- **Before the first code**, read T-78's As built for the sky's file and parameters.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - an existing viewer frame that changes;
  - any loosened check.
- **Carried from earlier plans:**
  - each new check shown failing, as a table;
  - `finish` read whole, with its `Merge task/T-NN` line;
  - artifacts by job id;
  - report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-79/`.
- Run `finish` from inside the worktree and leave the folder.
