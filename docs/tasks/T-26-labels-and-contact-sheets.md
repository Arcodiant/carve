---
description: >-
  render draws face and vertex ids on the image and tiles up to six captioned views into one contact sheet
---
# T-26 — Labels and contact sheets

**Status:** Done

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-25

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent reads element ids straight off the image and sees several views of a mesh in one picture, each tile captioned, so one call answers "which face is which" and "what does it look like all round".

## Scope

**Labels.** `render` gains `labels`: `none` (default), `faces`, `vertices`, `both`. Drawn by the engine's text on an **immediate surface** composed into each size's resolved target: in `RenderComposition`, after each `SceneRenderStage`, `.AddImmediate(resolved-N, context.CreateImmediateSurface(), immediateTag: "labels-N")`; `RenderSystem` reaches it through `IImmediateSurfaceRegistry.Get("labels-512")` and so on, and queues the draws in `Render` before `renderer.RenderFrame()` (the docs: `DrawText` is called from update code, and on the headless host the overlay draws into bound targets as it does windowed). The default font is the engine's embedded 8x16 bitmap; no font file.

- **Projection on the CPU**: `ScreenProjection` (new, pure) maps a drawn-space point through the frame's view and projection matrices to pixels of the requested size (pixel centres at +0.5, Y down). A unit test projects the drawn origin to the centre and a known corner to its known pixel.
- **Which labels.** A face is labelled when it faces the camera: in perspective `dot(normal, eye − centre) > 0`, in orthographic `dot(normal, direction) > 0`; a vertex when it is on at least one such face. A label whose box lies wholly off the image is skipped. A label can still land on a face hidden by nearer geometry (a concave mesh), which the description says.
- **How they look.** Text white `(1, 1, 1, 1)` at scale 1 on a backing rectangle `(0, 0, 0, 0.6)` padded 2 px (`MeasureText` sizes it), the rectangle drawn first so the text sits on it. A face label is centred on its projected centre. A vertex has a filled dot of radius 3 px in white (`DrawCircle`) and its id at `(+5, −18)` from the dot. Draw order: faces, then vertices, ascending id, so later labels sit over earlier ones.
- With `labels: none` nothing is queued and the frame is byte-identical to T-25's.

**Contact sheet.** `render` gains `views`: an array of 2 to 6 entries, each a view name (`front`, `back`, `+x`, …) or an object with `direction` or `view`, and optional `zoom` and `target`, and `columns` (1 to 3; default `min(3, views.Length)`). `views` is exclusive with the single-view arguments `view`, `direction`, `zoom`, `target`; `mode`, `labels`, `size`, `projection` and `highlight` apply to every tile. Tool errors: fewer than 2 or more than 6 views; an entry that is neither a name nor such an object, or one with both `view` and `direction`; a zoom or target out of range in an entry, named by its position in words as the edit tools do; `columns` outside 1 to 3; `views` together with a single-view argument.

- Each tile is a full render at `size` through the existing queue, posted in order; each tile's **caption** is drawn by the engine at `(4, 4)`: the name as given (`+x`) or `(x, y, z)` as given for a direction, with `zoom n` and `target (…)` appended as the single caption does, white on the backing rectangle. A single render draws no tile caption.
- **`SheetComposer`** (new, pure CPU) tiles the RGBA pixels: rows of `columns` tiles, a **2 px gutter** of the background colour's sRGB value between tiles, an unused tile filled with background. 6 views at 256 in 3 columns give 772 x 514; the composer returns the exact size, and the PNG is encoded from it.
- **Caption** of the result: `3x2 tiles of 256x256, orthographic, views front, +x, -x, +y, -y, +z, 8 vertices, 6 faces`, with the mode, labels and highlight parts where the single caption has them, e.g. `2x2 tiles of 512x512, wireframe, labels faces, views front, (2, 1, 1), 72 vertices, 70 faces`.

