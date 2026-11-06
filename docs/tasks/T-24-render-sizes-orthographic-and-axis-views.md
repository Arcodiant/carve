---
description: >-
  render takes a size of 256, 512 or 1024, an orthographic projection and the six axis views, with the 512 frames unchanged
---
# T-24 — Render sizes, orthographic and axis views

**Status:** Open

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
