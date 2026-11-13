---
description: >-
  render mode checker draws the model lit with a generated checker texture, and unmapped faces magenta
---
# T-57 — Checker look

**Status:** In Progress

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-54

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees its UVs on the model: stretch, direction, mirroring and unmapped faces all read at a glance, in every view `render` already has.

## Scope

- **First, a probe in the product** (Carve's own `RenderSystem`, headless): a texture made with `IGraphicsResourceManager.CreateTexture` and drawn with `Material.LitTextured` on `PositionUvNormalVertex` corners.
  - Planning's probe showed only that `IModelLoader` reads `TEXCOORD_0` and an embedded texture from a `.glb` and draws it headless.
  - If Carve's path fails, stop: it is an engine need, recorded as `CLAUDE.md` says.
- **The checker texture is this look's content** (`src/Carve.Server/CheckerTexture.cs`):
  - 256 x 256 RGBA8, sRGB, made in code: 8 x 8 cells of 32 px.
  - Column c has hue `#E04040`, `#E08A30`, `#D8C830`, `#50B050`, `#40B0C0`, `#4070E0`, `#6A50D0`, `#B050C0` (red at u = 0 to violet at u = 1).
  - Cell `(c, r)` is that hue when `c + r` is even, and the hue times 0.55 per channel, rounded, when odd.
  - Cell `(0, 0)`, at UV (0, 0), the top-left, is white `#FFFFFF`.
  - Sampled nearest (`PixelArt`), repeat, no mipmaps, so UVs outside 0..1 tile as the game would tile them.
- **`render` with `mode: "checker"`:**
  - Mapped faces are drawn lit with the checker at their corner UVs, with the same normals as `shaded` (flat or smooth, per T-42).
  - Unmapped faces are drawn lit in magenta `#FF00FF`.
  - A mesh with no UVs at all is all magenta, and the result says no face has UVs.
  - It works with every argument `render` has: `view`, `direction`, `zoom`, `target`, `projection`, `size`, `views` and `columns`, `labels`, `highlight` (as `shaded` draws it), `mesh`, `pose`, `clip` and `time`.
  - The caption names the mode, as `wireframe` does.
- **The description is content:** what the checker shows (stretched cells mean stretch; hue order is u, the white cell is the origin, so a reversed order or a misplaced white cell means a mirrored or turned island; magenta is unmapped), with a worked example.
- **Frames in `scenario-crate`** (`CrateLooks.cs` or a sibling file):
  - The crate gets UVs through `set_uvs`, worked out in the scenario: each face's corners laid planar along its dominant axis into the unit square. One face is left unmapped.
  - Frames: `front`, `back` and `+y`; a 2 x 2 `views` sheet; and `front` at sizes 256 and 1024.
  - Pixel checks are worked from the geometry and the UVs given (`CameraPixels` projection, then the UV at that point, then the expected cell): the white cell where predicted and absent from its mirror; the hue order across a face; magenta on the unmapped face.
  - Lighting changes value, so compare hue and order, not exact bytes.
  - Work every check against the geometry before writing it, and stop if one cannot pass.
- **Tests:** `regression`: the mode with each argument family, the all-magenta case, and that `shaded` frames are unchanged.

## Notes

- Opus: a new engine path, and the frames other tasks compare against.
- Runs side by side with T-55, T-56, T-59 and T-60. It owns `RenderTool.cs`, `RenderSystem.cs`, `MeshBridge.cs`, `LookMeshes.cs` and the crate scenario's look files in this wave. T-58 follows it in the same files.
- **Write down for the dependants:** how the texture is made and held (T-58 draws it again), the mode plumbing in `RenderTool`, and the hashes of the new frames.
- No sound, motion or feel: the person's look at the frames is the whole review.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame (all suites listed in Verify), by hash before and after;
  - a positive pixel check on every new frame, and one a mirrored or flipped camera would fail;
  - each new check shown failing, as a table (include a texture uploaded with v flipped);
  - sample shading near face corners as well as middles;
  - `finish` read whole, artifacts by job id, report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-57/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any loosened check, and any change to the checker's colours.
- Run `finish` from inside the worktree and leave the folder.

### Plan

Written 2026-11-13 by the research agent (Opus 5.5) against `milestone/M-6` at `b46b6fa` (T-53 landed; T-54 planned against its Scope, not yet landed). Nothing here is code yet. Build the points marked *(open rule n)* only once the person has ruled.

**Read against the code:**

- `MeshBridge.ToMeshData` already gives every triangle three vertices of its own (`PositionUvNormalVertex`, `Uv` left zero), so per-corner UVs need **no vertex splitting** for render: each vertex takes its own corner's UV.
- `Shading.Triangles` drops vertex ids; `Triangulator.Triangulate(mesh)` gives them in the same order (as `LookMeshes.HeatColours` already relies on). A corner's UV is `mesh.Uvs.Of(t.Face)[mesh.Faces[t.Face].CornerOf(v)]`, holes included. Order per triangle stays A, C, B.
- `Model.Combined` drops UVs (T-53 As built; ruling 7 gives it to T-57). **`ExportTool` exports `model.Combined` too**, so T-59, running side by side, needs the same carrying for a model of several meshes *(open rule 9)*.
- `CameraPixels.Perspective` (crate) always uses up +Y; `ViewFraming.UpFor` uses -Z when `|dir.Y| > cos 1°`. The `+y` frame needs that rule added to `CameraPixels` (it changes nothing for any direction given to it today; Soft links the file).
- Engine docs (0.119.0): `CreateTexture(ReadOnlySpan<byte>, w, h, TextureFormat, SamplerDescription?, bool generateMipmaps)`, default sampler `PixelArt` (nearest, Repeat); `TextureFormat.Rgba8Srgb` decodes on sample; `Material.LitTextured(texture)` lights from per-vertex normals; `Destroy(Texture)`. The docs say the shader multiplies the per-vertex colour into the base colour; the probe confirms it for `LitTextured`.

**Step 0, the probe** (a `regression` test in `tests/Carve.Server.Tests`, kept): `define_mesh` the unit cube, `set_uvs` on its +Z face so (0,0) is its top-left as seen, render `view +z`, `mode: "checker"`. Assert the top-left cell white, the top-right violet, the bottom-left (row 7, col 0, odd) dark red, and one unmapped face magenta. If the texture does not draw, or vertex colours are ignored under `LitTextured`, stop: an engine need (D-1 table, `CLAUDE.md`). If it draws v-flipped, stop and ask (glTF axes are settled).

**Files:**

1. **`src/Carve.Server/CheckerTexture.cs` (new, CPU, no engine handle):** `const Size = 256, Cells = 8, CellPixels = 32`; `Hues` (the eight); `Cell(int c, int r)`: `(0,0)` → `FFFFFF`; `c + r` even → hue; odd → each channel `(x * 55 + 50) / 100` in integers (= round(x · 0.55); no channel lands on .5), giving `#7B2323 #7B4C1A #776E1A #2C612C #23616A #233E7B #3A2C72 #612C6A`. `Pixels`: RGBA8, A = 255, row 0 first = v 0 (top), built once. `WhiteCentre = (16/256, 16/256)`. A unit test pins its SHA-256.
2. **`src/Carve.Core/Model.cs` (`Combine`):** each mesh's `Uvs` offset into the combined mesh (face ids by the faces before it, seams by the vertices before it), attached with `.WithUvs(...)` after the style. No mesh with UVs → null, so a combined mesh without UVs is as now.
3. **`LookMeshes.cs`:** `Look.Checker`. `LookScene` gains `Vector2[]? Uvs` (one per triangle vertex, A, C, B). `Compose` for `Checker`: `FlatColour` null; `Uvs` the corner UVs, an unmapped face's corners all at `WhiteCentre`; `VertexColours` always: white for mapped, magenta `(1, 0, 1)` for unmapped, highlighted faces orange *(open rule 3)*; `EdgeLines` null; `OrangeLines` as `shaded`. So unmapped = white texel × magenta = lit magenta, in one mesh and one material. Face colours are not drawn *(open rule 4)*. Every other look's `LookScene` is unchanged (`Uvs` null).
4. **`MeshBridge.cs`:** `ToMeshData(mesh, space, Vector2[]? uvs = null)` writes `Uv` only when given; with null, byte for byte today's buffer.
5. **`RenderSystem.cs`:** `Make` for `Checker` uses `ToMeshData(mesh, space, scene.Uvs)` (never the submeshed path) and the checker material. `Texture? _checker`, `Material? _checkerMaterial`, made lazily on the frame thread at the first checker look: `resources.CreateTexture(CheckerTexture.Pixels.Span, 256, 256, TextureFormat.Rgba8Srgb, SamplerDescriptions.PixelArt, generateMipmaps: false)` and `Material.LitTextured(_checker)`, held for the process's life (as the cameras and light are; never per frame). `internal Texture Checker` (the lazy getter) is for T-58. A render that never asks for the checker makes nothing.
6. **`RenderTool.cs`:** `mode` accepts `"checker"` (validation, `AllowedValues`, error "use 'shaded', 'wireframe', 'normals' or 'checker'", both description lists, the error list); `"checker" => Look.Checker`; `weights` with `checker` refused as with wireframe *(open rule 6)*. `AddLookParts` already writes a non-shaded look's name: `512x512, view front, checker, 72 vertices, 70 faces`. When no drawn face has UVs, `no face has UVs` follows `checker` *(open rule 5)*, counted in `RenderTool` over the drawn mesh (the named mesh, else the combined one).
7. **Description (content)**, in the tool's text and `mode`'s: "'checker' draws the model lit with a 256x256 checker of 8x8 cells at each face's corner UVs (set_uvs, project_uvs, unwrap). Each column has its own hue, red at u = 0 through orange, yellow, green, cyan, blue and indigo to violet at u = 1; light and dark cells alternate, and the cell at uv (0,0), the texture's top-left, is white. Stretched or squashed cells mean stretched UVs; hues running violet to red along u, or the white cell at the wrong corner of an island, mean it is mirrored or turned; magenta is a face with no UVs. UVs outside 0 to 1 repeat the checker. Example: a cube face given [[0,0],[1,0],[1,1],[0,1]] from its top-left corner round shows the 64 cells with white at its top-left; given [[1,0],[0,0],[0,1],[1,1]] it shows violet there and white at the top-right: mirrored." Wording final at implementation.
8. **Scenario, `scenarios/Carve.Scenario.Crate/CrateChecker.cs` (new)**, run from `CrateScenario` right after `CrateLooks` *(open rule 8)*:
   - `CrateUvs.Lay(Shape)` (static, for T-58 too): each live face by its normal's dominant axis, over the crate's bounds `CrateMin..CrateMax` *(open rule 1)*, laid to read unmirrored from outside, v down: +Z `u=(x-x0)/sx, v=(y1-y)/sy`; -Z `u=(x1-x)/sx`, same v; +X `u=(z1-z)/sz`, same v; -X `u=(z-z0)/sz`, same v; +Y `u=(x-x0)/sx, v=(z-z0)/sz` (screen up is -Z); -Y `u=(x-x0)/sx, v=(z1-z)/sz`. The lid panel `TopFace` is left unmapped *(open rule 2)*. One `set_uvs` call, its counts checked.
   - Frames: `crate-checker-front`, `-back`, `-+y`, `-sheet` (2 x 2, `views` front, back, +x, +y *(open rule 7)*), `-front-256`, `-front-1024`; then `undo` and `crate-front-after-checker`, byte-equal to `crate-front`, so the later stages see the crate as before.

**What a pixel check can and cannot predict.** On a planar face whose UVs are affine in position (every crate face here), perspective-correct interpolation gives the same UV at a point as the formula, so a check **can** predict which cell a projected point sees (column hue, light/dark/white) and whether it is magenta. It **cannot** predict bytes: light, ambient, the tonemap and sRGB encode set the value, and per-channel tonemapping shifts hue a little. So checks classify, never match bytes:

- **hue:** the nearest of the eight hues must be the predicted column's *(open rule 10)*;
- **light vs dark:** on one face, a light cell's brightest channel exceeds the neighbouring dark cell's (same normal and light, monotonic tonemap);
- **white:** small channel spread, brighter than every hue cell on that face; **magenta:** G near 0, R and B close and well above it.

Sample only where `frac(8u)` and `frac(8v)` lie in 0.25..0.75, at least 3 px inside the face on screen, at cells next to each face corner and in the middle, never under a sheet tile's caption. Work each against `CrateLooks.Shape` before writing it; stop if one cannot pass (a sliver face, as the +X frame from above).

Per frame at least: front: white at the +Z side's top-left, violet at its top-right, the hue order along one row of +Z, the lid panel magenta, a light/dark pair; back: the same on -Z (its top-left as seen is the +X end); +y: white at the lid band's (x0, z0) corner, the panel magenta; sheet: white and magenta in each tile's own frame; 256 and 1024: the front's, scaled. Each white check also runs at its mirror position and must fail there, so a mirrored camera, a u-mirrored texture and a v-flipped upload each fail (the broken-product table).

**The guard.** Before the first code, hash every frame of scenario-cube, -crate, -shed, -export and -character (from `run.json`) and every exported file; after, all are identical. By construction nothing existing changes (no UV buffer, no texture made, `Combined` unchanged without UVs); the hashes prove it.

**Tests:**

- `unit`: `CheckerTexture` (size, each cell centre, white, the dark formula, pinned hash); `LookMeshes.Compose(Checker)` (UVs in A, C, B order, a holed face's hole corners, unmapped → white centre + magenta, highlight orange, no edge lines); every other look unchanged on a mesh with UVs; `ToMeshData` without UVs unchanged. `tests/Carve.Core.Tests`: `Combined` carries UVs and seams with offsets, and none when no mesh has them.
- `regression`: the probe; checker with each argument family (`view`, `direction`+`zoom`+`target`, `projection`, `size`, `views`+`columns`, `labels`, `highlight`, `mesh` on a two-mesh model with UVs on the second only, `pose`, `clip`+`time`), each asserting the caption and one predicted cell; the all-magenta mesh and its caption; `weights` + `checker` refused; the mode error text; a `shaded` frame of a mesh with UVs byte-equal to the same mesh without.

**For the dependants (As built):** the `CheckerTexture` API and hash, `RenderSystem.Checker`, the `Look.Checker` plumbing, `CrateUvs.Lay`, and each new frame's hash.

### Open rules for the person

1. **"Into the unit square" for the crate:** each side over the crate's bounds, so all +Z faces share one checker across frame and panel *(recommended)*; or each face stretched alone to the unit square (every strip shows all 64 cells); or T-55's box 3 x 2 grid (T-55 lands alongside).
2. **The unmapped face:** the lid panel, seen from front and +y *(recommended)*; or the +X bar's top; or a -Z face, seen only from the back.
3. **Highlighted faces:** the checker times orange, as `shaded` multiplies its surface *(recommended)*; or solid lit orange, hiding the checker there.
4. **Face colours in `checker`:** not drawn, the checker replaces them *(recommended)*; or the checker tinted by them.
5. **The UV note:** a caption part `no face has UVs` after `checker`, only then *(recommended)*; or also `N faces unmapped` whenever some are; or a separate text line.
6. **`weights` with `checker`:** refused, as with wireframe and normals *(recommended)*; or weights wins.
7. **The 2 x 2 sheet's views:** front, back, +x, +y *(recommended)*; or front, back, -x, +y.
8. **Where the stage runs:** after `CrateLooks`, undone after with a byte-equal front frame *(recommended; T-58 lays the UVs again with `CrateUvs.Lay`)*; or at the end of the crate's stages, UVs kept.
9. **`Model.Combined`'s carrying and T-59:** T-57 lands it as its first commit, pushed early, and T-59 rebases on it *(recommended)*; or T-59 owns it, since export needs it.
10. **Hue tolerance:** nearest of the eight, no angle bound *(recommended)*; or nearest and within ±15°; the white and magenta thresholds set from the probe frame and shown before use.
11. **The checker's colours** (the planner's, open to objection), including the eight dark values above.

### Rulings (the person, 2026-11-13)

All eleven as recommended. On rule 9 in particular: T-57 lands UVs through `Model.Combined` as its first commit, pushed early to `milestone/M-6`, and T-59, running beside it, rebases onto that commit and does not change `Combined` itself. On rule 10: the white and magenta thresholds are shown to the person, from the probe frame, before they are used.

### As built

Built by three agents, all Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-13: the first wrote the engine path, the texture and the probe; the second the argument tests, `CameraPixels`' vertical rule, the crate stage and the broken-product table; the third rebased onto T-55, T-56 and T-60, ran `verify`, the hash guard and the round-2 review, and wrote this. The probe passed: no engine need.

**How UVs reach the GPU.** `Model.Combine` (`src/Carve.Core/Model.cs`, the first commit, for T-59) offsets each mesh's `Uvs` into the combined mesh (face ids by the faces before it, seams by the vertices before it); no mesh with UVs gives null, so a combined mesh without UVs is as before. `LookMeshes.Compose` for `Look.Checker` gives `LookScene.Uvs`, one UV per triangle vertex in A, C, B order (`LookMeshes.CornerUvs`: `mesh.Uvs.Of(face)[CornerOf(v)]` over `Triangulator.Triangulate`, holes included; an unmapped face's corners all at `CheckerTexture.WhiteCentre`), and vertex colours white for mapped, magenta for unmapped, orange for highlighted (`CheckerColours`); no edge lines, face colours not drawn. `MeshBridge.ToMeshData(mesh, space, uvs?)` writes `Uv` only when given; null is byte for byte the old buffer. `RenderSystem.Make` uses that path and the checker material when `scene.Uvs` is set. So unmapped = white texel x magenta = lit magenta, one mesh, one material.