**The description** says what labels show and leave out (front-facing only, edges by their vertex ids, a label may sit on a hidden face), how a sheet is asked for and captioned, that six axis views orthographic at 256 is the quick all-round look, and every error. The worked example: the six-axis sheet.

**Byte-identical guard.** Every existing frame, with labels none and no views, stays byte-identical (table). If one differs, **stop and report**.

**Content, in `scenarios/Carve.Scenario.Crate`** (this task owns it in this wave):

- the crate `front` at 1024 with `labels: both`: at the projected centre of the lid's panel face the pixel is darker than that face's shade by at least 60/255 (the backing), and a white pixel exists inside its label box; at the projected position of a front corner vertex the pixel is white (the dot); at the projected centre of a back-facing face (the -X panel) there is no backing: the pixel there is the shade of what is in front, not darkened;
- the crate `direction [0.4, 2, 0.8]` at 512 with `labels: faces`: the lid panel's label present, a side's label present, a hidden face's absent, as above;
- a sheet of the six axis views, orthographic, 256, `labels: none`: the PNG is 772 x 514; the pixels at (257, 100) and (514, 100) are the gutter; each tile's centre is not background; in tile `+x` the caption box at (4, 4) holds white pixels; the tile order is as given (the `+y` tile, the lid seen head-on, is the one with no bar visible: check a bar pixel in `+x` and its absence in `+y`);
- a sheet of 4 views (`front`, `+x`, `(2, 1, 1)` zoom 2 target on the bar, `-z`), 256, columns 2: 514 x 514, the fourth tile where columns 2 puts it, with its caption `(2, 1, 1), zoom 2, target (…)`;
- one error call each for 7 views, `columns: 4`, and `views` with `view`.

**Tests:**

- `ScreenProjectionTests` and `SheetComposerTests`, `unit`: the projections above; tiling sizes, gutter positions, order and the empty tile for 2 to 6 tiles at each column count.
- `RenderTests`, `regression`: every error, the caption forms, the PNG sizes, and `labels: none` byte-identical to the same render without the argument.
- **Show each new check failing**: back-facing labels drawn, Y not flipped in the projection (the dot misplaced), tiles in the wrong order, the gutter dropped, the caption of a direction tile written as a name.

## Notes

