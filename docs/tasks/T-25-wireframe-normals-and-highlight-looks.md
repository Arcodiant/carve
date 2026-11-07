---
description: >-
  render draws a hidden-line wireframe, a normals look of coloured faces and spikes, and highlights selected elements
---
# T-25 — Wireframe, normals and highlight looks

**Status:** Done

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

### As built

**Model:** built and verified on Opus 5.5 (`claude-opus-5-5`).

**Decided by the person (2026-11-07), against the Scope:** the line mesh is not scaled by `1.004`. That scale pushed an edge in a concave corner into the wall beside it: on the crate the lid recess's floor edges were missing and the +Z panel's bottom edge was dotted, against the Goal's "every visible edge", with every specified pixel check passing. I asked with a crop (`artifacts/review/T-25/round-1/`) and the person chose B: each edge is lifted `0.004` in drawn space along the normalised mean of its faces' normals (`LookMeshes.LineLift`). Highlighted edges follow it. An edge between faces back to back has no mean and stays put. The Scope above keeps its original text as the record; the milestone's Wireframe decision is corrected. Checks were added for the concave edges.

**The engine risk, settled first, before anything was built:** a throwaway change to `RenderSystem`, run through `scenario-crate` and reverted. `Material.SolidColor(Vector4.One)` with per-vertex colours does apply them (lid `(188, 255, 188)`), and a `PositionVertex` line list draws on the headless path at `Edge`, `(237, 237, 237)`. So neither the interim nor the stop was needed, and there is no engine need. Frames are in `artifacts/review/T-25/probe/`.

**What changed:**

- `LookMeshes.cs` (new, linked into `Carve.Server.Tests`): `Look`, `Highlight`, `LookScene`, `LookMeshes.Compose`. `RenderSystem.PutLook` draws what `Compose` says, from `ShowMesh` and from `ApplyView` when the mode or highlight differs; a camera change alone rebuilds nothing. The line entities exist only while the look has such lines, so a shaded frame after a wireframe is byte-identical (checked).
- With no face highlighted, shaded and wireframe faces carry no vertex colours at all, so the shaded mesh is exactly the old one. With one highlighted, the wireframe's fill goes through `Rgba8`, so it reads `(85, 86, 90)` rather than `(85, 87, 89)`.
- `RenderPump` refuses a highlight the shown mesh lacks, as it does a target, since the mesh may have moved on after the call. `LookMeshes` skips such ids itself, because `ShowMesh` can meet a stale highlight.
- **A file outside the owned list:** `MeshEdits.cs` gains a public `UnknownElements`, taken out of `Move` with its text unchanged, so `move` and `highlight` cannot drift apart. T-22 had not touched that file.
- `RenderTool`'s schema has `highlight` written by hand, since a `JsonElement?` parameter generates nothing useful. `EditToolTests` pins the new argument list.

**Readings of the Scope, for the person to overrule:**

- **Caption order:** the Scope's "after the projection part" is followed literally, so the mode and highlight come before zoom and target (`256x256, view +x, orthographic, normals, highlight 1 face, zoom 2, target (1, 0.5, 0.25), 8 vertices, 6 faces`). T-24's note put new parts after the target.
- **Other keys in `highlight` are ignored**, not refused. `select` returns `kind`, `count` and `of` beside its list, and "exactly the shape `select` returns" means that object has to work as it stands (tested). The cost is that a typo such as `face` highlights nothing; the caption then shows no `highlight` part.
- A count of one is singular for all three: `1 face`, `1 edge`, `1 vertex`. Duplicates count once, and `[a, b]` equals `[b, a]`.
- A highlighted edge leaves the `Edge` line mesh, or the two would tie at one depth. So `2 x Edges.Count` holds with no highlighted edge.
- "Lid's recessed panel faces" is read as the panel and its four walls: 5 faces.
- `cube-normals-back` is in the crate scenario, as the Scope's heading says, at the very end so that no existing frame moves.

