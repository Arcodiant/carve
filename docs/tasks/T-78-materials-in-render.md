---
description: >-
  render draws a face with a material through Material.Pbr, the same material the engine's loader makes of the export, lit with a fixed sky so metal reads
---
# T-78 — Materials in render

**Status:** Open

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-74, T-76

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees a textured, metallic or glowing face in `render` as the game's loader would draw it, and every existing frame is unchanged.

## Scope

- **First, a probe in the product** (Carve's own `RenderComposition` and `RenderSystem`, headless). Record each step in As built.
  1. `Material.Pbr` with a texture made by `IGraphicsResourceManager.CreateTexture` (or `ITextureLoader` from bytes) draws on Carve's pipeline. Base colour and emissive are uploaded as sRGB, the other maps as linear.
  2. A `ProceduralSky` in Carve's render scene lights a metallic, roughness 0.35 face visibly, where without it the face is near-black. The planning probe saw that near-black in the viewer.
  3. With the sky in the scene, every existing frame of every suite is byte-identical. The sky must change nothing drawn with `LitMesh`, `SolidColor` or M-6's checker.

  If step 1 fails, it is an engine need: record it as `CLAUDE.md` says, and stop. If step 3 fails, stop and ask; do not scope the sky by layers or by look without a ruling.
- **The fixed sky** (new `src/Carve.Server/RenderSky.cs`, the one definition, which T-79 links into the viewer):
  - the engine's `ProceduralSky` defaults, with its sun along Carve's light direction for each view;
  - no background is drawn (Carve's pipeline composes no `SkyboxStage`), so the dark slate stays.
  - Record its parameters in As built.
- **One function** builds the engine `Material` from a T-74 `Material`: `Material.Pbr` with every field, textures uploaded once per distinct PNG and held for the session (released as `IGraphicsResourceManager` requires, after every material holding them).
- **`render` in `shaded`:**
  - A face with a material is drawn through that function, as a submesh, beside the existing colour submeshes (T-30's path).
  - Uncoloured and colour-only faces stay `LitMesh`, so a model with no material draws exactly as before.
  - Textures are sampled at M-6's corner UVs, and an unmapped face samples whatever its zero UVs give, as the game would.
  - It works with every argument `render` has (`view`, `direction`, `zoom`, `target`, `projection`, `size`, `views`, `columns`, `labels`, `highlight`, `mesh`, `pose`, `clip`, `time`).
  - `wireframe`, `normals`, M-6's `checker` and `uvs`, and the weights look ignore materials, as they ignore colours.
- **The description** says that a face with a material is drawn as the game's PBR shading draws it, under a fixed sky so that metal reads, and that a game's own lighting will differ.
- **Tests:**
  - **The equality test (`regression`):** a cube whose six faces carry six materials covering every field and texture slot is exported through T-76's code, loaded with `IModelLoader.LoadModel(bytes, ...)`, and drawn on Carve's pipeline with the loader's `SubmeshMaterials`. It is drawn again through this task's function. The two frames are byte-identical, at `front` and `back`.
  - A material and a colour face side by side; `double_sided` seen from behind; each argument family.
  - The byte-identical guard on every existing frame of every suite.

## Notes

- **Opus.** A new engine path, and the sky is a lighting change every later frame stands on. Plan for two agents: the probe and the sky, then the function, the plumbing and the tests.
- **Checked at planning (2026-11-11), in `spikes/Carve.ModelViewer` (`StandardHdr`, sun and ambient only):**
  - textured, emissive and metallic-roughness-mapped boxes drew as authored;
  - metallic 1 at roughness 0.35 drew near-black;
  - a glossy dielectric (roughness 0.15) showed no visible highlight from `front`.

  The engine's `EnvironmentLight` and `ProceduralSky` docs say a PBR metal needs one of them for image-based specular.
- **The person decided at planning:** Pbr for material faces only, so existing frames stay as they are; and a fixed sky for Pbr faces.
- In its wave it owns `RenderTool.cs`, `RenderSystem.cs`, `RenderComposition.cs`, `MeshBridge.cs`, `LookMeshes.cs` and the new `RenderSky.cs`.
- **Write down for the dependants**, in this file's As built: the sky's parameters and file (T-79), how textures are held, and the hashes of every existing frame before and after.
- **Before the first code**, read this Scope against what M-6 (T-57's texture path and `checker` plumbing), M-17 (bones and weights looks) and T-74/T-76 landed. Stop if they disagree.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - any existing frame that changes;
  - the equality test not byte-identical;
  - the sky's parameters beyond the engine's defaults and the sun direction;
  - any loosened check.
- **Carried from earlier plans:**
  - a positive pixel check on every new frame, and one that a mirrored or flipped camera, or a v-flipped texture, would fail;
  - sample shading near face corners as well as middles;
  - each new check shown failing, as a table;
  - `finish` read whole, with its `Merge task/T-NN` line;
  - artifacts by job id;
  - report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-78/`.
- Run `finish` from inside the worktree and leave the folder.
