---
description: >-
  An agent calls render and gets a shaded Empyrean image of the current mesh from the front or the back
---
# T-4 — Shaded render tool

**Status:** Open

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
