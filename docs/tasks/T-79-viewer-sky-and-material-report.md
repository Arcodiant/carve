---
description: >-
  The model viewer takes --sky to light a model with Carve's fixed sky, and view.json reports every PBR field and texture of each material
---
# T-79 — Viewer sky and material report

**Status:** In Progress

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

### Checked against landed code (2026-11-16)

The Scope holds, with these details:

- **`--sky` is a new kind of argument.** `ViewerOptions` throws on any `--` word other than `--Empyrean:*` and its four valued options, and it expects a value after each of those. `--sky` has no value. `run-viewer.ps1` already passes a `--` word through, but only inside its view list.
- **The sky is the same fixed sky as Carve's.** Under ruling 2, `RenderSky.Sky` is `ProceduralSky.Default` with its own sun, and it uses engine types only. The viewer adds it unchanged and does not point it at `ViewerLook.ToSun`. The viewer's `DirectionalLight` stays where it is. The sky's specular probe is baked once, on the first frame, with no rebake. This replaces the Scope's "with the sun along the viewer's own sun".
- **The viewer must load with mipmaps on.** `LoadModel(ContentPath)` today defaults `generateMipmaps` to false, which gives nearest sampling with no mips (`PixelArt`). Under ruling 1 the viewer calls `LoadModel(path, generateMipmaps: true)`.
  - That changes how textured models draw, so check that `viewer-cube` and the export scenario's viewer frames are still byte-identical. Their models have no textures, so they should be. If any frame changes, stop.
- **`Texture` exposes `Width`, `Height` and `Format`,** so each slot's size can be reported, and its colour space too if wanted.
- **The viewer draws no sky.** `StandardHdr` composes no `SkyboxStage`, so `--sky` lights the model but draws no sky behind it.

### Rulings (2026-11-16, the person)

1. **Mipmaps on.** The viewer loads with `generateMipmaps: true`, as Carve's render and the equality test do. Export writes no sampler.
2. **One fixed sky.** The viewer's `--sky` is T-78's sky: `ProceduralSky.Default` with its own sun direction, unchanged, baked once, with no rebake. It does not follow the viewer's sun.
3. **Engine need 8 (D-1), Low.** The `.glb`'s glTF sampler does not decide sampling: 0.119.0 ignores it. Whoever loads a Carve `.glb` must pass `generateMipmaps: true` to get the mipmapped, trilinear textures Carve's render shows. Without it, the engine draws them nearest-sampled with no mips. Carve's render, the viewer and the tests pass the flag, and a game must pass it too.
