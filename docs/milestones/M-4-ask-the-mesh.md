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
- **Health check** (recommended): a `check` tool with today's five warnings passed through plus `inside-out` (a closed, consistently wound shell with negative signed volume; *amended by the person, 2026-11-07, in RV-20:* below -1e-6 x the shell's bounds diagonal cubed, so a flat two-sided sheet is not reported), `coincident-vertices` (exact float equality, no tolerance), `duplicate-face` (same vertex set), the shell count, notes (size in metres, base on y = 0, origin inside the bounds, more than one shell) and stats (triangles, area, volume when closed). T-junctions and face-face intersection are left for later.
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

cube, six axis views, orthographic, size 256: 772 x 514
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

**As planned.**

- The order held: T-22, T-23 and T-24 side by side, then T-25, T-26, T-27 and T-28 in sequence. T-22's one rebase conflict (`Program.cs`'s tool list, against T-23) was resolved by the member as planned. No member needed a hand-off, and none failed `verify`.
- Checking the runner before the first dispatch, as M-3's retrospective asked, worked: `scenario-cube` passed through the queue on Mantorok in 8 s, and the feed had 0.119.0. Jobs then ran on both Mantorok and Xellotath with no restore failure.
- The engine held. T-25's probe found that the unlit path applies per-vertex colours and that line lists draw headless; T-26's found that the tagged immediate surface draws text into the resolved target. No engine need was recorded (D-1's *Render modes* says so).
- Every existing frame stayed byte-identical by hash through every render member: 22 at T-24, 29 at T-25, 35 at T-26, 53 at T-27 (cube and crate).
- T-27's two clean runs gave identical hashes for all 10 shed frames, and the queued `verify` run matched them.
- The person approved T-24's, T-25's, T-26's and T-27's frames, and T-28's round 2 sheet and back view.
- The milestone's `verify` passes every check but `review` (7/7 members merged, 8 suites).
- **Models:**
  - Opus 5.5 on T-22, T-23, T-24, T-25, and T-26 (moved from Sonnet, below);
  - Sonnet 5.5 on T-27 and T-28.

  No rework was traced to the cheaper tier. T-27's stops were all spec faults, each found from the geometry before any code.

**Not as planned.**

- **The order went against the person's earlier ruling.** The milestone and M-16 both recorded (2026-11-06) that M-16 to M-19 run ahead of M-4. They were still Planned; M-4 was started on the person's request of 2026-11-07, flagged in the opening table but not asked. So T-24 and T-25 rebased onto no character `render` arguments, and M-16 to M-19 will rebase onto M-4's.
- **The spec could not be met as written in eleven places.** Each was put to the person and ruled on 2026-11-07, all on the recommended option:
  - T-23: the duplicated door is also found by non-manifold and winding warnings on its four edges (`MeshReport` may not change, and any layout of a joined duplicate trips them). The shed is 4 x 3.9 x 3, not 3.5, so the chimney clears the ridge for T-27's `min.y >= 3.6`.
  - T-24: the L-block `-y` check could not pass (that L lies in XY); a second L in XZ was added.
  - T-25: the specified 1.004 wireframe scale pushed concave edges into the wall. Edges are lifted 0.004 along their faces' mean normal instead, and the milestone's Wireframe line was corrected.
  - T-26: the six-tile sheet is 772 x 514, not 770. The side label's 60/255 could not pass on the unlit +Z frame. The bar check in the `+x`/`+y` tiles could not pass and became tile-equals-its-render. Long captions are cut at the tile edge.
  - T-27:
    - `faces where normal.y > 0.5` also returns the inside-out chimney's underside (14), and both are asserted.
    - The flipped -X wall is drawn and labelled from `front`, seen from inside through the roof gap, and culled only in the `-x` tile.
    - The remaining roof slope is back-facing from `front`, so the normals frame asserts the visible faces.
    - The select steps use `y` (there is no `centre`), with the operator error at character 3.
    - The door in the `+z` tile is the wall's shade, so the tile asserts wall shade there plus the chimney and the gap.
  - M-3's rule "check a spec's pixel checks against the geometry at planning" was not applied at M-4's planning, and almost every one of these was a hand calculation away.
- **T-26 moved from Sonnet to Opus.** Its probe failed on the first try through a misread doc (a headless host composes on the first `RenderFrame()`), not an engine fault. The plan's rule ("Sonnet if the probe passes on the first try") was followed literally. The Sonnet agent's 20 calls were spent; its probe finding was handed on.
- **T-28's acceptance criterion was amended.** The agents in rounds 1 and 2 read the `move` error correctly (3 and 9 are not an edge) and declined to guess another edge, asking instead. Round 3's description told agents to substitute the best-fitting edge, and that agent avoided the plant altogether. The person ruled round 2 the accepted round, counting an understood error and a refusal to guess as recovery, and had round 3's sentence reverted. B-2 (`move`'s error should point to `select`) is filed outside M-4.
- **T-28's run conditions** needed `--mcp-config .mcp.json` beside `--strict-mcp-config`, and the prompt on stdin (the variadic `--allowedTools` swallowed it). The person accepted both, and they are in T-28's Scope as the run conditions for acceptance runs.
- **Readings members landed that the spec did not settle** (none was on a stop list):
  - T-22:
    - a nesting limit of 200 (a deep `(` run would overflow the stack and end the server);
    - six added error wordings;
    - a literal beyond float range is refused, and one too small narrows to 0;
    - `QueryResult.ErrorsFor(kind)`;
    - a flag error points at the flag;
    - `kind` is exact-case, and `y==1andx<2` does not parse.
  - T-24: the spec's "zoom 3 shrinks the projected size" contradicts its own "`zoom` divides `h`". The member tested the latter, and the person's reply did not address it.
  - T-24 added two corner frames, since a cube head-on and the hexagon frame cannot show a mirror.
  - T-25:
    - mode and highlight come before zoom and target in the caption;
    - unknown keys in `highlight` are ignored, so a typo (`face`) highlights nothing;
    - "the lid's recessed panel faces" includes its four walls.
  - T-23: `samples/.gitattributes` pins LF, and "on the bounds" counts as inside.
  - T-25 and T-26 made `MeshEdits.UnknownElements` and `MeshToolSupport.Ordinal`/`Absent` public, outside their owned files.
