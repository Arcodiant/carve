---
description: >-
  render mode uvs draws a mesh's UV layout flat over the dimmed checker, with seams, labels and highlight
---
# T-58 — UV layout look

**Status:** Done

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-57

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees a mesh's UV layout flat, as the texture will be laid out: islands, seams, overlaps and empty space, with face ids it can act on.

## Scope

- **`render` with `mode: "uvs"`** draws one mesh's layout, as agreed at planning (the sketch is in M-6's file). It is an Empyrean render with an orthographic camera on a flat scene.
  - **Window:** the unit square, grown to take in the UV bounds of the mapped faces, kept square and centred, with a margin of 1/16 of the window on each side. The frame is square, at `size` (256, 512 or 1024; default 512).
  - **Ground:** the checker tiled across the whole window (repeat), dimmed to 40%.
  - **Unit square:** border in white, its corners labelled `(0,0)`, `(1,0)`, `(0,1)`, `(1,1)`. u runs right and v down, so `(0,0)` is the top-left.
  - **Faces:** each mapped face is filled with the checker at full brightness at its own UVs, so the frame shows exactly what each face samples.
  - **Edges:** every edge of a mapped face is drawn white, and marked seams are drawn yellow `#FFD800` over them.
  - **`labels`:** `faces` puts each face's id at the centroid of its UV polygon, in the existing label style. Other kinds are refused in this mode.
  - **`highlight`:** faces are filled in the highlight orange.
  - **`mesh`** is required when the model has more than one mesh.
  - **Refused in this mode**, each a tool error naming the argument: `view`, `direction`, `zoom`, `target`, `projection`, `views`, `columns`, `pose`, `clip` and `time`.
  - A mesh with no UVs is a tool error that names `set_uvs`, `project_uvs` and `unwrap`.
  - The caption is `uvs <mesh>`.
- **The description is content:** how to read the layout (bright is used, dim is empty, overlapping fills mean a shared texture area, yellow is a seam), with a worked example.
- **Frames in `scenario-crate`**, on T-57's crate UVs with one seam marked:
  - the layout at 256, 512 and 1024;
  - with face labels, and with one face highlighted;
  - one with a face's UVs set outside 0..1, so the window grows.
- **Pixel checks**, worked from the UVs given:
  - the white origin cell at the unit square's top-left, and not at its mirror (this fails a v-up layout);
  - a face's interior bright and an unused cell dim;
  - yellow on the seam's UV segment;
  - the highlighted face orange.

  Work every check before writing it, and stop if one cannot pass.
- **Tests:** `regression`: every refusal, the no-UV error, two meshes with and without `mesh`, and the sizes.

## Notes

- Opus: a new kind of frame, unlike any view of the model.
- Follows T-57 in `RenderTool.cs`, `RenderSystem.cs` and the crate look files. Runs alongside nothing else in those files.
- **Write down for the dependants:** the window rule as built, the colours, and the frame hashes. T-61 checks the barrel's layout against them.
- No sound, motion or feel: the person's look at the frames is the whole review.
- **Carried from earlier plans:** as T-57's Notes (the hash guard on every suite, a positive and a mirror-failing check per frame, a break per check as a table, `finish` read whole, artifacts by job id). Evidence goes in the milestone worktree's `artifacts/review/T-58/`.
- **Open rules: stop and ask.** Anything the milestone's sketch and *Decided at planning* do not settle, any loosened check, and any change to the agreed colours or layout.
- Run `finish` from inside the worktree and leave the folder.

### Plan

Written 2026-11-13 by the research agent (Opus 5.5) against `milestone/M-6` at `4144dbf` and T-57's branch at `508c326` (its Hand-off: texture, `Look.Checker`, `RenderSystem.Checker` and `CheckerColours.cs` done; `CrateUvs.Lay`, `CrateChecker.cs` and its frames not yet). **Start only once T-57 has merged into `milestone/M-6`**: every file marked *(T-57's)* is changed by it. Build the points marked *(rule n)* only once the person has ruled.

**How it is drawn: an Empyrean orthographic render of a flat scene, not CPU rasterising.** "Renders are Empyrean's", and the checker texture is already on the GPU; the layout is one more look, so the pump, queue, overlay and readback are reused unchanged. `Look.Uvs` replaces the model in the scene: its fill is the layout, not the mesh.

