---
description: >-
  render takes a size of 256, 512 or 1024, an orthographic projection and the six axis views, with the 512 frames unchanged
---
# T-24 — Render sizes, orthographic and axis views

**Status:** Done

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-21

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent asks for a smaller or larger image, a true orthographic view, or a head-on axis view by name, and today's renders come out byte for byte as before. This task also lays down the request record the two look tasks (T-25, T-26) add to.

## Scope

**The request.** `RenderSpec` (new record in `RenderQueue.cs` or its own file): `CameraSpec Camera`, `Projection Projection` (`Perspective`, `Orthographic`), `int Size`. `RenderRequest`, `RenderQueue.Post`, `RenderPump` and `IRenderScene.ApplyView` carry a `RenderSpec` in place of the bare `CameraSpec`; a change in any field is a change of view (applied on one `Update`, drawn on a later one, as now). `CameraSpec` itself is unchanged.

**Named views.** `view` accepts `front`, `back` (as now, byte-identical) and `+x`, `-x`, `+y`, `-y`, `+z`, `-z`: the head-on axis directions `(1,0,0)` and so on, with the existing up rule (`+y` and `-y` use -Z up). The caption reads `view +x`. The light turns with the camera, as for a direction.

**Orthographic.** `projection` is `perspective` (default) or `orthographic`. In `ViewFraming.Frame`, the orthographic camera keeps the eye where the perspective one puts it (outside the bounding sphere, so near and far stay positive) and builds `Matrix4x4.CreateOrthographic(2h, 2h, near, far)` with the half-height `h` the smallest at which every corner of the drawn box, seen in camera space from the target, is inside the margin: `h = max over corners of max(|seen.x|, |seen.y|) / (1 - 2 * Margin)`. `zoom` divides `h` and never moves the eye; `target` works as it does now. The caption adds `orthographic` after the view part: `512x512, view +x, orthographic, 8 vertices, 6 faces`.

**Size.** `size` is 256, 512 (default) or 1024; anything else is a tool error: `size: 300 is not a size; use 256, 512 or 1024`. `RenderComposition` declares three sets of targets (resolved, 4x multisampled colour, depth), one per size, and three `SceneRenderStage`s with camera tags `render-256`, `render-512`, `render-1024`, each clearing to the same background. `RenderSystem` holds three camera entities, one per tag, and `ApplyView` sets all three; `Render` reads back the target of the requested size. All three stages draw every frame (a small mesh costs nothing to draw three times); the 512 stage is composed exactly as today, so the 512 frames do not change. The caption's first part is the size: `256x256, view front, 8 vertices, 6 faces`. The PNG has that size.

**The description** grows by the six names (with what each shows), `projection` (that orthographic keeps parallel edges parallel and shows true proportions, so it is the one for reading dimensions and for a sheet of axis views, while perspective is what a game shows), and `size` (256 for a quick look or a sheet tile, 1024 for a dense mesh with labels, 512 the default), with every new error. The M-1 and M-2 text stays.

**Byte-identical guard.** Before any code change, run `scenario-cube` and `scenario-crate` and keep every frame; after, all existing frames are byte-identical (md5 table in the report). If one differs, **stop and report**.

**Content, in `scenarios/Carve.Scenario.Cube`** (this task owns it in this wave). Add, each with a positive pixel check and a check a wrong camera would fail:

- the cube `front` at 256 and at 1024: corners background, centre mesh, the three-shade order as in the 512 frame, the PNG and caption of that size;
- the cube `+x` orthographic: one flat face filling the margin square; the face's edge pixels lie at `round(0.08 * 512)` and `511 - round(0.08 * 512)` to within 1 px, with background 2 px outside;
- the cube `direction [1, 1, 1]` orthographic: a regular hexagon with the `(1, 1, 1)` corner at the image centre. Compute from the orthographic matrix the pixel of each of the six silhouette corners and check mesh 3 px inside each and background 3 px outside; a perspective frame fails this, since its silhouette is not regular;
- the L-block `-y` orthographic (looking up from below, -Z up): the L reads with its arms where the matrix says; sample a pixel on each arm and one in the notch, and say which pixel a mirrored camera would fail;
- one error call each for `size: 300`, `projection: "iso"` and `view: "top"`.

**Tests:**

