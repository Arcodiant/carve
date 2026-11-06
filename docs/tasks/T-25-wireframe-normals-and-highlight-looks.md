---
description: >-
  render draws a hidden-line wireframe, a normals look of coloured faces and spikes, and highlights selected elements
---
# T-25 — Wireframe, normals and highlight looks

**Status:** Open

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-24

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees a mesh's structure (every visible edge), its normals (each face's facing as a colour and a spike) and what a selection picked (highlighted elements), from any camera, at any of the three sizes, and today's shaded frames are unchanged.

## Scope

**Arguments.** `render` gains `mode` (`shaded`, the default; `wireframe`; `normals`) and `highlight`: an object `{"vertices":[ids],"edges":[[a,b]],"faces":[ids]}` with any of the three lists, each optional, exactly the shape `select` returns and `move` takes (`MeshToolSupport.Ids` and `Pairs`). Tool errors, the mesh untouched: a mode not listed; an unknown vertex or face id or a pair that is not an edge, listed by element as `move` does; `highlight` that is not an object. `RenderSpec` gains `Look Mode` and `Highlight` (immutable id sets, compared by value so that an equal highlight is not a view change).

**The scene: `src/Carve.Server/LookMeshes.cs`** (new; pure CPU builders, tested without a device) and `RenderSystem`:

- **Wireframe (hidden-line).** The faces are drawn as today's triangles with an unlit `Material.SolidColor` fill, `Fill` = linear `(0.09, 0.095, 0.10, 1)`; every edge of `Topology.Edges` is a `PositionVertex` line-list mesh (`resources.CreateMesh` with `MeshData<PositionVertex> { Topology = Empyrean.MeshTopology.LineList }`; note the name clash with `Carve.Core.MeshTopology`) drawn with `Material.SolidColor(Edge)`, `Edge` = linear `(0.85, 0.85, 0.85, 1)`, `CastsShadow` false. So that an edge lying on a visible face wins the depth test, the line mesh is scaled about the drawn origin by `1.004` (drawn space has half-diagonal 1); the faces are not moved. No light is involved, so the frame does not depend on the camera's light.
- **Normals.** The faces are drawn unlit (`Material.SolidColor(Vector4.One)`) with **per-vertex colours** (`resources.CreateMesh(meshData, vertexColors)`, one `Rgba8` per vertex), every vertex of a face's triangles carrying that face's colour `((n + 1) / 2, 1)` in linear RGB from its Newell normal: +Y is `(0.5, 1, 0.5)`, +X `(1, 0.5, 0.5)`, +Z `(0.5, 0.5, 1)`, and a face reversed shows the opposite colour. Plus a **spike** per face: a line from the face's drawn centre to centre + normal × `0.1` (drawn space), in `Edge`. A face wound the wrong way is culled as in shaded, so it shows as missing, which the description says.
- **Highlight**, in `Orange` = linear `(1, 0.55, 0.1, 1)`: highlighted faces get `Orange` as their per-vertex colour on the lit mesh (the engine multiplies it in after lighting, so the face stays shaded, in orange) in shaded mode, an `Orange` fill in wireframe mode, and in normals mode keep their normal colour but have their loop's edges drawn `Orange`; highlighted edges are a second line-list mesh in `Orange`, scaled as the wireframe's; highlighted vertices are small stars, three axis-aligned segments of half-length `0.02` (drawn space) through the vertex, in `Orange`. Vertices not highlighted carry white, so a mesh with no highlight is drawn exactly as before (check the bytes).
- The look meshes are rebuilt when the mesh, the mode or the highlight changes (old handles destroyed after the swap, as `ShowMesh` does); a camera change alone rebuilds nothing. The pump treats a look change as a scene change: applied on one `Update`, drawn on a later one.

**Caption.** After the projection part: `, wireframe` or `, normals` (nothing for shaded), then `, highlight 3 faces, 2 edges, 1 vertex` with only the non-zero counts; e.g. `512x512, view front, wireframe, highlight 2 faces, 72 vertices, 70 faces`.

**The description** says what each mode shows and when to use it (wireframe to count and place edges and see the face loops, normals to find a face pointing the wrong way or a non-planar face whose spike is off, highlight to see what a `select` picked before editing it), the colour code, that hidden edges are hidden, and every error.

