---
description: >-
  render_glb draws a .glb through Empyrean's own loader and GPU skinning at a clip and time, framed as Carve's preview is
---
# T-47 — Render a loaded glb

**Status:** Done

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

### As built

Two agents, both on Opus 5.5: the first built and committed the tool, and the second rebased, tightened one test, ran the breaks, the guard and `verify`, and asked for the review.

- *First hour, proven:* a `SkinnedPose` draws on Carve's headless host with nothing more than the component. The glb's bytes go through `IModelLoader.LoadModel(bytes, ModelFormat.Gltf, new ModelLoadOptions())` on the frame thread, keeping the file's own `Material` and `SubmeshMaterials`. The entity gets `MeshInstance` + `Transform3D` + `SkinnedPose { Palette }`. The palette comes from the engine alone: `ClipPoseProvider(skeleton, clip) { Time = t }.Evaluate`, then `SkinningPalette.Compute`, over the skeleton and clips of `IModelGeometryLoader.Parse`. No `AnimatorSystem` or `AddEmpyreanAnimation` is needed, because the pose is set once per request rather than played. No engine need.
- *Tool:* `render_glb(path, clip?, time?, view, direction, zoom, target, projection, size)`, in `src/Carve.Server/RenderGlbTool.cs` and `GlbScene.cs`. `Arcodiant.Empyrean.Animation` is referenced through `$(EmpyreanVersion)`. clip and time follow render's own rules and wording. The errors are: a missing file; a clip for a file with no skin; an unknown clip, listing the clips; and a target outside the rest bounds. The loader's `ParsedModel.Warnings` are repeated after the caption. The tool is registered in `Program.cs` beside T-45's `get_clips`; the rebase onto the milestone conflicted there, and the resolution kept both tools.
- *Same scene as render:* in `RenderPump`, a glb going in or out is a scene change, and it ends its step. `RenderSystem.ShowGlb` hides the model's entities and adds the glb entity, placed in the drawn space of the file's rest bounds (`DrawnSpace.Of`, the call render makes, T-32). The same `ApplyView` then applies the view. The next plain render shows the model again.
- *Material: the glb frame is darker than render's,* at about 0.73 of its brightness. The file loads as the engine's PBR material, and render draws with `Material.LitMesh`. In shape, pose and framing the two frames agree. The person ruled on 2026-11-12 that "render goes PBR", and **T-82 owns that change**. T-47 keeps the file's own materials, as its spec says, and does not change render's material.
- *Tests:* `RenderGlbTests` (regression) and three `RenderPumpTests`. The second agent tightened the glb pump test so that it marks the end of each step. Before that, a pump that did not end the step on a glb change still passed.
- *Guard:* all 87 existing frames hash identical to the before set (`artifacts/review/T-47/hashes-before.txt` against `hashes-after.txt`). They are the scenario-cube and scenario-crate frames from verify's jobs J-20261112-060217-0c0017 and J-20261112-060220-f50441.
- *Each new check shown failing:* each break ran locally (`dotnet test --filter RenderGlbTests|RenderPumpTests`) and was then reverted. Where an earlier assertion would fire first, it was disabled for that run only.

| Break | Check that failed |
|---|---|
| glb frame flipped in X (a mirrored camera) | near hand: "Walk at 0 s: the near hand at (219, 290) is background, expected skin" |
| the same, hand check disabled | mirror: "the hand's mirror at (292, 290) is skin" |
| glb entity scaled by -1 in X | near hand: "is trousers, expected skin" |
| no `SkinnedPose` | near hand at 0 s: "is trousers, expected skin" |
| no drawn-space transform | near hand: "is background, expected skin" |
| the same, loop checks disabled | rest head: "the head at (269, 105) is trousers, expected skin" |
| scale 1.05/R instead of 1/R | silhouette: "(155, 43, 358, 456) is not render's (160, 62, 353, 455)" |
| clip names left out of the error | errors test: `Assert.Contains` sub-string not found |
| model left hidden after a glb | silhouette against render's empty frame, and the before/after byte equality |
| pump not ending the step on a glb change | pump test: log `["glb", "view", "|", ...]` against `["glb", "|", "view", ...]` |

- *Brought up to date (2026-11-17, RV-52, 05237fb; the person's ruling "Skin at bind pose"):* with no clip, `render_glb` no longer draws a skinned file as it lies. `GlbScene` gives it the bind-pose palette (every bone at its node's rest transform composed with the file's inverse binds, `SkinningPalette.Compute`), as a game skins the file with no clip playing; only a file with no skin is drawn unskinned. The caption says "no clip: skinned at its bind pose" (or "no clip: the file as loaded, unskinned"), and the tool description says so. Before, a file whose joints or inverse binds were wrong drew correctly at rest. Good frames are byte-identical; Orcward's rest-front frames of a swapped bone order and swapped inverse binds now fail (T-48). In the same commit `render` lights a posed flat mesh by its skinned corner normals (`Skinning.Posed`), as the exported file's corners are lit in a game, and not by the posed polygon's own normal.
- *Guard scope:* T-47's `**Verify:**` was `server-regression, scenario-cube, scenario-crate`, and its hash guard covered those 87 frames only, though `RenderSystem` and `RenderPump` draw every frame suite; character, soft, rig, shed and export were neither verified nor hashed here. The milestone's verify ran every suite afterwards, and T-82 re-baselined every frame.
- *Not checked:* a real file with loader warnings. The warnings path is tested only with a fake loader. The queued server-regression job kept no frames, so the reviewed frames come from a local run of `RenderGlbTests` on the committed code.
- *Known limits:* the engine does not cache a load from bytes, so `RenderSystem` keeps the last file's `Model` and reuses it while the bytes are equal. Each distinct file is uploaded once and never released, because the docs do not say that a by-bytes model's mesh may be destroyed.
- *Review:* round 1 (`~\artifacts\carve\M-19\T-47\round-1`) was rejected for a skin-coloured patch at the top of the near sleeve (glb Walk 0 s +x). A crop of the shoulder shows the same patch in render's own frame of the `.carve`, at the same clip and time. It therefore comes from the gardener's geometry or colours, not from `render_glb`. On 2026-11-12 the person ruled that T-47 finishes as it is and that **T-84 owns the patch**: `GardenerSpec.cs`, the samples and their frames. T-47 does not touch the sample.