- **Layout space:** UV (u, v) sits at world `(u - cu, -(v - cv), z)`, (cu, cv) the window centre; the camera looks down -Z, up +Y, orthographic, so u runs right and v down on the image. Layers by z: ground 0, faces 0.01, white edges and the unit-square border 0.02, seams 0.03.
- **Material:** unlit textured, `new Material(MaterialFlags.Textured, Vector4.One, RenderSystem.Checker, ...)` with no `Lit` (Material.md: without `Lit` the surface colour is emitted unlit), held lazily beside T-57's. Vertex colour multiplies the sample: ground dimmed *(rule 3)*, mapped faces white, highlighted faces orange *(rule 4)*. The target is linear `Rgba16Float` and `ToDecodedImage` encodes sRGB, so an unlit face pixel should be its texel's byte to half-float rounding. If unlit textured or the vertex-colour multiply does not draw, stop: an engine need.
- **Ground:** one quad over the whole window, its UVs the window's corners, so the `PixelArt` sampler (Repeat) tiles it and the ground under a face shows the very texel the face does, dimmed.
- **Faces:** `Triangulator.Triangulate(mesh)` over the mesh's face range, each corner at its own UV (as T-57's `CornerUvs`, holes included), texcoord = UV. Each triangle is wound toward the camera by its UV signed area, so a flipped face (T-60's sense) is drawn, not culled; a zero-area face draws nothing.
- **Lines** (`MeshData<PositionVertex>`): every edge of each mapped face at that face's corner UVs (a cut edge is drawn once per side) plus the unit square's border, white *(rule 5)*; every face edge whose vertex pair `IsSeam`, on each side, `#FFD800` (linear `(1, 0.6867, 0)`) over them. Only marked seams are yellow *(rule 6)*.
- **Window rule** (`UvWindow.Of(mesh, faces)`, pure, unit-tested): B = the unit square ∪ the bounds of every mapped face's UVs (every corner, holes too); s = max(width, height) of B; centre = B's centre; window side W = s · 8/7, so the margin is W/16 each side. With only the unit square, at 512 the square spans px 32..480 and a cell is 56 px (256: 16, 224, 28; 1024: 64, 896, 112). Pixel (x, y) samples u = cu - W/2 + (x + 0.5)·W/size, v likewise from y.
- **Overlay** (the existing immediate surface and `LabelLayout`'s backing and text style): the corner labels `(0,0)` `(1,0)` `(0,1)` `(1,1)` always, at each corner *(rule 7)*; with `labels: "faces"`, each mapped face's id *(rule 8)*, relative to the named mesh as today. Labels off the image are dropped as `LabelLayout` does.

**Files.** New `src/Carve.Server/UvLayout.cs` (window rule, layout meshes, line lists, label plan; public static for unit tests). Changed: `LookMeshes.cs` *(T-57's)*: `Look.Uvs`. `LookHolder.cs`: the look key also holds the face range, and a fourth slot for seam lines, null for every other look. `RenderSystem.cs` *(T-57's)*: `Make` for `Uvs`, the unlit material, `Swap` given each slot's colour (white edges in `uvs`, grey as now otherwise), `ApplyView` framing the window with `Matrix4x4.CreateOrthographic(W, W, ..)` into a `ViewFrame`, `DrawOverlay` running for `Uvs`. `RenderQueue.cs`: `RenderSpec` carries the face range (`LabelWindow` does when a mesh is named among several; with one mesh, the whole). `RenderTool.cs` *(T-57's)*, below. The crate scenario *(T-57's files)*, below. `CLAUDE.md`'s crate row.

**`RenderTool`, mode `"uvs"`** (validation, `AllowedValues`, the error "use 'shaded', 'wireframe', 'normals', 'checker' or 'uvs'", both description lists):

- Applies: `size`; `labels` `none` or `faces` (`vertices`/`both` → "labels: 'vertices' is not drawn in mode 'uvs'; use 'faces' or 'none'"); `highlight` faces (edges and vertices *(rule 9)*); `mesh`.
- Refused, one problem each naming the argument, collected into `Failed("render", ...)`: `view`, `direction`, `zoom`, `target`, `projection`, `views`, `columns`, `pose`, `clip`, `time`, and `bones`, `bone_labels`, `weights` *(rule 10)*, e.g. "projection: not with mode 'uvs'; the layout is always flat, u right and v down". `zoom: 1` counts as absent, as `Given` already has it.
- `mesh`: required when the model has several meshes, whatever else is asked (the existing `Target` error plus "mode 'uvs' draws one mesh's layout"); optional with one.
- No mapped face → "mesh 'crate' has no UVs; give it some with set_uvs, project_uvs or unwrap". A partly mapped mesh's unmapped faces are absent *(rule 11)*.
- Caption: `512x512, uvs crate, labels faces, highlight 1 face, 72 vertices, 70 faces` *(rule 12)*.
- `mesh`'s text gains "in mode 'uvs', the one mesh drawn".

**Description (content),** in the tool text and `mode`'s: "'uvs' draws one mesh's UV layout flat, as the texture is laid out: u right, v down, (0,0) top-left, the unit square outlined in white with its corners labelled, the window grown to take in UVs outside it. The checker is tiled dimmed across the window; each mapped face is the checker at full brightness where its UVs lie, so bright is texture in use and dim is empty. Every face edge is white and a seam (mark_edges seam) yellow, on both islands it divides. Faces sharing texture overlap exactly, so an overlap shows as edges crossing inside a bright area, not as a colour (check reports overlaps). A face with no UVs is not drawn. Example: a cube after project_uvs box shows six bright squares in a 3 x 2 grid with dim gaps; labels 'faces' names each; a square with the white cell at its top-right rather than its top-left is mirrored." The Scope's "overlapping fills mean a shared texture area" cannot be seen with opaque fills at the same texels; this wording replaces it. Final at implementation, checked against the frames.

**Frames in `scenario-crate`**, `scenarios/Carve.Scenario.Crate/CrateUvLayout.cs` (new), run right after T-57's checker stage: lay the crate's UVs *(rule 1)*, mark one seam (an edge between two mapped faces on different sides), then `crate-uvs` (512), `-256`, `-1024`, `-labels`, `-highlight` (one face), `-grown` (one face's UVs set to u 1..1.5); then `undo` to no UVs and `crate-front-after-uvs`, byte-equal to `crate-front`. Pixels are predicted by the scenario's own `UvLayoutPixels.cs` (the window rule re-derived from the UVs it gave, never read from the server) and `CheckerColours.cs` *(T-57's)*.

**What a check can predict:** from the UVs given, the texel each pixel centre samples; whether it lies inside a mapped face (point-in-polygon in UV), hence bright (the texel's byte, *rule 13*) or dim (the encoded dimmed value); where an edge, seam or the border runs, to ±1 px; a highlighted face's solid orange. **What it cannot:** pixels on a face boundary or a line (MSAA coverage), a line's width, glyph pixels (only that a dark backing lies at a label's place), and anything about overlap (identical texels). Sample at least 4 px from any edge and clear of labels; check lines only where the cell beneath is not white, yellow or orange.

- Origin: the cell u, v in 0..1/8 white at the square's top-left; at its mirrors (v 7/8..1; u 7/8..1) not white: fails a v-up layout and a u-mirror.
- Bright and dim: a face interior at its texel; a ground cell (in the margin, or in the square where no face lies *(rule 1)*) dim, where bright must fail.
- Seam: yellow (`CheckerColours` gains a seam class) at the seam's UV midpoint on each side; not yellow 6 px off it.
- Highlight: orange inside the face; the checker's own byte in an unhighlighted neighbour.
- Grown: the origin cell where predicted (at 512, W = 12/7: the square's top-left at px (32, 107)), and the face at u 1..1.5 bright.
- 256 and 1024: the 512 checks scaled. Each check is worked against the UVs before it is written; stop if one cannot pass.

**Guard.** Before any code, hash every frame of scenario-cube, -crate, -shed, -export and -character from each `run.json`, and every exported file; after, all identical (the new frames are additions; `crate-front-after-uvs` proves the crate restored). By construction other looks do not change: the fourth slot is null, edge colours as now, the unlit material made only at the first `uvs` render. A broken-product table shows each new check failing: v-up layout, u-mirror, no dimming, seams not drawn, highlight ignored, window not grown, flipped face culled.

**Tests.** `unit`: `UvWindow.Of` (unit square; grown each way; non-square bounds kept square and centred; holes counted); composition (triangle UVs and positions, a flipped face wound toward the camera, ground UVs = the window, seam lines on both sides, the border); the face-label point. `regression` (real `carve`): a probe first (a quad mapped to the unit square: the origin cell white at (60, 60) and its bytes against the texel; stop if not); every refusal, one per argument; the no-UV error naming the three tools; two meshes with and without `mesh`; `labels` `vertices` refused; sizes 256, 512, 1024 by image size and caption; a `shaded` render after a `uvs` one byte-equal to one before it.

**For the dependants (As built):** the window rule as built, the colours (ground factor, white, `#FFD800`, orange), the probe's bytes, and each new frame's hash, for T-61's barrel.

### Open rules for the person

1. **The crate's UVs for the layout.** T-57's `CrateUvs.Lay` maps all six sides over the whole unit square, so every side overlaps, labels pile up, and no cell inside the square is empty. Recommended: lay the crate with `project_uvs box` for this stage (six islands in T-55's 3 x 2 grid, gaps to check dim). Or T-57's lay as the Scope says, dim checked only in the margin; or one frame of each.
2. **A barrel check here.** Recommended: no barrel frame (T-61's), but a `regression` test on `samples/barrel.carve`: the empty bottom-right dim, the side strip bright, the seam yellow near u 0.03 and 0.96 (T-56's rects). Or the barrel left wholly to T-61.
3. **"Dimmed to 40%".** Recommended 40% of the displayed value (vertex colour linear 0.133). Or 40% in linear light (reads about 66% bright).
4. **Highlight fill.** Recommended solid orange, as the Scope says. Or the checker times orange, as T-57's ruling 3 in `checker`.
5. **Edge white.** Recommended pure white, as the Scope says. Or wireframe's light grey 0.85.
6. **Which edges are yellow.** Recommended marked seams only. Or also every edge whose sides' UVs differ.
7. **Corner labels.** Recommended in the margin, diagonally off each corner. Or inside the square at each corner.
8. **Face id position.** Recommended the area centroid of the outer UV loop. Or the mean of its corners; or `FaceLabelPoint` mapped into UV (clear of holes).
9. **`highlight` edges and vertices in `uvs`.** Recommended refused ("only faces in mode 'uvs'"). Or drawn as orange lines and stars at each corner's UV (a vertex may sit in several places).
10. **Refusals beyond the Scope's ten.** Recommended `bones`, `bone_labels` and `weights` refused too. Or only the ten, the rest ignored.
11. **A partly mapped mesh.** Recommended unmapped faces absent, the caption adding `N faces unmapped`. Or absent with no note.
12. **Caption counts.** Recommended the named mesh's vertices and faces. Or the whole model's, as other modes give.
13. **Bright-pixel tolerance.** Recommended the texel's byte ±2, from the probe, shown to the person before use. Or classification only, as `checker` does.
14. **The layout's colours and window rule** (the planner's, open to objection): ground 40%, white, `#FFD800`, margin W/16, square and centred.

### Rulings (the person, 2026-11-13)

All fourteen as recommended:

- **Rule 1:** the crate stage lays its UVs with `project_uvs` `box`, not T-57's `CrateUvs.Lay`.
- **Rule 4:** a highlighted face is solid orange in this mode, as the Scope says, unlike T-57's tint.
- **Rule 11:** unmapped faces are left out of the layout, and the caption counts them.
- **Rule 13:** the bright-pixel tolerance comes from a probe frame that the person sees before it is used.
- **The rest:** as written above.

### As built

Built by four agents, all Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-13/14: the first wrote the look, the render path and the probe; the second the unit and regression tests and the crate stage; the third the schema test fix and the second hand-off; the fourth ran `verify`, the hash guard, the broken-product table and the round-2 review, fixed the corner labels and wrote this. Unlit textured drawing and the vertex-colour multiply worked: no engine need.

**`render`, `mode: "uvs"`** (`src/Carve.Server/RenderTool.cs`, `RunUvs`). Applies `size` (256, 512, 1024; default 512), `labels` `none`/`faces`, `highlight` faces, `mesh`. Errors, collected into one `Failed("render", ...)`, one problem per argument: `view`, `direction`, `zoom` (`zoom: 1` counts as absent), `target`, `projection`, `views`, `columns`, `pose`, `clip`, `time`, `bones`, `bone_labels`, `weights`, each "<arg>: not with mode 'uvs'; ..."; `labels` `vertices` or `both` "is not drawn in mode 'uvs'; use 'faces' or 'none'"; `highlight` edges or vertices refused, faces only in this mode; `mesh` required when the model has several meshes ("mode 'uvs' draws one mesh's layout"); no mapped face: "mesh '<name>' has no UVs; give it some with set_uvs, project_uvs or unwrap". A partly mapped mesh's unmapped faces are absent and counted. Caption `512x512, uvs crate, labels faces, highlight 1 face, 2 faces unmapped, 72 vertices, 70 faces`, the counts the named mesh's. The mode error lists `'shaded', 'wireframe', 'normals', 'checker' or 'uvs'`. The description (tool text and `mode`'s) is the Plan's, checked against the round-2 frames and left as written: six bright islands in a 3 x 2 grid with dim gaps after `project_uvs box`, the white cell top-left, seams yellow on both islands, the window grown for UVs outside the square.

**The drawing** (`src/Carve.Server/UvLayout.cs`; `Look.Uvs`, `LookHolder`'s fourth slot for seam lines, `RenderSpec.UvFaces`, `RenderSystem`'s orthographic layout path). An Empyrean render of a flat scene, unlit textured (`Material` without `Lit`) with `RenderSystem.Checker`. UV (u, v) at world `(u - cu, -(v - cv), z)`, camera down -Z, up +Y, orthographic `W x W`; layers ground 0, faces 0.01, white edges and border 0.02, seams 0.03. Ground: one quad over the window, its texcoords the window's corners (the sampler repeats), vertex colour sRGB 0.4 (`GroundVertex`, linear 0.133, byte 34). Faces: each mapped face triangulated at its corner UVs (holes too), vertex colour white, each triangle wound toward the camera by its signed area (a flipped face is drawn; zero area draws nothing); a highlighted face samples the checker's white centre with vertex colour orange, linear (1, 0.55, 0.1). Lines: every edge of each mapped face at that face's UVs, once per side, plus the unit square's border, white; every face edge whose vertex pair `IsSeam`, on each side, `#FFD800` (linear (1, 0.6867, 0)). Overlay (`LabelLayout.UvLabels`): the corners `(0,0)` `(1,0)` `(0,1)` `(1,1)` always, in the margin diagonally off each corner, slid inward along their margin row until whole on the frame (the margin, 16 px at 256 and 32 at 512, is narrower than the label, which was cut at the frame's edge: found before round 2, fixed in `34a108b`, unit-tested at all three sizes); with `labels: "faces"` each id at the area centroid of the outer UV loop (the mean of its corners when it has no area), relative to the named mesh.

**The window rule** (`UvWindow.Of`, unit-tested): B = the unit square and the bounds of every mapped face's UVs, every corner, holes too; s = max(width, height) of B; centre = B's centre; side W = s * 8/7, so the margin is W/16 each side. With only the unit square: at 256 the square spans px 16..240 (cell 28), at 512 32..480 (cell 56), at 1024 64..960 (cell 112). Pixel (x, y) samples u = cu - W/2 + (x + 0.5) * W/size, v likewise from y. Grown to u 1..1.5 at 512: W = 12/7, the square's top-left at px (32, 106.67).

**For T-61 (the barrel's layout).** The window rule above. The colours: dim ground = sRGB-encode(decode(texel) * 34/255), 40% of the displayed value (white reads 102); bright = the texel's byte exactly; edges `#FFFFFF`; seams `#FFD800`; highlight orange read as (255, 195..196, 90). The probe (round 1): 46 pixels, bright and dim both exact (difference 0); approved tolerance ±2 a channel. Seam pixels are partly covered by MSAA (a seam straddling two rows reads 75/25 or 50/50 yellow). **The person's ruling (2026-11-14):** seam pixels are checked by the blend fit as built, yellow `#FFD800` blended over the neighbouring colour (read 4 px either side across the line) in linear light, passing at a yellow share of 0.4 or more (somewhere in the 5 x 5 about the segment's midpoint); and not yellow 6 px off. The barrel regression test (`UvLayoutLookTests.The_barrel_shows_its_side_strip_bright_the_bottom_right_dim_and_its_seams_yellow`, rule 2) already checks `samples/barrel.carve`: the bottom right dim, each side face bright at its texel, every seam segment's midpoint at least 0.4 `#FFD800`, the seam column near u 0.03 and 0.96. Its frame at `34a108b` (local run) hashes `f022e9ecd3c06672984752c16f69fbdb66b181d6ece3878879da4dea92448766`.

**Frames and their checks** (`scenarios/Carve.Scenario.Crate/CrateUvLayout.cs`, step 5b after `CrateChecker`, with `UvLayoutPixels.cs`: the window worked from the UVs read back with `get_mesh`, never from the server). `project_uvs box`, the seam 39-36 marked (+Z face 1 to +X face 37), the frames, three undos, `crate-front-after-uvs`. A sweep every 8 px (4 at 256, 16 at 1024), 4 px clear of lines, 44 px of corner labels and clear of face labels: inside a face its texel ±2 (orange where it is the highlighted face alone), outside every face the dimmed texel ±2; the origin cell white (bright or dim) and its u and v mirrors not grey; the seam on both faces by the blend fit; the grown face bright at a cell centre. Samples logged in `uv-layout-samples.txt`. Hashes from the verify run at `34a108b` (crate job `J-20261114-012632-9f9f80`):

| Frame | SHA-256 |
|---|---|
| `crate-uvs` (512) | `961df160c7fa2b630cda8b884644936f4ff7ed57ff5dee6c5768cd7968f42675` |
| `crate-uvs-256` | `854860b96ae4fb813fa83b1b989548e51bd3013d89e68d36193340e7aa5a5f23` |
| `crate-uvs-1024` | `19a3888f719b3d85e1cafb964b80a5e610ec87b8a4a2aef5a39b1d969bb17c57` |
| `crate-uvs-labels` | `2c77db48f4a137e58c978d9655d5a05b6cde8ce92ea4ae6cffe55293005ab63a` |
| `crate-uvs-highlight` | `d927dce5ba9c18a54f191bbabc75b0e6c2bdb13dcf497909a1b85a7874971faf` |
| `crate-uvs-grown` | `427d73112ae5f7f54c150de24be164afdaa0db75021c7795bb77b49162911a2c` |
| `crate-front-after-uvs` (= `crate-front`) | `bf8ecf04ae27c76c1b4d0a0ff1f12200ec7f2f4ac04ab9f97b57852b0a436924` |

**Tests.** `unit` (`tests/Carve.Server.Tests/UvLayoutTests.cs`): the window rule, composition, lines, `LabelPoint`, `FaceLabels`, and the corner labels whole and off the square at 256, 512 and 1024. `regression` (`UvLayoutLookTests.cs`, 19): the probe, every refusal, refusals collected, `zoom: 1`, labels and highlight kinds, the no-UV error, the part-mapped caption, two meshes with and without `mesh`, sizes, a shaded frame byte-equal before and after a layout, the barrel.

**Review.** Round 1 (2026-11-13): the probe and the ±2 tolerance, approved. Round 2 (2026-11-14, Waystone, `G:\My Drive\artifacts\carve\M-6\T-58\round-2`: the six layout frames, `crate-front-after-uvs`, the barrel): **approved**. The person asked whether `crate-front-after-uvs` is meant to be untextured: yes; it is the `shaded` front view, which draws no texture, and it shows the crate restored after the undos (byte-equal to `crate-front`).

**Evidence** (`milestone/M-6` worktree, `artifacts/review/T-58/`): `hash-guard.md` (cube, crate, shed, export, character at base `ce991c3` against the verify at `34a108b`: only the seven new crate frames and `uv-layout-samples.txt` added; the two `export-result.txt` differ by temp path only), `broken-product.md` (v-up, u-mirror, no dimming, seams not drawn, highlight ignored, window not grown, flipped face culled: each fails the crate stage and at least one layout test; run at `2792206`, raw output in `broken/`), `frames/`, `baseline/`, `after/`, `scripts/`.
