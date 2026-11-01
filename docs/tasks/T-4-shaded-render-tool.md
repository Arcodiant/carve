---
description: >-
  An agent calls render and gets a shaded Empyrean image of the current mesh from the front or the back
---
# T-4 — Shaded render tool

**Status:** Done

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** T-3

## Goal

An agent calls `render` and gets back a real Empyrean image of the current mesh, whose visible faces each read as a different brightness, from the front or the back.

## Scope

In `src/Carve.Server`:

- `MeshBridge`: `PolygonMesh` to `MeshData<PositionUvNormalVertex>`. Flat shading: each face's triangles get their own vertices with the Newell normal. **Reverses each triangle's winding**, because Empyrean is clockwise from outside and Carve counter-clockwise. Pure CPU, unit-tested.
- `RenderComposition : IRenderPipelineConfigurator`: 512x512 bound targets: `Rgba16Float` 1x `TransferSource` colour, 4x MSAA colour and `Depth32Float` resolved through `resolveOutput`; clear to linear `(0.03, 0.035, 0.04, 1)`; `SceneRenderStage(cameraTag: "render")`.
- `ViewFraming`: pure CPU. From the bounds and the view, a look-at eye and a perspective projection (set on `Camera.ProjectionMatrix` by hand; the headless host does not apply it) leaving about 8% margin. `front` looks from the direction `(+1.6, +1.2, +2.2)`, `back` from its negation. A flat or point-sized bounds still frames.
- `RenderSystem : ISystem`: scene `carve`, one mesh entity (`Material.LitMesh((0.72, 0.72, 0.70, 1))`), ambient 0.25, directional 0.9, tagged camera. Drains a queue of requests in `Update` on the frame thread. On a `ModelStore` version change it swaps the mesh (`Destroy` the old one) and **renders on a later `Update`**, never in the one that changed the scene.
- `render(view: "front" | "back" = "front")`: image block (`image/png`) then one text line, `512x512, view front, 8 vertices, 6 faces`. Tool errors: no mesh yet (saying to call `define_mesh`), or the engine fault from `EngineStatus` (naming Vulkan). Pixels via `IRenderTargetReadback.ReadAsync` and `ToDecodedImage`, encoded in memory; nothing is written to disk.
- **The light turns with the view**: rotate the directional light by the same transform as the camera, so `back` shows three brightnesses as `front` does. A world-fixed light leaves the far faces ambient-only.
- **The tool description, as a priority**, says what the views are, what an empty result means, and that faces wound the wrong way show as missing.

Tests in `tests/Carve.Server.Tests`: `MeshBridge` winding and normals; `ViewFraming` keeps all bounds corners inside the margin for a cube, a long box and a flat quad; on a Vulkan device, through a client, a cube's `render` in both views returns a 512x512 PNG whose corner is background and whose three sampled face pixels differ by at least 10/255 in the expected order; render before define is a tool error; replacing the mesh changes the image on the very next call; the no-device run returns a tool error rather than crashing.

Test scenarios come in T-6.

## Notes

- **Decided by the person:** the agreed sketch in the milestone's file. MCP image plus one caption line, no file path, no grid or axes, 512x512 fixed, views `front` and `back`. M-2 adds a real camera.
- **Probed on the planning machine, RTX 5070 Ti, engine 0.117.0** (`artifacts/m1-probe/`): a counter-clockwise cube draws inside-out and unlit; reversing it gives pixels byte-identical to `PrimitiveMesh.Cube`. Lighting is absent in the frame that creates the scene (the first frame is black); deferring one `Update` fixes it, and every frame is then byte-identical across frames and processes. A render costs about 100 ms and 18 KB of base64. Idle is 1.8% of one core and 169 MB.
- The probe used one world-fixed light and a single view. The turning light and `back` view are not yet seen.
- A back-face-culled open mesh shows nothing from behind. That is honest, and `define_mesh`'s warning covers it. Do not turn culling off without asking.
- The numbers above are the agreed starting look; a change that keeps the sketch is the implementer's, anything else goes back to the person.
- The engine has no normals or UV debug view; those are later milestones. If anything here needs what the engine lacks, add an engine need to D-1, do not file a request.
- Writes down for T-5 and T-6: the sampled pixel positions and the caption's form.
- Look at frames at the end, not after every change: they fill context.