- **Evidence lost.** T-27 kept its review frames and `breaks.log` in its own worktree's `artifacts/`, which `finish` emptied, not in the milestone's as its brief said. Only its As built tables and the frame hashes in its notes remain.
- **A stale guard.** A guard early in T-26 compared stale frames, because `cairn test artifacts` returns the last queued job, not a local run. T-26 redid it on the right runs.

**Never verified.**

- **Checks seen to fail only behind another:**
  - T-24: `CheckMarginSquare` and the 330/120 degree samples.
  - T-25: the corner checks and later samples per face.
  - T-26: the checks listed in its As built.
  - T-27: door-equals-wall shade, gutters, captions, `-x` culled-wall pixels and the orange eave pixel.
- **The stray vertex's "not labelled" check cannot run:** no labelled frame holds (6, 0, 0). Since RV-20 the scenario logs it as not covered, rather than passing it silently.
- **Not run under breaks:** the regression tests (T-24, T-25) and the hash guard (T-26). No mirrored or flipped camera was run against the product in T-27; it was only reasoned from the ray test.
- **Sizes and looks:**
  - Since RV-20, `The_looks_are_drawn_at_every_size` renders the wireframe with one highlighted edge at 256, 512 and 1024 and checks its edge and orange pixels. The normals look is never rendered at 1024 nor pixel-checked at 256, and the scenario frames' looks are still at 512 perspective only.
  - `highlight` on vertices, and on a sheet's pixels beyond the every-tile test RV-20 added, is untested, and no scenario frame shows a labelled sheet.
  - No acceptance agent used `highlight` or `undo`.
