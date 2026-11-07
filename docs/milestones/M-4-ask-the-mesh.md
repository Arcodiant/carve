---
description: >-
  An agent queries and selects elements, checks a mesh's health and reads normals and wireframe renders
---
# M-4 — Ask the mesh

**Status:** Active

**One-liner:** *An agent queries and selects elements, checks a mesh's health and reads normals and wireframe renders*

## What this milestone delivers

An agent asks precise questions of a mesh and acts on the answers: "select all unattached vertices", "select all faces above y=10". Selections feed the edit tools. It can read stats, check a mesh's validity, and see normals and wireframe renders.

Renders are made for an agent to read: part names and element ids drawn on the image, orthographic views, a contact sheet of several views in one image, and a size option (added in discussion, 2026-11-04).

Content: a deliberately broken sample mesh for the checks to find, and the normals and wireframe looks.

## Open at planning

- The query form: a small language, structured arguments, or named tools per query?
- Are selections held by the server, named, or returned as id lists?
- Does a render show the selection?
- Which validity checks matter to an Empyrean import.
- How the normals and wireframe modes are drawn, and whether that raises an engine need.
- How labels and a contact sheet are drawn (the engine, or composed after readback), and whether `render` grows arguments or a second tool takes them.

**Decided at planning (2026-11-05).** Each decision is the person's; where it was on the planner's recommendation it says so. Before the questions the planner probed the build and read the engine's 0.119.0 docs (no code probe was run):

- `main` no longer restores as pinned: `EmpyreanVersion` is 0.117.0 and the feed lists only 0.119.0, 0.115.x and 0.114.x, so `dotnet restore` fails with `NU1603` as an error; the last restore on the planning machine resolved 0.119.0 on an override.
- The engine draws what the new looks need, by its docs: `PositionVertex` line-list meshes draw depth-tested in the scene with no pipeline of Carve's own; `CreateMesh` takes per-vertex colours; on the headless host a tagged immediate surface draws text, rects and anti-aliased dots into a bound render target, with an embedded 8x16 bitmap font; an orthographic projection is a matrix Carve already sets by hand. So no engine need is expected; T-25 and T-26 each probe their part in their first hour and stop if the docs do not hold.
- Parts and part names are M-9's; in M-4 the labels are element ids, drawn by a mechanism M-9 can reuse.

The decisions:

- **The engine pin** moves to 0.119.0 in T-21, on which every task here depends. *Changed by the person, 2026-11-06:* T-21 now lands alone before M-3, so it is no longer a member here.
- **Query form** (recommended): one `select(kind, where)` tool with a small expression string: `kind` is `vertices`, `edges` or `faces`; `where` is comparisons on `x`, `y`, `z` (the vertex, the edge's midpoint or the face's centre), `min.*`, `max.*`, `normal.*`, `id`, `faces`, `edges`, `a`, `b`, `length`, `vertices`, `area`, and the flags `unattached`, `boundary`, `open`, `manifold`, `nonmanifold`, `non_planar`, joined by `and`, `or`, `not` and parentheses, with `all` and `none`. Comparisons are in float (B-1's lesson). A parse error names the character and lists what was expected. The grammar is T-22's.
- **Selections** (recommended): returned as id lists in the edit tools' own shape (`vertices`, `edges` as `[a, b]` pairs, `faces`), with `count` and `of`; the server holds no selection (T-11's decision stands). A render shows a selection through a `highlight` argument of the same shape.
- **Render shape** (recommended): `render` grows, no second tool. New arguments, each defaulting to today's render: `mode` (`shaded`, `wireframe`, `normals`), `labels` (`none`, `faces`, `vertices`, `both`), `size` (256, 512, 1024), `projection` (`perspective`, `orthographic`), `highlight`, and `views` (2 to 6 camera specs tiled into one image, exclusive with the single-view arguments) with `columns` (1 to 3). `view` gains the six axis names `+x`, `-x`, `+y`, `-y`, `+z`, `-z`.
- **Wireframe** (recommended): hidden-line. Faces in a flat unlit fill, linear `(0.09, 0.095, 0.10)`, every edge as a light line, linear `(0.85, 0.85, 0.85)`, each edge lifted 0.004 in drawn space along the mean of its faces' normals so visible edges win the depth test (the person chose this over a scale of 1.004 about the drawn origin in T-25, which hid edges in concave corners). Hidden edges are hidden.
- **Normals** (recommended): each face unlit in the colour `(normal + 1) / 2` (so +Y is light green, +X pinkish red, +Z light blue, a reversed face the opposite colour), plus a spike from each face's centre along its normal, 0.1 of the drawn half-diagonal long. A face wound the wrong way is culled, as in shaded.
- **Highlight** (planner's choice on the agreed shape): orange, linear `(1, 0.55, 0.1)`: faces tinted after lighting (an orange fill in wireframe, orange loop edges in normals), edges as orange lines, vertices as small orange stars of half-length 0.02.
- **Labels** (recommended): faces and vertices only, front-facing only (a vertex when it is on a front-facing face); edges are named by their vertex ids. White 8x16 text on a black backing at 0.6 alpha, a 3 px dot per vertex. A label may sit on a face hidden by nearer geometry; the description says so.
- **Health check** (recommended): a `check` tool with today's five warnings passed through plus `inside-out` (a closed, consistently wound shell with negative signed volume), `coincident-vertices` (exact float equality, no tolerance), `duplicate-face` (same vertex set), the shell count, notes (size in metres, base on y = 0, origin inside the bounds, more than one shell) and stats (triangles, area, volume when closed). T-junctions and face-face intersection are left for later.
- **Contact sheet** (recommended): up to 3 columns, rows as needed, the tile's caption top-left in white on a backing, a 2 px background gutter, empty tiles background; the engine draws every caption and the server only tiles pixels.
- **Sizes** (recommended): 256, 512 or 1024, each its own set of render targets in the one composition, drawn by a camera tagged for it, so the 512 frames do not change.
- **The broken sample** (recommended): the leaky shed, one gabled house about 4 x 3.5 x 3 m with every fault once: a missing roof slope, a flipped wall, a lifted eave corner, a stray vertex, an unmerged seam, a duplicated door face, and a detached, inside-out chimney. Committed as `samples/leaky-shed.carve` and as code in the Core tests, kept equal by a test.
- **The acceptance plant** (planner's choice): the prompt names vertices 3 and 9 as an open edge; they are not an edge, and `move`'s error says so.

**The sketches**, agreed by the person. Views are in coordinates: `front` is the eye on `(1.6, 1.2, 2.2)` from the centre, +Y up, 512 x 512, as in M-1.

```text
cube, front, wireframe (hidden-line)          cube, front, normals
+----------------------------+                +----------------------------+
|                            |                |            |  |  |         |
|          3________2        |                |          3_|__|__|2        |
|         /|       /|        |                |         /  top:   /|       |
|        / |  3   / |        |                |        / (0.5,1,0.5)|      |
|      7/__|____6/  |        |                |      7/__________6/ |--    |
|      |   | 1  |   |        |                |      |            |  |--   |
|      | 0 |____|___|1       |                |      |  +Z face   |+X|--   |
|      |  /     |  / hidden  |                |   -- |(0.5,0.5,1) |   1    |
|      | /  fill| /  edges   |                |   -- |            | /      |
|      |/_______|/   not     |                |   -- |____________|/       |
|      4        5    drawn   |                |      4            5        |
|  edges light grey, faces   |                |  spikes: light lines from  |
|  flat dark fill, no light  |                |  each face centre          |
+----------------------------+                +----------------------------+
512x512, view front, wireframe, 8 vertices, 6 faces    512x512, view front, normals, 8 vertices, 6 faces

cube, front, shaded, labels both              the leaky shed, front, labels faces
+----------------------------+                +----------------------------+
|                            |                |        chimney (inside-out, |
|          3o_______2o       |                |        detached) [ ]        |
|          /         /|      |                |          ____/\____         |
|         /    f3   / |      |                |  missing /    f9   \        |
|       7o_______6o   |      |                |  roof   /__________ \_      |
|       |         |   |      |                |  face  |     f2      |\     |
|       |   f1    |f5 |1o    |                |   -->  | door f4=f11 | | f6 |
|       |         |  /       |                |        |  (dup)  [|] | |flip|
|       |         | /        |                |        |__________|__|/     |
|       4o_______5o          |                |  stray vertex 41 .          |
|  ids in white 8x16 text    |                |  (ids illustrative; T-23    |
|  on a dark backing, a 3 px |                |   fixes them)               |
|  dot per vertex            |                |                            |
+----------------------------+                +----------------------------+
512x512, view front, labels both, 8 vertices, 6 faces  512x512, view front, labels faces, V vertices, F faces

cube, six axis views, orthographic, size 256: 770 x 514
+-------------+-------------+-------------+
|front        |+x           |-x           |
|             |             |             |
|    _.-'-._  |  +-------+  |  +-------+  |
|   |'-._.-'| |  |       |  |  |       |  |
|   |   |   | |  |  +X   |  |  |  -X   |  |
|    '-.|.-'  |  |       |  |  |       |  |
|             |  +-------+  |  +-------+  |
+-------------+-------------+-------------+   2 px gutter, background colour
|+y           |-y           |+z           |
|             |             |             |
|  +-------+  |  +-------+  |  +-------+  |
|  |       |  |  |       |  |  |       |  |
|  |  top  |  |  | under |  |  |  +Z   |  |
|  |       |  |  |       |  |  |       |  |
|  +-------+  |  +-------+  |  +-------+  |
+-------------+-------------+-------------+
3x2 tiles of 256x256, orthographic, views front, +x, -x, +y, -y, +z, 8 vertices, 6 faces
```

**Order.**

1. **Wave 1:** T-21 (the engine pin), already landed ahead of M-3.
2. **Wave 2:** T-22 (`select`), T-23 (`check` and the shed) and T-24 (sizes, orthographic, axis views, the `RenderSpec` record) side by side. T-22 and T-23 each add one line to `Program.cs`'s tool list; whoever lands second keeps both. T-24 owns every render file and the cube scenario.
3. **Wave 3:** T-25 (wireframe, normals, highlight) on T-24, then T-26 (labels, sheets) on T-25: the three render tasks share files, so they run in sequence while wave 2's other tasks finish. Each carries its own frames into the existing scenarios.
4. **Wave 4:** T-27 (the shed scenario and its suite) on T-22, T-23 and T-26.
5. **Wave 5:** T-28 (the acceptance run).

Eight tasks, at the top of the planning rule's range: the sheet, labels, sizes and orthographic views were added to the milestone in discussion, and the render work splits into three tasks of about sixty calls each rather than one.

**M-3 lands first.** T-23 writes the sample with `CarveFile` and T-27 and T-28 open it with `open`, both M-3's. If M-3 has not merged when M-4 starts, T-23 stops and asks.

**Models.** The stronger model on T-22 (the grammar is a public interface), T-23 (the shed's ids and `check`'s shape are built on), T-24 (`RenderSpec` and the composition) and T-25 (it meets the engine where the docs stop); Sonnet on T-21, T-27 and T-28, and on T-26 if its first-hour probe passes.

**From the M-1, M-2 and M-3 plans, carried into every task:**

- a named list of open rules, with a stop;
- a positive pixel check on every frame, and one a mirrored or flipped camera would fail (the crate is symmetric about x = 0.5; the shed is not);
- each new check shown failing, given as a table;
- what a fixer may not change;
- members run `finish` inside their worktree and leave the folder;
- a planted mistake no description can foresee, and a final `get_mesh` asked for in the prompt;
- the byte-identical guard on every existing frame, checked by hash before and after each render task.

Nothing in M-4 has sound or motion, so there is no listening or real-time pass.

**Added 2026-11-06.** M-16 to M-19 run before this milestone, for Orcward (the person). T-24 and T-25 will rebase onto the `render` arguments those milestones add.

## Required tasks

- [ ] [T-22](../tasks/T-22-select-tool-and-query-language.md) — An agent selects vertices, edges or faces with select and a small where-expression, and gets ids the edit tools take
- [ ] [T-23](../tasks/T-23-health-check-and-the-leaky-shed.md) — An agent checks a mesh with check (inside-out shells, coincident vertices, duplicate faces, shell count, stats and scale notes), shown on the leaky shed sample
- [ ] [T-24](../tasks/T-24-render-sizes-orthographic-and-axis-views.md) — render takes a size of 256, 512 or 1024, an orthographic projection and the six axis views, with the 512 frames unchanged
- [ ] [T-25](../tasks/T-25-wireframe-normals-and-highlight-looks.md) — render draws a hidden-line wireframe, a normals look of coloured faces and spikes, and highlights selected elements
- [ ] [T-26](../tasks/T-26-labels-and-contact-sheets.md) — render draws face and vertex ids on the image and tiles up to six captioned views into one contact sheet
- [ ] [T-27](../tasks/T-27-leaky-shed-scenario.md) — The shed scenario finds every planted fault with check and select, moves a selection and shows the shed in every look, in its own suite
- [ ] [T-28](../tasks/T-28-ask-the-mesh-acceptance-run.md) — Fresh agents open the leaky shed, find its faults with check and select, act on a selection and read labelled renders, recovering from a wrong edge

## Retrospective