- `ViewFramingTests`, `unit`: the orthographic frame keeps every corner of the cube, the long box and a flat quad inside the margin for the eight named views, `[1,1,1]` and `[0.4,2,0.8]`; the projected images of two parallel edges stay parallel; zoom 3 shrinks the projected size three times; no matrix holds NaN; the perspective frame for `front` and `back` is unchanged bit for bit.
- `RenderPumpTests`, `unit`: a size change or a projection change is a view change.
- `RenderTests`, `regression`: every error above, every caption form, the PNG dimensions for the three sizes, the eight named views all render without error, and `front`/`back` captions unchanged.
- **Show each new check failing**: orthographic ignored (the hexagon check), size ignored (the PNG dimension), `+y` with +Y up (NaN, a render error), the margin off by half.

## Notes

- **Decided by the person at planning (2026-11-05):** `render` grows (no second tool); sizes 256, 512 and 1024 only; the six axis names added to `view`.
- **Decided at planning on the planner's recommendation:** orthographic as a `projection` argument with the framing rule above; three target sets with tagged cameras in one composition (not keyed pipelines); caption forms above.
- **Design-heavy: `RenderSpec` and the composition are what T-25 and T-26 build on.** Run it on the stronger model.
- **Open rules: stop and ask.** Any change to the field of view, the margin, the light, the background, MSAA, or the existing frames' bytes. Any size beyond the three. A pixel-check tolerance looser than 10/255.
- **A fixer may not change** the framing, the light or `DrawnSpace` to make a frame match.
- **Writes down for T-25 and T-26:** `RenderSpec`'s shape and where a field is added, the three camera tags and target sets, how `Render` picks a target, the caption builder's rules (view, projection, then the parts T-25 and T-26 add, then the counts), and the scenario's pixel helpers.
- **Files owned in this wave:** `RenderTool.cs`, `RenderQueue.cs`, `RenderPump.cs`, `RenderSystem.cs`, `RenderComposition.cs`, `ViewFraming.cs`, `TargetCheck.cs` if needed, `CubeScenario.cs`, their tests. Not `Program.cs` (T-22 and T-23 add tools there), not `MeshBridge.cs`, not `CrateScenario.cs` (T-25 and T-26 add to it).
- Frames at three sizes fill context: look at the new frames together at the end, not after each change.
- Run `finish` from inside the worktree and leave the folder.

### As built

**Model:** built and verified on Opus 5.5 (`claude-opus-5-5`).

**Decided by the person (2026-11-07), against the Scope's list:** the L-block `-y` check could not pass as written. That L lies in the XY plane, so from -Y it is a solid 2 x 1 rectangle (measured: all 92,880 pixels of its 430 x 216 box drawn, no notch). The person chose option A: keep `-y` and -Z up, and add a second L-block lying in the XZ plane, `LBlockLying()`, a quarter turn of the first, so the winding is kept. The old L-block and its frames are untouched; the rectangle frame was dropped. Same review: the Tests line "zoom 3 shrinks the projected size three times" disagrees with the Scope's "`zoom` divides `h`", which makes the mesh three times larger on the image. I told the person I had tested the Scope's rule; their reply did not comment on it.

**What changed:**

- `RenderSpec(CameraSpec Camera, Projection Projection, int Size)` in `RenderQueue.cs`, carried by `RenderRequest.Spec`, `RenderQueue.Post`, `RenderPump` and `IRenderScene.ApplyView`. `CameraSpec` is unchanged.
- `Projection` and `ViewFraming.NamedViews` (name to direction, all eight) in `ViewFraming.cs`; `Frame` takes a `Projection` defaulting to perspective. `RenderView` stays `Front`, `Back`, since `CameraSpec.ForView` was not to change.
- `RenderComposition`: `Sizes`, `CameraTag(size)`, `Target(size)`; `RenderSystem` holds one camera per size.
- `render`: `view`'s eight names, `projection`, `size`, the description, the caption. Its first sentence had to change, since "of 512x512 pixels" is no longer always true; the rest of the M-1 and M-2 text stands.
- `size` is a `double` parameter so `512.5` gets the named error rather than a binder's bare one. The size error has no full stop, exactly as the Scope gives it; the others end in one.
- `CLAUDE.md`'s Layout row for the cube scenario. `EditToolTests` and `RenderTests` pinned the old schema (argument list, view enum); both now pin the new one exactly.

**For T-25 and T-26:**

