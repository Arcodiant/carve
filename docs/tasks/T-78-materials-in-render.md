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

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft, scenario-uvs, scenario-cuts, scenario-gatehouse, scenario-rig

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

### Plan

Read against landed code and the 0.119.0 docs on 2026-11-16, ahead of T-74 and T-76. Names marked *(T-74)* or *(T-76)* are placeholders until those As builts say otherwise.

**What changed since planning.** The Scope holds. These are the consequences it does not spell out:

- **Render does not see the model's materials.** `IRenderScene.ShowMesh` gets only `Model.Combined`, a `PolygonMesh`. The material records live on the `Model` *(T-74)*. `RenderPump.ShowMesh(ModelSnapshot)` has the model, so it passes the materials through. Check that the posed path (`Skinning.Posed(...).Combined`) keeps each face's material name as it keeps colours.
- **The sky's specular probe is baked once.** `ProceduralSky` lights `Pbr` faces in two ways:
  - an SH diffuse term, re-read every frame;
  - a GGX specular probe, baked whole in the first frame that renders the scene (`IReflectionProbes`, the procedural-sky guide).

  The probe does not follow later changes to the sky. Under ruling 2 the sky never changes: it is `ProceduralSky.Default`, with its own fixed sun. One bake therefore serves every view, and nothing is rebaked or settled. Carve's directional light still turns with the camera, but the sky reads no light (`SunFromDirectionalLight` is off).
