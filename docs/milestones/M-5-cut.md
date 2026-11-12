---
description: >-
  An agent cuts into a mesh: plane cuts, loop cuts, knife paths, face merge and delete
---
# M-5 — Cut

**Status:** Active

**One-liner:** *An agent cuts into a mesh: plane cuts, loop cuts, knife paths, face merge and delete*

## What this milestone delivers

An agent cuts into a mesh: plane cuts, loop cuts and knife paths, with face merge and delete to tidy up, on a model visibly built this way.

Content: a sample model that needs cuts to exist, with a scenario showing each stage.

**Depends on:** M-18 (the person, 2026-11-09). It starts once M-18 is Done, so cuts carry hard edges and smooth shading.

## Open at planning

- What "cut" means (D-1): which of plane, loop and knife matter, and whether boolean subtraction is wanted. A true boolean would need its own milestone.
- How a knife path is specified by coordinates, with no pointer.
- What happens to selections and element ids across a cut.
- Faces with holes, moved here from M-2 (decided at M-2's planning): a face as an outer loop plus inner loops, its `define_mesh` syntax, triangulation and how the edit tools treat it. M-2's edge table leaves room for it.
- What deleting a face or vertex does to ids, which M-2 promises are never reused.

**Decided at planning (2026-11-09).** Each was decided by the person, from the planner's recommendations.

- **What "cut" means.** `plane_cut`, `loop_cut` and `knife`, with `merge_faces`, `delete` and **`bridge`** added so an opening can go right through a wall. There is no boolean subtraction; a true boolean would be a milestone of its own. One tool per operation, as D-1 decides.
- **Order.** M-18 runs first, keeping the ruling of 2026-11-06, and M-5 starts once M-18 is Done. So cuts carry T-42's hard edges and smooth shading, and T-43's `subdivide` is taught about holed faces in T-63. M-5's place relative to M-17 and M-19 was not asked.
- **Ids across a cut.** A split face keeps its id for the piece holding its first outer-loop corner, or the next corner off the cut if that one lies on it. The other pieces are appended. Every edit's report gains `split: [{face, pieces}]` and `deleted: {vertices, faces}`. The server still holds no selection, so an agent's ids stay valid apart from what `split` and `deleted` name.
- **Delete and ids.** A deleted vertex or face is a tombstone: its slot stays (`null` in `get_mesh` and the file), and its id is never reused along the current chain of edits. Counts are live counts. Render, `select`, `check` and export skip tombstones, and export compacts. There is no `compact` tool. Deleting a vertex deletes every face on it. Deleting or merging faces also deletes vertices no live face uses. Everything removed is listed in `deleted`.
- **Faces with holes.**
  - A face is an outer loop plus hole loops. In `define_mesh` and `get_mesh` it is a plain array or `{"loop": [...], "holes": [[...], ...]}`.
  - Holes are wound clockwise seen from outside, and a wrong winding is refused.
  - Triangulation bridges each hole into the outer loop, then ear clips.
  - `extrude` on a holed face makes walls on every loop (a tube), and an inset on a holed face is refused.
- **The knife.** It takes points `[[x, y, z], ...]` on the surface, within 1e-4 of the bounds diagonal, each snapped to a vertex, then an edge, then a face's interior. Consecutive points share a face. A closed path wholly inside a face cuts out an inner face and leaves the face with a hole. An off-surface point's error gives its distance and the nearest surface point, facts only.
- **The tool API.**
  - `plane_cut(point, normal, keep: both | above | below = both, fill = true, faces?)`, where `above` is the side the normal points to.
  - `loop_cut(edge: [a, b], at: [fractions from a])`. The ring crosses quads and stops at a non-quad or an open edge.
  - `knife(points, closed = false)`.
  - `merge_faces(faces)`: connected faces only, the lowest id survives, and a non-planar result is a warning.
  - `delete(vertices?, faces?)`.
  - `bridge(loops: [[v, ...], [v, ...]])`: two open loops of equal count, paired by least total length over rotations and both directions.
- **What carries.**
  - Pieces take their face's colour and groups.
  - Both pieces of a split hard edge stay hard, and new edges are soft, except a plane cut's fill border on a smooth mesh, which is hard.
  - New vertices blend weights: by fraction on an edge, or barycentrically inside a face, renormalised and capped at 4. UVs blend the same way if M-6 has landed; how seams carry goes back to the person.
- **The file version.** One new Carve file version, owned by T-63 and taken as the next free one at dispatch: 4 after T-42's 3, unless M-6's UVs took it first. It holds holed faces and the `null` tombstones. `CarveFile.Write` writes the lowest version that holds the model: this one only when the model needs it (a holed face, a tombstone), otherwise 2 or 3 byte for byte as T-42's tests require (ruled by the person, 2026-11-11).
- **The sample**, as agreed: a gatehouse wall, built by T-69, coordinates in metres.

  ```text
   front, from +Z            apex (0, 3.4)
                                 /\
                               /    \
                             /        \
                           /            \
               (-1.5,2.6)/                \(1.5,2.6)
                        |                  |
               y=2.2    |==================|  cornice (front band, +0.08)
               y=2.0    |==================|
                        |              /\  |  window: knifed front and back,
                        |  ______     |  | |  faces deleted, loops bridged:
                        | |      |    |__| |  you see through it
                        | | door |         |
                        | |brown |         |  door: open knife path from
               y=0      |_|______|_________|  the floor edge, coloured
                      -1.5 -1.1 -0.4  0.3 1.1 1.5
  ```

  1. The slab: x -1.5..1.5, y 0..4, z -0.15..0.15.
  2. `loop_cut` at y = 2.0 and 2.2, then the front band extruded out by 0.08.
  3. `merge_faces` on the back and end pieces.
  4. Two `plane_cut`s through (±1.5, 2.6) and (0, 3.4), each keeping below with fill. This removes the top face whole.
  5. `knife` along the door, (-1.1, 0) → (-1.1, 1.6) → (-0.4, 1.6) → (-0.4, 0), and the door coloured `#6b4423`.
  6. `knife`, closed, along the window, (0.3, 0.8), (1.1, 0.8), (1.1, 1.5), (0.7, 1.75), (0.3, 1.5), front and back.
  7. `delete` both window faces, then `bridge` the two loops. The mesh is closed.

  Frames are taken from directions (from the model toward the eye) `[0.6, 0.35, 1]` and `[-0.6, 0.35, -1]`, and orthographic `[0, 0, 1]` through the window, with wireframes of the stages.
- **Content with each mechanic.** Each tool task brings its own description and worked example, and a stage of its own in the new Cuts scenario (`scenario-cuts`, created by T-63). The gatehouse is the milestone's sample and needs every tool, so it is a task of its own after them, frames kept apart from code. The milestone is reached through the product's own MCP tools from a plain launch.
- **Order of the tasks:**

  | Wave | Tasks |
  |---|---|
  | 1 | T-63 (holes; owns the file version and creates the Cuts scenario) |
  | 2 | T-64 (tombstones and `delete`) |
  | 3 | T-65 (`FaceSplit` and `plane_cut`) beside T-68 (`merge_faces` and `bridge`) |
  | 4 | T-66 (`loop_cut`) beside T-67 (`knife`), both on T-65's `FaceSplit` |
  | 5 | T-69 (the gatehouse) |
  | 6 | T-70 (the acceptance run) |

  Tasks side by side share only `Program.cs`'s tool list and `CutsStages.cs`, both adds only, kept on both sides.
- **Models.**
  - Opus for T-63, T-64, T-65, T-67 and T-68: design-heavy, or deciding what others build on.
  - Sonnet for T-66, T-69 and T-70.

  T-63, T-64, T-65 and T-68 are planned for two agents each, with a hand-off point named.
- **Not worked at planning.** The gatehouse's counts and pixel checks could not be worked against the geometry, because the tools do not exist yet. T-69's first commit is a Core `GatehouseGeometry` test that works them before any frame, with a stop on any that fails. The planes, the apex and the window's fit were checked by hand.

## Required tasks

- [ ] [T-63](../tasks/T-63-faces-with-holes.md) — A face is an outer loop plus holes: define_mesh's object form, topology, triangulation, check, select, render, export and Carve file version 4
- [ ] [T-64](../tasks/T-64-tombstones-and-delete.md) — delete removes faces and vertices, leaving tombstoned slots so ids are never reused, through get_mesh, the file, render, select, check and export
- [ ] [T-65](../tasks/T-65-plane-cut.md) — plane_cut splits faces along a plane, keeping both sides or one with the cut filled, on Core's shared face-splitting step that reports each split face's pieces
- [ ] [T-66](../tasks/T-66-loop-cut.md) — loop_cut adds edge loops across a ring of quads at given fractions
- [ ] [T-67](../tasks/T-67-knife.md) — knife cuts a path of surface points through faces; a closed path inside a face cuts out an inner face and leaves a hole
- [ ] [T-68](../tasks/T-68-merge-faces-and-bridge.md) — merge_faces dissolves connected faces into one, and bridge joins two open loops with a ring of quads
- [ ] [T-69](../tasks/T-69-gatehouse-scenario.md) — The gatehouse wall built by cuts alone: cornice, gable, door and a see-through window, shown at each stage and kept as samples/gatehouse.carve
- [ ] [T-70](../tasks/T-70-cut-acceptance-run.md) — Fresh agents cut a wall into a gable with a see-through window from the descriptions alone, recovering from a knife point off the surface

## Retrospective