- **Adding a field:** add it to `RenderSpec` with a default meaning today's render. The pump compares the whole record, so it is a change of view with no pump change. The record compares by reference for an array or list field, so a `highlight` list needs a value-equal type or it reads as a new view every time.
- **Tags and targets:** `render-256`, `render-512`, `render-1024`, from `RenderComposition.CameraTag(size)`. One `TargetSet` per size (resolved, 4x multisampled colour, depth), built on first use, declared `carve-resolved-<size>`, `carve-multisampled-<size>`, `carve-depth-<size>`. A new stage goes inside `Compose`'s `foreach`.
- **Picking a target:** `RenderSystem.Render` reads `composition.Target(request.Spec.Size)`. All three stages draw every frame, and `ApplyView` sets all three cameras, so the size only chooses what is read back. Asking for a target the frame did not use fails in the engine, by name.
- **Caption (`RenderTool.Caption`):** size; view or direction; `orthographic`; zoom; target; the new parts; then the counts, last. Every part but the size, view and counts appears only where it differs from the default.
- **Test sources:** `RenderQueue.cs`, `RenderPump.cs` and `ViewFraming.cs` are linked into `Carve.Server.Tests`, which has no `RenderComposition`, so they cannot name it: `RenderSpec.DefaultSize` lives on the record for that reason.
- **Scenario helpers (`CubeScenario.cs`):** `OrthographicPixels(min, max, direction, up, size, zoom, target)` gives a function from a mesh point to a pixel, worked out independently of the product. `CheckOutline` (mesh inside, background outside each corner of a convex outline), `CheckMarginSquare`, `CheckBrighter`, `CheckCaption`, `Rounded`, `AtAngle`. `ExpectErrorAsync` takes the text to expect.
- A flat axis-aligned edge lands all its partly covered pixels on one value, so at some sizes it counts as a shade in `CheckShades`: it needs 1,500 pixels of a value and a square's edge row can reach that. It stays below that in these frames (the L-block's is 860).

**Byte-identical guard: no frame differs.** Baseline taken before any code change, `--fresh` so that the frames were this machine's at this commit; they equal T-21's table. Kept in `artifacts/review/T-24/baseline/` of the milestone worktree. After the change: cube 13 of 13, crate 9 of 9 identical, by hash, on the run before the rebase and again on `verify`'s frames.

| Frame | md5 (before = after) |
|---|---|
| crate/crate-back | `262138e0bc9023f45be7801f229cdef4` |
| crate/crate-dir-0.4-2-0.8 | `a926d569da5ae3fd2e0078dea1b337e1` |
| crate/crate-dir-2-1-1-zoom3 | `9cd00c55a6ff639b48391ce34b647f24` |
| crate/crate-dir-m2.2-1.2-1.6 | `d7de54b630b8569118b642356ea56d39` |
| crate/crate-front, crate/crate-front-after-errors, crate/crate-front-again, crate/crate-front-redone | `a3a0ce4968b751ac43ce9f9780edb576` |
| crate/crate-front-undone | `df089e2b3bac74d2543dbe386754cd1b` |
| cube/after-error-front, cube/cube-flipped-front | `c5a8ee0236604c9479f6b6f86b255dba` |
| cube/cube-back | `38ea53b55779de73adaa97ad9d1428e7` |
| cube/cube-dir-0--1-0, cube/cube-dir-1-0-0 | `f7e6d57111cea6e921abec2a55b4439b` |
| cube/cube-dir-0.4-2-0.8 | `c9087eee5f9712957e0e98639c2d336a` |
| cube/cube-front, cube/cube-front-again | `20ada7cd09fce42964ea446da7edf23f` |
| cube/cube-zoom3-corner | `daa3867f11fd09ae3beb58929bae4c76` |
| cube/lblock-back | `ec371e100b97ccf1ce19dfeda6e3f959` |
| cube/lblock-dir-0--1-0-corner | `1401b6ffbb9830320fd2e830be0c1758` |
| cube/lblock-front | `0ff55d97fc2e45bb8b7a59c887f9a46b` |
| cube/longbox-front | `f8999224bcede2e93c9e71cb00c1402b` |

**New frames**, opened together on one sheet (`artifacts/review/T-24/round-2/sheet.png`) and compared by hash with `verify`'s:

| Frame | md5 |
|---|---|
| cube-dir-1-1-1-ortho-corner | `5ed8488494587bbc05a4e15c8ce78115` |
| cube-dir-1-1-1-ortho | `ced7ee1ed901c78c7f6c672147b90253` |
| cube-front-1024 | `5eb3d643aac2fffb621c9c0874da761a` |
| cube-front-256 | `9fdd7aaac256018957ca6411552062e0` |
| cube-plusx-ortho-corner | `423452e8191ba219af984f1218e0c325` |
| cube-plusx-ortho | `f7e6d57111cea6e921abec2a55b4439b` |
| lblock-lying-minusy-ortho | `7b429f34cd46c10e180d36f7ce79b83b` |

**What each frame's wrong-camera check is:**

| Frame | Positive check | What a wrong camera fails |
|---|---|---|
| `cube-front-256`, `-1024` | size, caption, corners, centre | the three-shade order at scaled points: flipped loses the top, mirrored swaps +Z and +X |
| `cube-plusx-ortho` | edges at 41 and 470, background 2 px outside, one shade | nothing: a cube head-on is the same mirrored; `cube-plusx-ortho-corner` is its check |
| `cube-plusx-ortho-corner` (added) | zoom 3 on (1, 1, 1): lower right quarter, bottom right corner | lower left quarter if mirrored, upper right if flipped |
| `cube-dir-1-1-1-ortho` | six corners from the matrix, 3 px each side | flipped: the top face is no longer brighter. Mirrored: nothing, +Z and +X differ by only 3 of 255, under the step of 10 |
| `cube-dir-1-1-1-ortho-corner` (added) | zoom 3 on (1, 1, 0): 240 degrees is the mesh | 330 degrees is filled if mirrored, 120 degrees if flipped |
| `lblock-lying-minusy-ortho` | X arm upper left, Z arm lower right, caption | the notch pixel (lower left) is filled by a mirrored camera, a camera at +Y, or +Z up |

**Each new check failing on a broken product.** Each break was made, built, run and reverted; the whole log is `artifacts/review/T-24/breaks.log`. The scenario stops at its first failure, so a check further on is shown by a break that reaches it.

| The break | The check that failed |
|---|---|
| orthographic ignored | `cube-dir-1-1-1-ortho`: 3 px inside the corner at (256, 41) is background |
| size ignored | `cube-front-256`: the PNG is 512x512 |
| `+y` with +Y up | unit: no NaN, and three other orthographic framing tests; scenario: render error, `nearPlaneDistance ('NaN')` |
| margin off by half | unit: inside the margin and touching it; scenario: `cube-plusx-ortho` shows 2 shades (the edge row now half covered). `CheckMarginSquare` comes after this, so it was not itself seen to fail |
| `ApplyView` sets only the 512 camera | `cube-front-256`: three-shade order |
| `+x` looks from -X (mirror) | `cube-plusx-ortho-corner`: lower right quarter is background |
| orthographic rolled 180 (flip) | `cube-plusx-ortho-corner`: lower right quarter is background; unit: keeps the perspective eye |
| `[1,1,1]` rolled 180 | `cube-dir-1-1-1-ortho`: the top, over +Z, 169 against 226 |
| direction reversed with a target (mirror) | `cube-dir-1-1-1-ortho-corner`: inside the wedge is background |
| `-y` looks from +Y (mirror) | `lblock-lying-minusy-ortho`: the notch pixel is the mesh; unit: the named directions |
| `-y` with +Z up (flip) | `lblock-lying-minusy-ortho`: the notch pixel is the mesh |
| size not validated | `size 300` said the engine's text, not the error |
| projection not validated | `projection iso` was not a tool error |
| view not validated | `view top` said `An error occurred invoking 'render'.` |
| pump compares the camera only | unit: a change of size, and of projection, is a change of view. The scenario passes, since every camera is set and the target is picked per request |

**Not checked:**

- The 330 and 120 degree samples of `cube-dir-1-1-1-ortho-corner` were not seen to fail: the break reversing the direction fails on the 240 sample first.
- `CheckMarginSquare` was not seen to fail, as above, though the unit test on the same rule was.
- The regression tests were not run under the breaks; the scenario and the unit tests were.
- `scenario-export` and `viewer-cube` are not this member's suites, and their frames were not compared.
- `T-22` had not landed at the rebase; `Program.cs` was not touched.