**Pixel checks against the geometry.** None had to go back, but three needed care:

- **The +X frame from `[0.4, 2, 0.8]` is a sliver.** The eye is at x = 1.067, barely off the plane of x = 1, so the frame is one to three pixels wide. It passes at ±2 on seven measured points (`PlusXFrameSeenFromAbove`), but its middle and corners cannot be sampled as other faces are, and those points were picked from a run.
- **The lid panel's own spike** stands on the image's centre column, down a pixel boundary, so no pixel of it is whole (`(214, 246, 214)`). The check uses the spike of the lid's +Z band instead.
- **From `front` the recess's near walls hide strips of the lid panel**, so `PanelSeenFromFront` lists the corners as seen.

Lines are one pixel wide and split their coverage on a slope, so `CheckLineAlong` takes the closest pixel touching the projected stretch and holds it to within 10/255 of the line's colour. That is at least as strict as the Scope's "at least 200 in green".

**For T-26 and T-27:**

- **`RenderSpec`:** `Look Mode = Look.Shaded` is the fourth positional field. `Highlight` is an init property defaulting to `Highlight.None`, not a positional one, since a default parameter must be a constant. Build one with `new RenderSpec(camera, projection, size, look) { Highlight = picked }`. `Highlight.Of(vertices, edges, faces)` sorts, de-duplicates and orders each pair; it compares by value.
- **Caption (`RenderTool.Caption`):** size; view or direction; `orthographic`; mode; `highlight ...`; zoom; target; new parts; counts last.
- **Colours as seen in a frame (sRGB):** `Fill` `(85, 87, 89)`; `Edge` `(237, 237, 237)`; `Orange` lines `(255, 196, 89)`, orange fill `(255, 195, 90)`; +X `(255, 188, 188)`, -X `(0, 188, 188)`, +Y `(188, 255, 188)`, -Y `(188, 0, 188)`, +Z `(188, 188, 255)`, -Z `(188, 188, 0)`. A highlighted lit face is the shade times `Orange`, e.g. `(216, 165, 74)` on the lid.
- **The projection helper:** `scenarios/Carve.Scenario.Crate/CameraPixels.cs`. `Perspective(min, max, direction, size, zoom, target)` returns a function from a mesh point to a pixel position, worked out independently of the product; up is +Y only, so it does not serve a near-vertical direction. `Holding` gives the pixel containing a point. Note that `CubeScenario`'s `OrthographicPixels` rounds, where this floors. `CrateLooks.cs` has `CheckFace`, `CheckPoints`, `CheckColour`, `CheckLineAlong` and the `Srgb` encoder.
- **Adding a look's entities:** `RenderSystem.AddInstance` and `Swap`/`LineSlot`. `MeshInstance` is a struct: take it with `ref var`, or a write is lost silently (this cost one run here). `RenderComposition.cs` was not touched.
- `MeshData<T>` is a struct, so `MeshData<PositionVertex>?` is a `Nullable`.

**Byte-identical guard: no frame differs.** Baseline taken `--fresh` before any code change; its hashes equal T-24's table, which stands unchanged. After the change cube 20 of 20 and crate 9 of 9 are identical by hash, on `verify`'s frames (`artifacts/review/T-25/baseline/baseline.md5`, 29 frames, `verify/`).

**New frames**, opened together on one sheet (`artifacts/review/T-25/round-1/sheet.png`; the wireframes again after the lift, `round-2/sheet.png`) and compared by hash with `verify`'s:

| Frame | md5 |
|---|---|
| crate-wireframe-front | `978a6da4b2e8924395d537610ea13772` |
| crate-normals-dir-0.4-2-0.8 | `e727fd361143d7bc38f9007e0ba6961f` |
| crate-highlight-front | `4317550e35972f4a6984f91b9a99a8a2` |
| crate-highlight-wireframe-front | `1f0580bb9aa79b98b08e84d1a9169649` |
| cube-normals-back | `95d5b2fe290d049782789fe5aefc23d6` |
| crate-front-after-looks | `a3a0ce4968b751ac43ce9f9780edb576` (equal to `crate-front`) |