**The checker, formula and API** (`src/Carve.Server/CheckerTexture.cs`, CPU only): `Size = 256`, `Cells = 8`, `CellPixels = 32`; `Hues` `#E04040 #E08A30 #D8C830 #50B050 #40B0C0 #4070E0 #6A50D0 #B050C0` (column 0 = u 0, red, to column 7, violet); `Cell(c, r)`: (0,0) white `#FFFFFF`, `c + r` even the hue, odd each channel `(x * 55 + 50) / 100` (round(x * 0.55)): `#7B2323 #7B4C1A #776E1A #2C612C #23616A #233E7B #3A2C72 #612C6A`. `Pixels`: RGBA8, A 255, row 0 = v 0 (top), made once. `WhiteCentre = (16/256, 16/256)`. Pinned SHA-256 of `Pixels`: `27EBA66D119AAC9255DBD826EB165AE918976FF5123D226367C0CADC741B53EC`. On the GPU: `RenderSystem.Checker` (internal, lazy, frame thread) is `CreateTexture(Pixels, 256, 256, Rgba8Srgb, SamplerDescriptions.PixelArt, generateMipmaps: false)`, and the lazy `Material.LitTextured(Checker)`; both held for the process's life; a render that never asks makes neither.

**`render`.** `mode: "checker"` (`RenderTool.cs`: validation, `AllowedValues`, both descriptions with the worked example, error "use 'shaded', 'wireframe', 'normals' or 'checker'"); caption `512x512, view front, checker, ...`; `no face has UVs` follows `checker` when no face of the drawn model has UVs (counted over the mesh named by `mesh`, else the whole model; `mesh` aims only, every mesh is still drawn); `weights` with `checker` refused.

