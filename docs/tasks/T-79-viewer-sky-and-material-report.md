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

### As built (2026-11-16, Claude Sonnet 5.5)

**The switch.** `--sky` (no value) in `ViewerOptions` (`ViewerOptions.Sky`, default false). It adds one entity carrying `Carve.Server.RenderSky.Sky`, which is `ProceduralSky.Default` unchanged, with its own sun, baked once. The viewer links `src/Carve.Server/RenderSky.cs` as a file (`Compile Include ... Link`) and still references no Carve project. The viewer's own `DirectionalLight` and ambient stay as they are; the sky does not follow them, and no sky is drawn behind the model. The viewer now loads with `LoadModel(path, generateMipmaps: true)` (ruling 1, engine need 8).

**How T-80 calls it**, with the barrel's export as the content folder:

```
pwsh scripts/run-viewer.ps1 <content folder> <model path in it, e.g. models/barrel.glb> <out folder> front=1.6,1.2,2.2 --sky
```

`run-viewer.ps1` passes any `--` word through as before (an example is in its header). The out folder gets `<view>.png` per view and `view.json`; exit 0 is a pass.

**`view.json`.** The old keys are unchanged (`model`, `triangles`, `bounds`, `views`, `sun`); added are top-level `sky` (bool) and, per entry of `materials` (no name: engine need 3), beside `baseColor` [r,g,b,a, linear], `metallic`, `roughness`, `pbr`:

| key | holds |
|---|---|
| `emissive` | [r,g,b], `Material.EmissiveColor` (linear) |
| `occlusion` | `Material.Occlusion` (1 when the file has no occlusion map) |
| `doubleSided` | bool |
| `flags` | `MaterialFlags` as the engine prints it, e.g. `Textured, SolidColor, NormalMapped, Lit, LitWithVertexNormals, Pbr` |
| `textures` | object with the slots `baseColour`, `metallicRoughness`, `normal`, `emissive`, `occlusion`, each `{ set, width, height, format }` (`set` false gives 0, 0 and `format` null; `format` is the engine's `TextureFormat`, `Rgba8Srgb` for base and emissive, `Rgba8Unorm` for the rest) |

Per T-76, base colour is the linear `baseColorFactor`, `occlusion` is the strength written only with an occlusion map, and `emissive` is zero when the factor was black. `materials` lists one entry per submesh material, or the base one.

**Hashes** (SHA-256 of every PNG, before and after, mipmaps on and `--sky` off; `artifacts/review/T-79/hashes-before.txt` and `hashes-after.txt`, compared with no difference):

| frame | SHA-256, first 12 |
|---|---|
| viewer-cube `first/front.png`, `second/front.png` | 96c9265cf60f |
| viewer-cube `minus-x/minus-x.png` | de4f30489fab |
| scenario-export `viewer/front.png` | fa9ca6a9ade7 |
| scenario-export `viewer/minus-x.png` | ff9ca6d9f4cb |
| scenario-export Carve frames (5, `front*.png`) | bf8ecf04ae27 |

No existing frame changed by loading with mipmaps.

**Tests** (`tests/Carve.ModelViewer.Tests/TexturedCubeTests.cs`, fixture `TexturedCubeFixture.cs`, a metallic 1 / roughness 0.35 cube written with SharpGLTF, all five slots at distinct sizes): every field and slot size reported with and without `--sky`; an untextured cube reports every slot unset; the +Z and top faces are at least 60 (max channel) lighter with the sky than without (measured with the fixture's near-black emissive: +Z (158,141,120) with the sky; T-78's probe saw 11 against 150); the corners stay the same dark background. Each shown failing:

| Broken on purpose | Tests that failed |
|---|---|
| the sky entity never added | A_metallic_face_is_lighter_with_the_sky_than_without |
| baseColour slot read from the normal texture | View_json_reports_every_field_and_texture_of_the_material (both cases) |

A review of the plain and `--sky` frames of the fixture cube went to the person (round-1); it came back with no notes. Not checked: a real textured (non-flat) model under `--sky`, which is T-80.