**Each new check failing on a broken product.** Each break was made, built, run and reverted; the whole log is `artifacts/review/T-25/breaks.log`. The scenario stops at its first failure, so the mirrored and flipped cameras were applied to one frame at a time, leaving the frames before it alone.

| The break | The check that failed |
|---|---|
| lift 0 (the Scope's "scale 1.0") | `crate-wireframe-front`: the lid panel's far edge. The convex top-front edge still passes: it wins the tie |
| the old scale of 1.004 | the same: the lid panel's far edge |
| the top-front edge not drawn | `crate-wireframe-front`: the top-front edge at x = 0.2 |
| hidden back edge drawn in front of the faces | `crate-wireframe-front`: the hidden edge's projection is `(237, 237, 237)` |
| vertex colours dropped | `crate-normals`: the lid panel is white |
| +X colour wrong | `crate-normals`: the +X frame |
| -X colour wrong | `cube-normals-back`: the -X side |
| spike length doubled | `crate-normals`: past the tip of the spike; unit: `A_spike_stands_on_each_face_along_its_normal` was written to the same rule but not run under this break |
| spikes not drawn | `crate-normals`: the spike of the lid's +Z band |
| highlight on every face, shaded | `crate-highlight-front`: the frame's \|R - B\| |
| highlight on every face, wireframe | `crate-highlight-wireframe-front`: an unhighlighted face is orange |
| highlighted edges not drawn | `crate-highlight-front`: the bar's top edge |
| the same, in wireframe only | `crate-highlight-wireframe-front`: the bar's top edge |
| stars not drawn | `crate-highlight-front`: the star's +X arm |
| mode ignored | `crate-wireframe-front`: the caption |
| mode left out of the caption | `crate-wireframe-front`: the caption |
| highlight left out of the caption | `crate-highlight-front`: the caption |
| mode not validated | `render {"mode":"xray"}` was not a tool error |
| highlight ids not validated (tool and pump) | `render {"highlight":{"faces":[99]}}` was not a tool error |
| line entities left in the scene after a look | `crate-front-after-looks`: bytes differ |
| wireframe mirrored / flipped | `crate-wireframe-front`: the lid panel / the +Z frame |
| normals mirrored / flipped | `crate-normals`: the +Z frame / the lid panel |
| highlight mirrored / flipped | `crate-highlight-front`: the lid panel's R - B, both |
| highlight wireframe mirrored / flipped | `crate-highlight-wireframe-front`: the lid panel, both |
| cube mirrored / flipped | `cube-normals-back`: the -Z side / the underside |
| pump compares the camera only | unit: 4 fail, among them a change of mode and of highlight. The scenario was not run under this one |
| `Highlight` compared by reference | unit: 2 fail, equal highlights and the pump's |

"Mirrored" here is the camera with its X and Z swapped, as earlier members broke it: a true image mirror also reverses the winding.

**Not checked:**

- The regression tests were not run under the breaks; the scenario and the unit tests were. So the caption and error tests in `RenderTests` were not themselves seen to fail, though the scenario's caption and error checks were.
- These were not each seen to fail on their own, being behind another check in the frame or stopped by one first: the corner checks of the new frames, each face's samples after the one that failed, and the `A_look_follows_the_mesh_through_an_edit` regression test.
- T-22 landed during this member, and the rebase brought it in. `A_select_result_is_a_highlight_as_it_stands` passes a real `select` result to `render`, for faces only; `select`'s vertex and edge results were not put through.
- A highlight refused by the pump is tested with a fake scene only.
- **The Goal's "any of the three sizes":** every pixel check of a look is at 512, perspective. One caption test renders a look at 256, orthographic; no look was rendered at 1024.
- `scenario-export` and `viewer-cube` are not this member's suites, and their frames were not compared.