**Byte-identical guard.** Before any code change, keep every `scenario-cube` and `scenario-crate` frame; after, every existing frame is byte-identical (table). If one differs, **stop and report**.

**Content, in `scenarios/Carve.Scenario.Crate`** (this task owns it in this wave), each frame with a positive check and one a wrong camera would fail (the crate is symmetric about x = 0.5: use a `direction` that is not, or the bars' asymmetric handles):

- the crate `front`, wireframe: a face-centre pixel is the fill (sRGB of `Fill`, ±2); a pixel on the frame's top-front edge, computed from the camera matrices, is at least 200 in green; corners background; a pixel on a hidden back edge's projection shows the fill, not the edge;
- the crate `direction [0.4, 2, 0.8]`, normals: the lid panel's pixel equals sRGB of `(0.5, 1, 0.5)` within ±2 per channel, the +Z frame's `(0.5, 0.5, 1)`, the +X frame's `(1, 0.5, 0.5)`; a spike pixel above the lid is `Edge`;
- the crate `front`, shaded, highlight of the lid's recessed panel faces, the +X bar's top edges and one bar corner vertex: the panel pixel has R − B ≥ 60 and the unhighlighted frame pixel has |R − B| ≤ 6; an edge pixel and the star's pixel are orange;
- the crate `front`, wireframe with the same highlight: the panel pixel is the orange fill;
- the cube, normals, `back`: the underside is `(0.5, 0, 0.5)`, sRGB `(188, 0, 188)` ±2;
- one error call each for `mode: "xray"` and a highlight naming face 99.

**Tests:**

- `LookMeshesTests`, `unit`: the wireframe line mesh has `2 × Edges.Count` vertices with the right positions scaled by 1.004; the spike mesh has one segment per face from centre along the normal of length 0.1; the colour of each face from its normal, exact; highlight colours only on highlighted faces; equal highlights compare equal.
- `RenderPumpTests`, `unit`: a look change is a scene change.
- `RenderTests`, `regression`: every error, every caption form, and that `mode: shaded` with an empty highlight gives the byte-identical M-1 frame.
- **Show each new check failing**: scale 1.0 (the edge check), vertex colours dropped, spike length doubled, highlight applied to every face, mode ignored.

## Notes

- **Decided by the person at planning (2026-11-05):** hidden-line wireframe over a flat fill (not see-through, not edges over shaded); normals as face colours plus spikes; the selection is shown by a `highlight` argument of ids (the server holds no selection).
- **Decided at planning on the planner's recommendation:** the colours and numbers above (`Fill`, `Edge`, `Orange`, scale `1.004`, spike `0.1`, star `0.02`); the engine's `PositionVertex` line list and per-vertex colours, found in its docs (`guides/meshes-and-vertex-layouts.md`, `guides/materials-and-instances.md`), not a pipeline of Carve's own.
- **An engine risk to settle first:** the docs say the standard shader multiplies per-vertex colours in after lighting; whether the unlit `SolidColor` path also applies them is not stated. Probe it in the first hour. If it does not, draw the normals look as one triangle mesh per colour with `Material.SolidColor`, write that down as an interim, and ask whether to record an engine need. Likewise if line lists do not draw on the headless path: **stop and ask**; the fallback (CPU-drawn lines after readback) is not chosen.
- **Runs on the stronger model**: it meets the engine where the docs stop.
- **Open rules: stop and ask.** Any colour or number not listed. Edges drawn over the shaded look. Any change to the lit material or the light. A pixel-check tolerance looser than 10/255 (the normals colours are ±2).
- **A fixer may not change** the framing, the light or `DrawnSpace`; nor the existing frames.
- **Writes down for T-26 and T-27:** `RenderSpec`'s new fields, the caption rules, the exact sRGB values of `Fill`, `Edge`, `Orange` and the six axis normal colours as seen in a frame, and the scenario's projection helper (camera matrices to pixel) that T-26 reuses for labels.
- **Files owned in this wave:** `RenderTool.cs`, `RenderQueue.cs`, `RenderPump.cs`, `RenderSystem.cs`, `LookMeshes.cs`, `CrateScenario.cs`, their tests. Not `RenderComposition.cs` (T-26 adds the overlay stage) unless a target must change, in which case say so.
- Look at the new frames together at the end.
- Run `finish` from inside the worktree and leave the folder.