- **Textures are trilinear with mips (ruling 1).** The loader picks a model texture's sampler from the caller's `generateMipmaps` flag alone. `true` gives mips and Linear on all three filters, with `Repeat` and anisotropy 1. It ignores the `.glb`'s glTF samplers (engine need 8 in D-1). The function makes the same textures: `ITextureLoader.DecodeCpu(bytes)`, then `IGraphicsResourceManager.CreateTexture(pixels, w, h, Rgba8Srgb` for base and emissive, `Rgba8Unorm` for the rest, with the sampler `generateMipmaps: true` gives, `generateMipmaps: true)`. These are owned textures that Carve may destroy. Write the exact `SamplerDescription` into As built. The equality test shows it matches the loader's (`Format`, `MipLevels`, sampler).
- **Occlusion strength without an occlusion map cannot reach the game.** glTF carries the strength only on `occlusionTexture`, and T-76 writes it only there. The engine applies `Material.Occlusion` to indirect light even with no map. The function therefore passes `OcclusionTexture is null ? 1 : Occlusion`, which is what the loader gives, and the equality test confirms it. By ruling 4 render and export ignore it, and the warning that it has no effect is T-75's.
- **Unmapped faces need (0, 0), not the checker's white cell.** `LookMeshes.CornerUvs` puts an unmapped face's corners at `CheckerTexture.WhiteCentre`. Export writes (0, 0) for them (`GlbExport`'s corner-UV rule), or no `TEXCOORD_0` at all when no face is mapped. The material path uses the export's rule.
- **The product has no loader path.** Drawing an exported `.glb` in Carve is T-47 (M-19). The equality test therefore runs in an in-process host, below (ruling 5).
- **Carve's target is linear `Rgba16Float` with no tonemap,** clamped at readback. The planning probe's near-black metal was seen on `StandardHdr`, which tonemaps. In Carve, emissive and sky-lit specular will clip at 255.
- **`Pbr` faces get the sky's SH ambient on top of Carve's `AmbientLight` 0.25. `Lit` faces do not.** The procedural-sky guide says: "The classic `Lit` (non-PBR) path takes no sky ambient". So a matte white material face is brighter and bluer on top than an uncoloured face. `ColourRule` does not predict `Pbr` pixels. The description says so.

**Agent 1: probe and sky (commit 1).**

1. **Baseline hashes.** Use the milestone head after T-74 and T-76 merge. Take every frame hash in each scenario suite's `run.json`, and the hash of every PNG the `server-regression` tests write under `artifacts/*-tests/`. Take them from runs of that commit (`cairn test run <suite> --fresh`, reason: "T-78 baseline hashes"), not from an older carried pass. Keep the table in `artifacts/review/T-78/hashes-before.tsv`.
2. **The Scope's probe steps 1 to 3,** in the product, headless. Add these:
   - **1b.** A highlighted material face: the shaded look's white and orange vertex colours multiply into `Pbr`. If they don't, highlight shows nothing on material faces. Stop and ask.
   - **2c.** History independence. Render `front`, `back`, `front` of a metal face. The first and third must be byte-identical, and the second must equal `back` rendered first in a fresh process. With a fixed sky this should hold with no rebake. If it fails, stop and ask.
   - **2d.** In step 2, record the metal pixel with and without the sky. Rule P3's threshold comes from these values.
   - **2e.** A mipmapped, trilinear texture from `CreateTexture` draws on Carve's pipeline.
3. **`src/Carve.Server/RenderSky.cs`,** engine types only, so the viewer links it (T-79). It is the one definition: `public static ProceduralSky Sky => ProceduralSky.Default;`. Nothing is changed from the defaults, sun included, and `SunFromDirectionalLight` stays off. Write the `Default` values, `SunDirection` among them, into As built.
4. **`RenderSystem.BuildScene`** adds one entity carrying `RenderSky.Sky`. Nothing else in `RenderSystem` or `RenderPump` changes for the sky: no sun writes, no `Rebake`, no settling.
5. **The guard.** Run every Verify suite and compare every hash with step 1. If any differs, stop and ask (the Scope's rule). Do not scope the sky by layer, by look, or by whether the mesh has a material. Then commit.

**Agent 2: the function, the plumbing, tests (commits 2 and 3).**

- **`src/Carve.Server/PbrMaterials.cs`, the one function:**
  - `internal static Material Build(Carve.Core.Material m, Func<TextureRef, bool, Texture> texture)` *(T-74's texture type)* calls `Material.Pbr`:
    - `FaceStyle.ToLinear(BaseColour)` with W 1, the same conversion T-76 writes;
    - metallic and roughness;
    - the five textures, base and emissive with sRGB `true`;
    - linear emissive;
    - the occlusion rule above;
    - `DoubleSided`.
  - An instance (frame thread, made by `RenderSystem`) holds two caches:
    - textures keyed by (SHA-256 of the PNG bytes, sRGB), so one PNG used as both colour and data is two textures;
    - engine materials keyed by every field plus the texture keys.

    Both are held for the process, like `Checker`. If ever released, the materials go first and then the textures (the `Material` lifetime contract).
- **`MeshBridge.ToSubmeshedMeshData`** keys each triangle by colour *or* material:
  - uncoloured first, then colours by first appearance (unchanged), then materials by first use by face. That is T-76's primitive order, so submesh *i* pairs with the loader's `SubmeshMaterials[i]`.
  - Corner UVs, in A, C, B order (export's rule), go into the vertex buffer **only when some live face has a material**. A colour-only mesh then gets exactly today's buffers. A `MeshBridgeTests` case asserts that by array equality.
- **`RenderSystem.Make`:** the submesh branch runs when `scene.FlatColour is null` and the mesh has colours or materials. Material submeshes take `PbrMaterials`, colour ones `LitMesh` as now. Checker, uvs, wireframe, normals and weights go through their existing branches before it, so they ignore materials as they ignore colours.
- **Description:** the Scope's sentence, plus: material faces take the sky's ambient and colour faces do not.
- **The equality test,** `tests/Carve.Server.Tests/PbrEqualityTests.cs` (`regression`):
  - It hosts the engine headless in-process as `Program.cs` does (`AddEmpyreanCore`, `AddEmpyreanGraphicsHeadlessCore`, `UseRenderPipeline<RenderComposition>`). It links `RenderComposition.cs`, `RenderSky.cs`, `PbrMaterials.cs`, `MeshBridge.cs`, `ViewFraming.cs` and the export files, and builds the scene's lights and sky through the same code `BuildScene` uses. Factor that into a small linkable `RenderLights` class.
  - The cube has six materials covering every field and slot, with coarse block textures (rule P5's marker). It is exported through T-76's code and loaded with `LoadModel(bytes, ModelFormat.Glb, generateMipmaps: true)`.
  - First assert every `Material` field, each texture's `Width`, `Height`, `Format`, `MipLevels` and sampler, and that the loader's textures are not the function's (reference inequality, so nothing compares with itself).
  - Then draw Carve's own mesh data twice: with the loader's `SubmeshMaterials`, and with `Build`. Compare the raw readback bytes at `front` and `back`.
  - Then check that the harness's `Build` frame equals the real `carve` `render` of the same cube and view, pixel for pixel after the same sRGB encode. That shows the harness is Carve's render (ruling 5).
  - If either comparison is not identical, stop (the Scope's rule). Record the first differing field or pixel.
- **The other tests** are the Scope's. Each new frame uses the shared pixel rules below. Every argument family includes `highlight` on a material face, `pose` and `clip` on a skinned mesh with a material, and a `views` sheet.
- **The guard again** on the finished branch, with before-and-after hashes in As built.

**Frames for the person's review** (`artifacts/review/T-78/`, one contact sheet plus the files):

1. the equality cube at `front` and `back`, loader beside function, and the zero-diff count;
2. a metal face (roughness 0.35) with and without the sky;
3. a textured face whose marker shows orientation, at `front` and at zoom 3;
4. an emissive face;
5. a material face beside a colour face;
6. `double_sided` seen from behind;
7. one frame per argument family;
8. the five other looks on the material cube, showing materials ignored.

**Shared pixel rules for T-78 and T-80, accepted by the person (ruling 7),** with P2 widened for mipmaps.

- **P1, exact where exact is possible:**
  - unchanged frames byte-identical;
  - function equal to loader;
  - **locality:** Carve's pipeline has no shadows and the probe holds only sky, so a face's interior pixels depend on its own material or colour, the view and the light alone. A face's interior samples equal those of the same face in another render where only other faces differ.
  - **history:** a request gives the same bytes whatever was rendered before.
- **P2, where to sample:**
  - The point's UV footprint has a half-width of **the larger of 2 texels and 3 s**. Here s is the number of mip-0 texels one screen pixel spans there, along its longer axis.
    - Trilinear sampling picks a mip level whose texel is up to 2 s wide, and reads ±1 of those texels.
    - With half a pixel added, that reaches about 2.5 s; 3 s leaves a margin.
    - Close up, sampling is bilinear at mip 0: ±1 texel, plus margin, gives 2.
  - The footprint lies inside one class region of the texture (stave, gap, clean, rust, rune, ring, marker...), as the generator labels it, so blended texels are all one class.
  - The point is at least 3 px from any drawn polygon edge at 512, scaled with size; in clear sight by the ray test; and clear of labels and caption.
  - This generalises `UvsChecker`'s 0.2 to 0.8 inset. Small features (T-80's 2-texel gaps) need zoomed frames, where s is small.
- **P3, relations, not values,** between two P2 samples on the same face:
  - "brighter" means a lead of at least M = 12 levels in max(R, G, B) of 8-bit sRGB;
  - hue is channel order, with at least M between ordered channels;
  - "lit, not near-black" means max channel at least T, halfway between probe step 2d's with-sky and without-sky metal values.
- **P4, emissive:**
  - glowing when R ≥ 230 and R > G + M > B + 2M (for `#FF8020`), and brighter by M than the same face's non-emissive sample;
  - absent when no P2 sample in the frame meets it.
- **P5, orientation:** every new frame has a sample pair whose classes swap under a v-flip, a u-mirror or a mirrored camera.
- **P6, a floor:** each check needs at least 8 P2 samples, or it fails as "not in sight" rather than passing empty.
- **P7, the viewer** (`StandardHdr`, tonemapped, the same sky, its own directional light): P3 and P4 relations only, never values.

### Rulings (2026-11-16, the person)

These override the Scope and the Plan where they differ.

1. **Mipmaps on.**
   - The one material function uploads with mipmaps and the trilinear sampler that `generateMipmaps: true` gives.
   - Render, the viewer (T-79) and the equality test all load with `generateMipmaps: true`.
   - Export writes no sampler.
   - P2's footprint is widened to the larger of 2 texels and 3 s.
2. **A fixed sky sun, at `ProceduralSky.Default`'s own direction.**
   - The sky is the engine's default unchanged, baked once, with no rebake or settling.
   - This replaces the Scope's "sun along Carve's light direction for each view".
   - The rebake-and-settle probe step, `Settling`, the `RenderPump` changes and the `SunDirection` writes are dropped. Probe step 2c, the history check, stays.
   - T-79's viewer uses this same sky.
3. **Engine need 8 (D-1), Low:** the `.glb`'s own glTF sampler should decide texture sampling. 0.119.0 ignores it, and mipmaps need `generateMipmaps: true` from the loading code. Interim: Carve's render, the viewer and the tests pass the flag, and a game must pass it too.
4. **Occlusion strength without an occlusion map:** render and export ignore it. `define_material` or `check` warns that it has no effect; that warning belongs to T-75, not this task.
5. **The equality test** runs in an in-process engine host in the tests now, plus a check that the harness's frame equals the real `render`'s.
6. **T-80's iron:** rust is told from iron by hue, plus a "not near-black" check that fails without the sky. This is T-80's, and it uses P3's threshold T from probe step 2d.
7. **P1 to P7 are accepted,** with P2 widened as in ruling 1.