**What T-58 reuses.** `RenderSystem.Checker` (the texture, drawn again in the `uvs` layout), `CheckerTexture` (`Cell`, `Hues`, `WhiteCentre`), the `Look`/`LookScene.Uvs` plumbing and `ToMeshData(..., uvs)`; in the crate scenario `CrateUvs` (`Side`, `Normal`, `SideOf`, `Uv`, `At`, `Lay(shape, unmapped...)`) and the classifier `scenarios/Carve.Scenario.Crate/CheckerColours.cs` (thresholds approved in round 1: white spread <= 24 and least >= 120; magenta G <= 30, |R-B| <= 24, min(R,B) - G >= 100; hue nearest of eight, no angle bound). `CameraPixels` gained the `UpFor` rule (-Z up within 1 degree of vertical) and `Eye(...)`.

**Frames and their checks** (`CrateChecker.RunAsync`, after `CrateLooks`, its own crate build, every face but `TopFace` laid; then `undo`). Hashes from the verify run at `17d6073` (crate job `J-20261113-170755-8d9167`):

| Frame | SHA-256 |
|---|---|
| `crate-checker-front` | `438473c296b50fe060a520db433175c9d28c62cc65ce5cff89114699cdbf5afa` |
| `crate-checker-back` | `ac7e297efa1cf8a398c0c3cef3a8186054c1ca719ca18df1d540840256f81635` |
| `crate-checker-plusy` | `3c5327ab10cd39a339c220ec47179aa4b8299027007aba2408d7a337a71a9b81` |
| `crate-checker-sheet` (2x2: front, back, +x, +y) | `f9082d8abe21ec8f4c1fbced24a72e11aac0f8df8bf797113a19def04304de4e` |
| `crate-checker-front-256` | `5d884652877bc27d71cfcf708b442b796e47163abb548ae78911357f71cb630e` |
| `crate-checker-front-1024` | `430c46fe45fc52e24f3175e14c01006ba7e1dd28b3184260edee4876489fee6e` |
| `crate-front-after-checker` (= `crate-front`) | `bf8ecf04ae27c76c1b4d0a0ff1f12200ec7f2f4ac04ab9f97b57852b0a436924` |