### As built

Ran on Opus 5.5 (`claude-opus-5-5`). `verify` passes, and the frames were looked at through the render tests (`artifacts/render-tests/`, kept and ignored; written on each test run): cube front, cube back and pyramid match the agreed sketch. No scenario run: those arrive in T-6.

**Files** (`src/Carve.Server`): `MeshBridge`, `ViewFraming` (with `RenderView`, `ViewFrame`), `RenderComposition`, `RenderQueue`, `RenderPump`, `RenderSystem`, `RenderTool`. The tests compile `MeshBridge.cs`, `ViewFraming.cs`, `RenderQueue.cs` and `RenderPump.cs` as linked files and reference the engine package for its vertex types; the product is still not linked.

**For T-5 and T-6.**

- **Caption:** `512x512, view front, 8 vertices, 6 faces`, the second content block, always this form (`1 faces` included). The first block is the image, `image/png`, RGBA.
- **Sampled pixels, for the unit cube of `DefineMeshTool.CubeExample`** (x, y from the top left), measured on the RTX 5070 Ti, as R, G, B:
  - background: (2, 2) = about (48, 52, 56), everywhere the mesh is not.
  - `front`: top (256, 133) = (216, 216, 213); +Z (172, 302) = (190, 190, 187); +X (367, 288) = (171, 171, 169). Order top > +Z > +X.
  - `back`: -Z (172, 209) = (219, 219, 216); -X (367, 223) = (196, 196, 193); underside (256, 378) = (118, 118, 116). Order -Z > -X > underside.
  - These are the projections of the face centres, `RenderTests.PixelOf`; another mesh needs its own, from its `DrawnSpace` and `ViewFraming.Frame`.
- `RenderSystem` takes one `Update` to apply a new mesh or a new view and one more to render, so a render costs up to two 50 ms steps on a change, none on a repeat. Requests with the same view in one `Update` all render in it, unless the store moves: `RenderPump` (the sequencing, split from `RenderSystem` so it tests with no device) peeks at the queue and then reads the store before every request, in that order, so a render posted after `define_mesh` returned shows that mesh or a newer one and never the empty `_report`. A version change ends the `Update`, except that each `Update` opens by serving a head whose posted store version the shown mesh already meets, so a stream of `define_mesh` cannot starve a render: at most 2 frames (RV-2). `RenderTests` has the overlapping burst on the real `carve`, with a same-count mesh compared on the image; `RenderPumpTests` puts the calls at exact points.
- The same calls gave byte-equal PNGs within one process across a front, back, front sequence (`The_default_view_is_front_and_a_repeat_is_the_same_image`). Two processes were not compared here; T-6 does that.
- `EngineStatus` gained `WhenStopped`: completes when the frame thread ends, never throws. `RenderQueue` needs no engine service, so a tool can resolve it on a machine with no device (it carries `RenderedFrame`, which holds engine pixels).

**Readings of the spec.**