- **Decided by the person at planning (2026-11-05):** labels for faces and vertices only, front-facing only; a sheet is `views` on `render` (no second tool); up to 3 columns with the caption top-left in each tile; the engine draws labels and tile captions, the server only tiles pixels.
- **Decided at planning on the planner's recommendation:** the label look (white on a 0.6 black backing, 3 px dot, offsets), the 2 px gutter, 2 to 6 views, the caption forms.
- **An engine risk to settle first:** a tagged immediate surface drawing into the resolved target on the headless host, per the docs (`AGENTS.md` of the graphics package: "the immediate overlay … draws there into bound targets as it does windowed"). Probe it in the first hour. If text does not reach the target, **stop and ask**: the fallback (text composed on the CPU with a font of Carve's own) is not chosen.
- Builds on T-24's record and T-25's look meshes; the design here is the label placement and the sheet, which the person has agreed. Sonnet is enough if the probe passes on the first try; otherwise the stronger model.
- **Open rules: stop and ask.** A font or size other than the embedded 8x16 at scale 1. Labels on edges. More than six views or more than three columns. Any change to the existing frames. Hidden-face labels suppressed by any means not in the Scope (a depth test on the CPU would be a new mechanism).
- **A fixer may not change** the framing, the light, the look colours or the existing frames.
- **Writes down for T-27 and T-28:** the final caption forms, the pixel geometry of a label (box size for an n-digit id), the sheet's size formula, and where the per-tile caption sits.
- **Files owned in this wave:** `RenderTool.cs`, `RenderQueue.cs`, `RenderPump.cs`, `RenderSystem.cs`, `RenderComposition.cs`, `ScreenProjection.cs`, `SheetComposer.cs`, `CrateScenario.cs`, their tests.
- Frames at three sizes and sheets fill context: look at them together at the end.
- Run `finish` from inside the worktree and leave the folder.

### As built

**Model:** built and verified on Opus 5.5 (`claude-opus-5-5`). The member moved from Sonnet 5.5 to Opus 5.5 after the probe did not pass on the first try, as the Notes require: the tagged surface does not exist until a headless host has composed, so the first `registry.Get(tag)` errored until a warm-up frame was drawn.

**Decided by the person (2026-11-07), against the Scope.** Asked with the frames (`artifacts/review/T-26/round-1/`); the person agreed with each recommendation:

- **A sheet of 6 at 256 in 3 columns is 772 x 514, not 770.** 3 x 256 + 2 x 2 is 772, and the Scope's own gutter pixels (257, 100) and (514, 100) fit only that. The number is corrected in this file, T-27 and the milestone's sketch; the 2 px gutter stands.
- **The side's label from `[0.4, 2, 0.8]` cannot darken by 60/255.** The +Z frame is unlit there at 118, and a 0.6 backing takes it to 76, a drop of 42. Both labels of that frame, and the lid at 1024, are held to the exact backing value (0.4 of the shade in linear light) within 10/255; the lid keeps its 60 as well. The side's check is stricter than the Scope's, not looser.
- **"A bar pixel in `+x` and its absence in `+y`" cannot pass.** Orthographic and head-on the `+x` tile is one shade (225), bar included, and the `+y` tile does show the bar: its ends stand about 4 px proud on both sides, as in `-y`, `+z` and `-z`. Replaced by: every tile equals its own single render pixel for pixel outside its caption, and a new sheet, `crate-sheet-axes-corner`, each axis view zoomed 3 on the crate's largest corner, where the mesh lies in a quarter that depends on the view. That Scope line above keeps its original text as the record.
- **A long tile caption is cut at the tile's edge.** A 256 tile holds 31 letters and any caption with a target is at least 35. The result's text caption carries each tile's whole.

**Readings, agreed in the same review or for the person to overrule:**

- The Scope's fourth bullet gives the fourth tile the caption `(2, 1, 1), zoom 2, target (…)`, but that entry is third in its list. The third tile is checked for it, and `-z` is checked as the fourth.
- The -X panel is four strips round the bar. From `front` the -Z strip's centre falls under face 49's own label, and from `[0.4, 2, 0.8]` the -Z frame and three strips fall under other labels, so the hidden-face check uses the strips nothing lands on: 54, 55, 56 from `front`, 55 from the tilted view.
- The sheet's caption lists each tile's whole caption, zoom and target included: `views front, +x, (2, 1, 1), zoom 2, target (1.02, 0.4, 0.5), -z`. The Scope's example shows a direction with neither. That example, `2x2 tiles of 512x512 … views front, (2, 1, 1)`, has two views in a 2x2; the description's has four.
- `columns` without `views` is range-checked and otherwise ignored: the Scope lists no error for it.
- A vertex's label counts as on the image if its dot or its id's box is, so a dot is not dropped because its id runs off the edge.
- An object entry of `views` with any other key, or with neither `view` nor `direction`, is the "not such an object" error.
- Text is placed on whole pixels (`MathF.Round`), or the 8x16 bitmap blurs; a dot stays at its exact projected point.

**The warm-up, settled.** It is lazy: `RenderSystem.DrawOverlay` draws one extra `RenderFrame()` only when a frame has an overlay and no frame has yet been drawn, so a render with no overlay never draws an extra frame. Tested by `A_first_render_with_labels_draws_them_and_what_follows_is_unmarked` and `A_first_render_may_be_a_sheet`. No engine need: the docs give this as the way (`guides/render-targets.md`).

**What changed:**

- `RenderComposition`: an `AddImmediate` after each `SceneRenderStage`, tag `LabelTag(size)`, `labels-256`, `-512`, `-1024`. `Background` is now public.
- `RenderSpec`: `LabelSet Labels = LabelSet.None`, the fifth positional field, and the init property `string? TileCaption`.
- `ScreenProjection.cs`, `SheetComposer.cs` (both as the Scope says) and `LabelLayout.cs` (new, not in the owned list): `LabelSet`, `OverlayLabel` and the pure placement, so that which labels and where is tested with no device. All three are linked into `Carve.Server.Tests`.
- `RenderSystem`: `IImmediateSurfaceRegistry`, `DrawOverlay`, and `_frame`, the last `ViewFrame`.
- `RenderTool`: `labels`, `views`, `columns`. The camera checks moved into `CameraRefusal`, so an entry of `views` gets the same words with `views: the 3rd listed entry: ` in front. `Tile` is one camera as given.
- **A file outside the owned list:** `MeshToolSupport.Ordinal` and `Absent` became public, for those error texts. `CrateLooks.cs` gained `Vertex` on `Shape` and made a few members `internal`, and `CrateLabels.cs` is new. `CLAUDE.md`'s Layout row for the crate is updated, and `EditToolTests` pins the new argument list.

**For T-27 and T-28:**

- **Single caption:** size; view or direction; `orthographic`; mode; `labels <set>`; `highlight …`; zoom; target; counts. `512x512, view front, labels both, 8 vertices, 6 faces`.
- **Sheet caption:** `<columns>x<rows> tiles of <size>x<size>`; `orthographic`; mode; `labels <set>`; `highlight …`; `views ` and each tile's caption, comma-joined; counts. `3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 72 vertices, 70 faces`.
- **Tile caption:** text top left at (4, 4), backing from (2, 2), `8n + 4` wide and 20 high for n letters; at most 31 letters show on a 256 tile. The name as given, or `(x, y, z)` as given, then `zoom n`, `target (x, y, z)`.
- **Sheet size:** `columns x size + (columns - 1) x 2` by `rows x size + (rows - 1) x 2`, `rows = ceil(views / columns)`: 772 x 514 for six at 256 in 3 columns. Tile k's top left is `((k % columns) x (size + 2), (k / columns) x (size + 2))`. Gutter and empty tiles are `(48, 53, 56, 255)`, exactly a frame's background.
- **A label's pixels:** an n-digit id's text is `8n x 16`, its backing `(8n + 4) x 20`, 2 px round the text. A face's text top left is `round(centre - (4n, 8))`; a vertex's is `round(dot + (5, -18))`, and its dot is radius 3 about the projected point, on the continuous scale where pixel k covers k to k + 1.
- **Colours as seen:** text and dots `(255, 255, 255)`. A backing gives 0.4 of the shade in linear light: 216 reads 143, 234 reads 156, 190 about 124, 118 reads 76, background `(48, 53, 56)` reads about 31. A white pixel is 250 or more on each channel; nothing under a backing reaches that. The image stays opaque.
- **Helpers:** `CrateLabels.cs` has `IdBox`, `CheckFaceLabel` (white in the box, the exact backing at the centre pixel), `CheckUnmarked` (same as the unlabelled frame), `CheckTile`, `Backed`, `TileAt`, and `Letter`, which reads a caption letter cell by cell. `Shape.Vertex` finds a vertex by position. They are internal to the crate scenario: T-27 copies what it needs or moves them.
- **The centre pixel of a face label can be a letter's.** `CheckFaceLabel` fails on that rather than pass it, so a face id with a stroke at its middle needs another sample.
- **Check a label's place against the others first:** a nearer face's label can sit on the point. Face labels on the crate overlap, and later ones cover earlier ones as the Scope says.
- **Run folders:** `cairn test artifacts <suite>` returns the last *queued* job's frames. After a `cairn test run` on this machine they are in `artifacts/scenarios/<name>/<n>/`, and the copy `artifacts` gives is stale. This cost one false guard here, redone. A job id works for a queued run.

**Byte-identical guard: no frame differs.** Baseline taken `--fresh` before any code change: 35 frames, T-25's 29 and its six new frames, all equal to T-25's tables, which stand unchanged. After the change cube 20 of 20 and crate 15 of 15 are identical by hash, on `verify`'s frames (`artifacts/review/T-26/baseline/baseline.md5`, `verify/verify.md5`). The overlay stage with nothing queued changes no byte; that was first checked on its own, before any label was drawn.

**New frames.** The five that show something new were opened together (`artifacts/review/T-26/round-1/`), by me and by the person, and equal `verify`'s by hash. The 13 reference frames were not opened; `CheckTile` compares six of them and three of the 256 ones with the sheets pixel for pixel.

| Frame | md5 |
|---|---|
| crate-labels-both-front-1024 | `fba307ff5ace849f7d89f06b21ca7ed2` |
| crate-labels-faces-dir-0.4-2-0.8 | `191d89a7683fe1f5a6115a95a7c8cca2` |
| crate-sheet-axes | `ab79b075b0a2b8a8c464f89461e747b8` |
| crate-sheet-axes-corner | `2154ed95ae144327d8e092bd61a6edb5` |
| crate-sheet-four | `1278e7576097b92794b48a41c8658385` |
| crate-front-1024 | `db55a8395570587c11ce4e1e4a9ac61b` |
| crate-front-256 | `3f5ed7cf24a91f20e357f80597e30b1f` |
| crate-plusx-256 | `79f38ec405459ebc4ed68f96972acb70` |
| crate-minusz-256 | `377ffeb6a00610dda814125a779fbc69` |
| crate-dir-2-1-1-zoom2-256 | `54215c655304fb1a535b8dc2fe86a657` |
| crate-axis-plusx-ortho-256, -minusx- | `41b134c2661add1ed4f631c39eec023c` |
| crate-axis-plusy-ortho-256, -minusy- | `40043c2f953fc15a5d728a087be6b31f` |
| crate-axis-plusz-ortho-256, -minusz- | `e2b9ad4f6a8a06b73bbc03b68fb68f6c` |
| crate-labels-none-front, crate-front-after-labels | `a3a0ce4968b751ac43ce9f9780edb576` (equal to `crate-front`) |

The pairs share a hash because the crate is the same from opposite axes, which is why the axes sheet alone cannot tell a camera.

**What each frame's wrong-camera check is:**

| Frame | Positive check | What a wrong camera fails |
|---|---|---|
| `crate-labels-both-front-1024` | label, dot and id at projected points, nothing on hidden strips | the dot if mirrored, the lid's label if flipped |
| `crate-labels-faces-dir-0.4-2-0.8` | lid and +Z labels, a hidden strip unmarked | the +Z label if mirrored, the lid's if flipped |
| `crate-sheet-axes` | size, gutters, centres, captions, each tile equal to its render | nothing: the crate head-on is the same mirrored and flipped; `crate-sheet-axes-corner` is its check |
| `crate-sheet-axes-corner` (added) | size, caption, the mesh in one quarter of each tile | that quarter is background, either way |
| `crate-sheet-four` | size, caption, each tile equal to its render, the direction tile's letters | the shade order at the projected points of the front tile, either way |

**Each new check failing on a broken product.** Each break was made, built, run through `scenario-crate` and reverted; the whole log is `artifacts/review/T-26/breaks.log`. The scenario stops at its first failure, so a check further on is shown by a break that reaches it.

| The break | The check that failed |
|---|---|
| back-facing labels drawn, labels both | `crate-labels-both-front-1024`: the -X strip 54's centre; unit: the front view's faces and vertices, and off-image skipping |
| back-facing labels drawn, labels faces | `crate-labels-faces-dir-0.4-2-0.8`: the +Z frame's backing value (a label over it) |
| one back-facing label, strip 55, labels faces | `crate-labels-faces-dir-0.4-2-0.8`: strip 55's centre |
| Y not flipped in the projection | `crate-labels-both-front-1024`: the lid's label is not in its box; unit: 2 fail, among them the known corner |
| tiles in reverse order | `crate-sheet-axes`: tile `+x` against its render; unit: 15 fail |
| the same, four-tile sheet only | `crate-sheet-four`: tile `front` against its render |
| the same, corner sheet only | `crate-sheet-axes-corner`: tile `+y`'s quarter |
| gutter dropped | `crate-sheet-axes`: the PNG is 768 x 512; unit: 21 fail |
| gutter the wrong colour | `crate-sheet-axes`: the gutter at (256, 100) |
| direction tile's drawn caption written as a name | `crate-sheet-four`: letters 4 and 7 are not the two 1s |
| direction tile's listed caption written as a name | `crate-sheet-four`: the caption |
| tile caption not drawn | `crate-sheet-axes`: tile `+x`'s caption |
| tile caption in the wrong place | the same; unit: at four, four |
| single render draws a caption | regression: `A_single_render_has_no_tile_caption`. The scenario passes: it compares frames with each other, and the hash guard is what would catch this |
| labels ignored by the frame | `crate-labels-both-front-1024`: the lid's label |
| backing not drawn | the lid's centre, 216 over 216 |
| backing at half the alpha | the lid's centre, 184 where 143 is due |
| id text not drawn | the lid's label |
| dot not drawn | the dot on the corner |
| vertex id below the dot | vertex 6's id; unit: up and to the right |
| face label not centred | `crate-labels-both-front-1024`: strip 55's centre (a label moved onto it); unit: 3 fail |
| labels none drawn as faces | `crate front, labels none`: bytes differ; regression: `Labels_none_is_the_same_render_as_no_labels` |
| labelled frame not opaque | `crate-labels-both-front-1024`: alpha 200 |
| labels stay on after a labelled frame | `crate-sheet-four`: the shade order |
| a plain front frame labelled once a label has been drawn | `crate front, after the labels and sheets`: bytes differ |
| warm-up frame removed | regression: both first-render tests. The scenario passes: its first labelled frame comes after many others |
| labels left out of the caption | `crate-labels-both-front-1024`: the caption |
| rows by columns in the sheet caption | `crate-sheet-axes`: the caption |
| labels part after the highlight | regression: `The_captions_of_labels_and_sheets`. The scenario passes: no frame has both |
| labels left off a sheet's tiles | regression: `What_applies_to_a_sheet_applies_to_every_tile`. The scenario passes: its sheets have no labels |
| 7 views not refused | `render` was not a tool error |
| `columns: 4` not refused | `render` was not a tool error |
| `views` with `view` not refused | `render` was not a tool error |
| labels both mirrored / flipped | the dot on the corner / the lid's label |
| labels faces mirrored / flipped | the +Z frame's label / the lid's label |
| corner sheet mirrored / flipped | tile `+x`'s quarter, both |
| front at 256, tile and frame, mirrored / flipped | `crate-sheet-four`: the shade order, both |

"Mirrored" is the camera with its X and Z swapped, as earlier members broke it; "flipped" is rolled 180 degrees.

**Not checked:**

- These were not each seen to fail on their own, being behind another check or stopped by one first: the -X strips 55 and 56 from `front`, the other gutter pixels and later tiles of a loop, `CheckSize` on the two later sheets, `CheckTile` on the `+x`, zoomed and `-z` tiles of the four-tile sheet, the 3 other letter checks of the direction tile's caption, the tile centres of the axes sheet, `crate-sheet-axes-corner`'s background quarters.
- The hash guard itself was not run under a break; `single render draws a caption` is the one break that needs it or the regression test.
- The error tests in `RenderTests` were not run under the breaks, apart from those named above; the scenario's three error calls were.
- The 13 reference frames were compared by check and hash, not opened.
- No scenario frame shows a sheet with labels, a highlight or a mode; the regression test covers labels and mode, and nothing covers `highlight` on a sheet's pixels beyond its caption.
- `scenario-export` and `viewer-cube` are not this member's suites, and their frames were not compared.
