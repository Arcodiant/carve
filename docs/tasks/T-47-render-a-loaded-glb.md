---
description: >-
  render_glb draws a .glb through Empyrean's own loader and GPU skinning at a clip and time, framed as Carve's preview is
---
# T-47 — Render a loaded glb

**Status:** In Progress

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-41

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent, and T-48, sees a `.glb` as the game gets it, through Empyrean's own loader and skinning.

## Scope

- **Tool:** `render_glb(path, clip?, time?, view...)`, drawing in the same scene, light and camera as `render`, framed from the file's rest bounds as T-32 settled.
- **Load:** through the engine's model loader, with the file's own materials. Reference `Arcodiant.Empyrean.Animation` in `Carve.Server` through `$(EmpyreanVersion)`, and pose from the file's own clip into `SkinnedPose`.
- **First hour:** prove a `SkinnedPose` draws on Carve's headless host, and record how. If it does not, record an engine need and stop.
- **Errors:** a missing file, a file with no skin given a `clip`, an unknown clip listing the names. Loader `Warnings` are repeated in the result.
- **Tests:** `regression`, a frame of the gardener at a clip and time, with pixel checks.

## Notes

- It shares no skinning code with Carve's preview, on purpose (D-6, *Two samplers*). Stronger model: it meets the engine where the docs stop.
- T-18's viewer remains the game stand-in for static models. This tool is the product's.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Hand-off

One agent, on Opus 5.5, handing off at the tool-call checkpoint (about 90 calls). Code committed; `verify` not yet passed.

**Done.**
- *First hour, proven:* a `SkinnedPose` draws on Carve's headless host with nothing more than the component. The glb's bytes go through `IModelLoader.LoadModel(bytes, ModelFormat.Gltf, new ModelLoadOptions())` on the frame thread (the file's own `Material` and `SubmeshMaterials`), and the entity gets `MeshInstance` + `Transform3D` + `SkinnedPose { Palette }`. The palette comes from the engine alone: `ClipPoseProvider(skeleton, clip) { Time = t }.Evaluate`, then `SkinningPalette.Compute`, over the skeleton and clips of `IModelGeometryLoader.Parse`. No `AnimatorSystem` or `AddEmpyreanAnimation` is needed, because the pose is set once per request rather than played. No engine need.
- *Tool* `render_glb(path, clip?, time?, view, direction, zoom, target, projection, size)` (`src/Carve.Server/RenderGlbTool.cs`, `GlbScene.cs`). `Arcodiant.Empyrean.Animation` is referenced through `$(EmpyreanVersion)`. clip/time follow render's own rules and wording: time only with a clip and required by it, and refused outside 0 to the clip's duration. The errors: a missing file; a clip for a file with no skin; an unknown clip, listing the clips; a target outside the rest bounds. The loader's `ParsedModel.Warnings` are repeated after the caption.
- *Same scene as render:* the pump (`RenderPump`) treats a glb as a scene change, as it does a pose. `RenderSpec.Glb` is compared by reference. On a glb request `RenderSystem.ShowGlb` hides the model's entities (`RenderLayerMask.None`), adds the glb entity, and ends the step. The view is then applied by the same `ApplyView`, which uses the same light entity, ambient, cameras, targets, background and `ViewFraming.Frame`, with only the framing swapped for the glb's: `DrawnSpace.Of(rest min, rest max)` of the file's bind-pose vertices, the very call render makes on the model's rest bounds (T-32). The entity's `Transform3D` is that drawn space (position -c/R, uniform scale 1/R). The next plain render shows the model again on the next step. Requests that do not involve a glb take exactly the old path.
- *Tests:* `RenderGlbTests` (regression) covers the following. The sample gardener is exported and drawn at Walk 0 s and 0.5 s from +x. The near hand, posed by Carve's sampler as the test's oracle, is skin; its mirror (Size-1-x) is not. Each silhouette box is within 2 px of render's frame of the same clip and time. With no clip, the head at rest is skin. Every error is checked, and render's frame of the model is byte-identical before and after the glb renders. `RenderPumpTests` gains three tests: glb in and out as scene changes on later steps; a glb served while the model cannot be shown; and bounds, space, counts and warnings text from `GlbScene.Prepare`. All of these pass locally (`dotnet test --filter RenderGlbTests|RenderPumpTests`).

**Surprising, for the person and T-48.** In shape, pose and framing the glb frame matches render's: the silhouettes agree, and a diff mask is all body and no edge band. **The glb is uniformly darker**, about 0.73 of render's brightness on the shirt (73,122,50 against 101,166,68). The file's material loads as the engine's PBR (metallic 0, roughness 1, as the export writes it), while render draws with `Material.LitMesh`. So at Walk 0 s and 0.5 s about 12 % of pixels differ by more than 8/255, far past D-6's 0.5 % rule. The spec says "the file's own materials", so T-47 keeps them. Whether render should draw with the same PBR material, which would change every existing frame, or T-48 should compare otherwise is the person's call (D-6, *A loaded frame against the preview*). Nothing was loosened. Comparison crop: `artifacts/render-glb-tests/cmp-0.0.png` (glb | render | mask).

**Left.**
1. `cairn task T-47 verify`: the first run was stopped, still waiting on the test queue, by the agent's background time limit, with no output. Run it again.
2. Guard: the "before" frames and hashes are in `artifacts/review/T-47/before/` and `hashes-before.txt` (87 frames, the latest scenario-cube and scenario-crate artifacts from the queue at the time). Hash the frames from verify's run and compare.
3. Break table: not run yet. The planned breaks:
   - camera X mirrored on the glb path: the hand and mirror checks should fail;
   - no `SkinnedPose`: the hand check at 0.5 s should fail;
   - framing on posed bounds or no drawn-space transform: the silhouette check should fail;
   - clip names left out of the error: the errors test should fail;
   - model left hidden after a glb: the before/after render equality should fail;
   - pump not ending the step on a glb change: the pump test should fail.
4. A capture review with `mcp__waystone__review` of the new frames in `artifacts/render-glb-tests/`, copied to `C:\Users\andre\artifacts\carve\M-19\T-47\round-1`.
5. `### As built`, `cairn lint`, then `finish`.

**Known limits.** A load from bytes is not cached by the engine, so `RenderSystem` keeps the last file's `Model` and reuses it while the bytes are equal. Each distinct file is uploaded once and never released, because the docs do not say a by-bytes model's mesh may be destroyed. A real file with loader warnings was not produced; the warnings path is tested with a fake loader only.