- `back` is the camera's own rotation applied to the light, literally: the light keeps its place in camera space, which is a half turn about the camera's up. In `back` the underside gets no directional light (its dot is -0.07) and is ambient-only at 118; that is the third brightness, and it is well apart from the other two. It is a different order from `front`, not a mirror of it. A mirrored light (negating the direction) would give the same three brightnesses as `front`, but is not a rotation.
- `back` is the eye at the negated direction with up still +Y, so it looks upward from below the mesh. The tool description says so.
- A fault is reported ahead of "no mesh yet": `define_mesh` cannot cure a missing device. The call waits for the engine to settle first.
- An unknown `view` is a tool error; the schema carries the `enum` and `default` too.
- A failed scene change is not an engine fault. An exception from `ShowMesh` fails every render, queued or later, with `The render failed: The mesh from define_mesh could not be put in the scene: <cause>` until a later mesh shows, and is not retried each frame, so the old mesh is never drawn under the new counts. An exception from `ApplyView` on a view change fails that request with `The back view could not be set up: <cause>`, and the next request sets the view again. Both are logged and the engine runs on. The Vulkan wording stays for what ends the frame thread, including a failure building the scene in the first `Update`.
- The view is normalised for drawing (RV-3, the decision of the person, after a check of the outcome failed review three times). `DrawnSpace.Of`, beside `MeshBridge`, takes the centre and half-diagonal of `MeshReport`'s bounds in double, and the bridge draws `(v - centre) / radius`, narrowed to float only after that. The scale is uniform, so normals stay as they are. Bounds of no size take radius 1: zero-area faces are refused, so it cannot arise from `define_mesh`, but it has a defined answer. The stored `PolygonMesh` is never changed and the agent is never told other coordinates: `define_mesh` still reports the bounds as stored.
- `ViewFraming.Frame` takes only the drawn box's corner, half-diagonal 1 about the origin, and the view. Its loop over the corners stays, since the box's shape sets the distance that puts the tightest corner on the margin, and so thin boxes and flat quads keep theirs (`ViewFramingTests`, tightest within 0.01 of the margin line, as before). Its arithmetic is single precision. There is no framing check, no framing error, no point floor and no coordinate range or limit of any kind.
- **What draws, measured on the real render path, both views:** a cube of side 1e-30, 1e-20, 1e-15, 1, 1e19, 1e30 at the origin; side 1 at (o,o,o) for o = 1e6, 2.5e6, 4e6, 1e7; side 1 at (-3598575.2,-3598575.2,3598575.2), (0,-4e6,0) and (0,5e6,5e6); and side 4e-5 at (100,100,100). All 28 images are byte for byte the unit cube's: front box x 62..437, y 91..470, 108,567 pixels; back box x 62..437, y 41..420, 108,574 pixels; no foreground on an image edge. So the measured bound is 0 and `RenderTests.A_cube_of_any_size_at_any_place_draws_like_the_unit_cube` asserts equality.
- **Far out, the picture is what float storage kept.** Each cube above stores as two distinct coordinates to an axis at an equal spacing, so it is still an exact cube (side 4e-5 at 100 stores as side about 3.8e-5), which is why the bytes match. Where floats are too coarse the mesh itself changes: side 1.3 at (1e7,0,0) stores as side 1 on X and 1.3 on Y and Z, and draws byte for byte as that box at the origin, which is a true picture and not refused (`A_mesh_coarsened_by_float_storage_draws_as_stored_and_is_not_refused`). The kernel's own checks still apply, so a side too small to survive storage is a zero-area refusal from `define_mesh`.
- **Left open:** `define_mesh` gives no warning for extreme bounds. It would need a threshold the person has not set.

**Engine need 2** in D-1: no in-memory PNG encode. `StbImageWriteSharp` is now a direct reference at the version the engine depends on, pinned in `Directory.Build.props`; `StbImageSharp` likewise, for the tests only.

**Not checked:** normalised drawing beyond cubes and boxes at the extremes, or on any device but the RTX 5070 Ti; a device lost after startup (the tool returns `FaultMessage` if the frame thread ends, by construction, untested); a mesh large enough to make `CreateMesh` slow on the frame thread, or a real `ShowMesh` or `ApplyView` exception: none is known to arise, `RenderPumpTests` uses a fake scene, and one exception injected into `ShowMesh` by a temporary patch gave the error above and a good render after the next `define_mesh`. A failure part way through creating the first mesh entity can leave that entity behind.