- **Acceptance:** `move`'s text is now the facts-only sentence the person ruled in RV-20, not round 2's as run, and no fresh session has run on it. B-2's effect is untried.
- **T-22:** "select leaves the version unchanged" rests on undo through the client and a source check, not on the version. Its nesting-limit break did not separate a crash from a message mismatch.
- **The installed carve:** `C:\tools\carve` is the 2026-11-05 snapshot, with no `select`, `check` or new `render` arguments, until it is re-published after the merge to `main`.
- **Machines:** any GPU but the two RTX 5070 machines (Mantorok, Xellotath), and any cross-machine frame comparison.
- The orchestrator opened no frame. Every look above is a member's or the person's.

**Known and left.**

- B-2: `move`'s non-edge error does not point to `select`.
- `CLAUDE.md`, `README.md` and `samples/.gitattributes` are inputs of no suite (a `verify` warning).
- Empty folders `branches/task/T-22` to `T-28` remain on disk, unregistered from git.
- `%TEMP%\carve-m4-acceptance\` (three rounds and a Release publish of `carve`) and the members' scratch files in `%TEMP%` are not cleared.
- Evidence is in the milestone worktree's ignored `artifacts/review/` (T-22, T-24, T-25, T-26, T-28) and `artifacts/m4-acceptance/round-1` to `round-3`.

**The test report** (`cairn test report --since 1d`) shows no flakes. `server-regression` is the costly suite, at about 36,000 slot-seconds a week over the window, with 1 failure and 1 unique catch. 27 jobs across the suites are `cnr` (never ran). Their cause was not looked into; members' deliberate breaks, one of which did not compile, are the likely source. Nothing was filed.

**For the next plan.**

- **Work every pixel check and every query against the geometry at planning**, by hand or with a throwaway Core test on the sample. Eleven spec faults here cost a stop each, and T-27 alone stopped three times.
- **Say what a failure is** in a rule like "the cheaper model if the probe passes on the first try". Here a misread doc counted and cost a dispatch.
- **Give each member's evidence folder in its brief as the milestone's absolute path, and check it after `finish`.** T-27 lost its evidence though the brief said where.
- **Acceptance plants:** an agent that meets a wrong id asks rather than guesses, and a description that tells it to guess makes it avoid the plant. Judge recovery by the error being understood, or plant something the agent can fix without guessing the person's intent.
- **Ask before running against a recorded ordering ruling.** M-16 to M-19's order was flagged, not asked.
- Re-publish `C:\tools\carve` after `main` moves.

**The review.** Round 1 (RV-20) split the diff over four Opus 5.5 reviewers (the query language and health check; the render pipeline; the scenario frame checks; the whole diff) and raised 23 findings: 3 Major, 15 Minor, 5 Nit. The Majors: `move`'s restored round-2 text told agents to move what `select` returns instead of asking, against the person's ruling; `shed-open-edges` would pass with every edge orange; and the look-after-an-edit test could not fail. The person ruled three (`move`'s text states facts only; `inside-out` below -1e-6 x the bounds diagonal cubed; -0 equals 0). Two Opus fixers, one on the product and docs and one on the scenarios in its own worktree, fixed 22 and skipped 1 (the stray vertex), each new check shown failing, with all 63 frames byte-identical. Round 2 (RV-21, one Opus reviewer) found every answer holds and no new defect, and raised 3 findings about text the fixes left stale. A Sonnet 5.5 fixer and the orchestrator fixed them. Round 3 (RV-22, Opus) found RV-21's answers hold and raised 2 findings, both Retrospective text claiming more than was done, fixed in the docs. Round 4 (RV-23, Opus) found nothing. No frame changed in any round. As in M-1 to M-3, members' `VERIFY PASS` hid checks that could not fail.
