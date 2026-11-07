---
description: >-
  render draws face and vertex ids on the image and tiles up to six captioned views into one contact sheet
---
# T-26 — Labels and contact sheets

**Status:** In Progress

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
- **`SheetComposer`** (new, pure CPU) tiles the RGBA pixels: rows of `columns` tiles, a **2 px gutter** of the background colour's sRGB value between tiles, an unused tile filled with background. 6 views at 256 in 3 columns give 770 x 514; the composer returns the exact size, and the PNG is encoded from it.
- **Caption** of the result: `3x2 tiles of 256x256, orthographic, views front, +x, -x, +y, -y, +z, 8 vertices, 6 faces`, with the mode, labels and highlight parts where the single caption has them, e.g. `2x2 tiles of 512x512, wireframe, labels faces, views front, (2, 1, 1), 72 vertices, 70 faces`.

**The description** says what labels show and leave out (front-facing only, edges by their vertex ids, a label may sit on a hidden face), how a sheet is asked for and captioned, that six axis views orthographic at 256 is the quick all-round look, and every error. The worked example: the six-axis sheet.

**Byte-identical guard.** Every existing frame, with labels none and no views, stays byte-identical (table). If one differs, **stop and report**.

**Content, in `scenarios/Carve.Scenario.Crate`** (this task owns it in this wave):

- the crate `front` at 1024 with `labels: both`: at the projected centre of the lid's panel face the pixel is darker than that face's shade by at least 60/255 (the backing), and a white pixel exists inside its label box; at the projected position of a front corner vertex the pixel is white (the dot); at the projected centre of a back-facing face (the -X panel) there is no backing: the pixel there is the shade of what is in front, not darkened;
- the crate `direction [0.4, 2, 0.8]` at 512 with `labels: faces`: the lid panel's label present, a side's label present, a hidden face's absent, as above;
- a sheet of the six axis views, orthographic, 256, `labels: none`: the PNG is 770 x 514; the pixels at (257, 100) and (514, 100) are the gutter; each tile's centre is not background; in tile `+x` the caption box at (4, 4) holds white pixels; the tile order is as given (the `+y` tile, the lid seen head-on, is the one with no bar visible: check a bar pixel in `+x` and its absence in `+y`);
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