Every cell centre of every face is swept: kept if facing the eye, in sight by a ray test, with a 3.5 px ring also in sight, 4 px inside the frame and clear of captions; then magenta on `TopFace`, white at (0,0), else not white, not magenta and nearest hue = its column; a light cell brighter than the dark one below it on each face. Named cells required: front +Z (0,0), (7,0), (0,7) and all of row 0; back the same on -Z; plusy +Y (0,0), (7,0), (0,7); each sheet tile its side's three. 920 samples, 0 failures, logged to `checker-samples.txt`. Regression: `CheckerLookTests` (the probe) and `CheckerArgumentTests` (each argument family with its caption and uv (0,0) white with its u-mirror and v-flip not; all-magenta with `no face has UVs`; two meshes, UVs on the second only; mode error text; `weights` refused; a shaded frame byte-equal before and after `set_uvs`).

**Rulings at round 2 (the person, 2026-11-13).** (1) The 3 px check ring may reach onto a neighbouring face in the same plane and side, where the UVs carry on unchanged (the +Z white cell at 256 px, where the frame's mitred bands meet on a diagonal through it). (2) On the 2x2 sheet, magenta is required in the front and +y tiles and required absent in the back and +x tiles (the lid panel cannot be seen from either). The person approved all six frames in round 2 with no notes.

**Evidence** (`milestone/M-6` worktree, `artifacts/review/T-57/`): `broken-product.md` (v-flipped upload, u-mirrored texture, corner UVs A,B,C, unmapped white, UVs not written, `Combined` dropping UVs, the checker stage's camera mirrored and flipped: each fails; run before the T-60 rebase at `7b86ead`), `hash-guard.md` (cube, crate, shed, export and character at base `63d5d83` against the verify run: every existing frame and exported file identical; the two `export-result.txt` differ only in the job's temp path), `frames/` (the six frames and `checker-samples.txt`).
