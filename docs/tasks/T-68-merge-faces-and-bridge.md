---
description: >-
  merge_faces dissolves connected faces into one, and bridge joins two open loops with a ring of quads
---
# T-68 — Merge faces and bridge

**Status:** In Progress

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-64

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent tidies up after a cut, merging the pieces it did not want back into one face, and joins two openings with a tunnel, so a hole can go through a wall.

## Scope

**Core: `FaceMerge`** (its own file). `merge_faces(faces)`.

- **What merges.** The listed faces must be live and connected, each sharing at least one edge with another listed face. They become one face: the union's outline, plus holes where the union encloses one (T-63).
- **Ids.** The lowest listed id survives, reshaped. The others are tombstoned (T-64).
- **Vertices.** Vertices left with no live face are tombstoned. Vertices still on the outline stay, including collinear ones (no vertex is dissolved from an outline).
- **What carries.**
  - The surviving face keeps its own colour and groups. A face of another colour among those merged is a warning naming it. It is not an error.
  - Hard marks on dissolved edges go.
- **Warnings.** A non-planar result is a warning, as `define_mesh`'s is.
- **Refused:**
  - fewer than two faces;
  - faces that are not connected;
  - mixed winding across a shared edge;
  - a result that is not one face, such as two faces touching at a vertex only.
- It reports `Deleted` (T-64).

**Core: `Bridge`** (its own file). `bridge(loops: [[v, ...], [v, ...]])`.

- **The loops.** Each loop is a closed cycle of vertex ids whose every edge is open (one face). Both loops have the same count.
- **The pairing.** Pairs are chosen by the least total length over every rotation of the second loop and both of its directions, so the order the agent lists them in does not matter. A tie goes to the first rotation found, in a stated order.
- **The faces.** One quad per pair of consecutive pairs, appended in the first loop's order. Each quad is wound so that its edge on each loop runs opposite to the existing face there, which closes the surface.
- **What carries.** New faces take the colour and groups of the face on the first loop's edge they come from, as `extrude`'s walls do. No vertex is created, so no weights are made.
- **Refused:**
  - a loop that is not a cycle of open edges;
  - unequal counts;
  - a vertex in both loops;
  - loops whose neighbouring faces wind such that no quad can agree with both.

**Server.** The tools `merge_faces` and `bridge`, each with its description and a worked example.

- `merge_faces`: on a cube that `loop_cut` has split, merging one side's pieces back. Until T-66 lands, the example's input is built with `define_mesh` directly.
- `bridge`: two unit squares 1 apart, given as open quads facing each other, bridged into an open-ended box.
- Each tool is one `undo` step.

**Tests.**

- `unit`, each with a break shown failing:
  - merge two quads, three in an L, and a ring of four round a square (which gives a holed face);
  - ids and orphans;
  - a colour mismatch warning;
  - each merge refusal;
  - bridge of two squares (pairing over rotations and reversal, winding, the result closed when they are the two holes of a slab's front and back);
  - bridge of two holes of T-63's plate front and back, giving a closed tube;
  - each bridge refusal.
- `regression`: both tools through the client, then `check`.

**Content.** `MergeBridgeStage.cs` in the Cuts scenario:

- a slab 2 x 2 x 0.2 defined with a square hole in its front face and a matching hole in its back face, the inner faces left out, bridged. From `[0.6, 0.35, 1]` shaded, and orthographic `[0, 0, 1]` showing the background through it.
- A plate of four quads merged into one, in the wireframe from `[0.6, 0.35, 1]`.

Ray-test pixel checks, each shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):**
  - the lowest id survives a merge;
  - vertices left with no face are removed and listed in `deleted`;
  - a non-planar merge warns;
  - bridge takes two vertex loops of equal count and pairs them by least total length.
- Runs beside T-65. Both add to `Program.cs`'s list and `CutsStages.cs`, so keep both sides. This task does not touch `MeshToolSupport`'s report body (T-65 adds `split` there) or `FaceSplit`. It uses `Deleted` as T-64 wrote it down.
- **Open rules: stop and ask.**
  - Bridging loops of unequal count.
  - Dissolving collinear outline vertices.
  - A merged colour other than the survivor's.
  - A bridge with twist or segments.
- Every existing frame stays byte-identical by hash, listed.
- Opus for the pairing and the winding rules. If one agent fills on merge, hand off before bridge with As built written.
- Evidence goes to the milestone worktree's `artifacts/review/T-68/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.

### As built

First agent: Opus (claude-opus-5-5). Core, server, tests and the Cuts stage are in (commits up to `990b937`). The stage's pixel-check breaks, the GPU hash comparison and the person's frame review are still to do (see Hand-off).

- **`FaceMerge`** (`src/Carve.Core/FaceMerge.cs`): `Merge(mesh, faces, out warnings)`. Refused: fewer than two faces (`faces: merge_faces needs at least two faces to merge (got n)`), an unknown or deleted id (`MeshEdits.UnknownElements`), an id listed twice (worded as extrude words it), faces not connected through shared edges (`faces[n]: shares no edge ... with faces[s]`), two listed faces running a shared edge the same way (`edges[a-b]: faces[x] and faces[y] both run this edge the same way (mixed winding)`), an edge shared by more than two listed faces, and a result that is not one face (`faces: the merged faces would not be one face: ...`: the outline passing through a vertex twice, a closed shell, no outer loop, or several outer loops). The outline is the listed faces' directed edges less the dissolved ones, chained. Seen along the listed faces' summed Newell normal, the counter-clockwise loop is the outer loop and every clockwise one is a hole. **Loop start (the spec leaves this open, so this is my choice):** the outer loop starts at the first corner of the survivor's own outer loop that is still on it, else at its lowest id. Holes start at their lowest id and are ordered by it. Vertices: every corner of a listed face that no live face (the merged one included) uses afterwards is tombstoned, so a stray stays (the person's 2026-11-12 ruling). Ids and deletion go through `PolygonMesh.Edit(reshapedFaces, deletedVertices, deletedFaces)` and `EditResult.From(..., deleted)`, T-64's API as written. Nothing of my own removes anything. Colour: the survivor keeps its own colour and groups. Each other listed face whose colour differs is a warning `faces[n]: merged with colour X, and the merged face keeps faces[s]'s colour, Y` (no colour counts as a colour). The warnings come back through `out warnings`, so `EditResult` gains nothing for them. Hard marks on dissolved edges go by the existing `Carried` rule: a hard edge no longer in the topology is dropped. Non-planar: the report's existing `NonPlanarFace` warning.
- **`Bridge`** (`src/Carve.Core/Bridge.cs`): `Join(mesh, first, second)`, `Pair(...)` and `Pairing(Rotation, Direction)`. Pairing: first-loop `i` goes with second-loop `(r + d*i) mod n`. The trial order is `d = +1, r = 0..n-1`, then `d = -1, r = 0..n-1`, and a later pairing replaces an earlier one only when strictly shorter (the stated tie order, also in the tool text). Quad `i` joins pairs `i` and `i+1` and is appended in the first loop's listed order. If the face on first-loop edge `a_i a_{i+1}` runs `a_i -> a_{i+1}`, the quad is `[a_{i+1}, a_i, b_i, b_{i+1}]`, else `[a_i, a_{i+1}, b_{i+1}, b_i]`. **My reading of the winding refusal:** the pairing (direction included) is chosen by length first. The bridge is then refused where the face on the paired second-loop edge does not run opposite to the quad (`edges[b0-b1]: paired with edges[a0-a1]: the faces on the two edges wind so that no quad between them agrees with both`), e.g. two squares facing the same way. It is also refused where the faces round the first loop wind both ways (`loops[0][i]: ... wind both ways`). Other refusals: a loop of fewer than 3, a vertex not live (`loops[k][i]: vertex v: ...`) or repeated (`repeats loops[k][j]`), neighbours with no edge between them, an edge that is not open, unequal counts (`loops: the loops have 4 and 3 vertices`), and a vertex in both loops (`vertices[v]: is in both loops`). New faces take the colour and groups of the face on the first loop's edge, through a new internal `EditResult.From(source, result, newFaces, styledFrom)` overload. `Carried` now takes `(Face, Added)` pairs, and extrude's path passes its walls exactly as before.
- `MeshEdits.Ordinal` is now `internal` (it was private), for the repeat message.
- **Server.** `merge_faces` (`MergeFacesTool.cs`) and `bridge` (`BridgeTool.cs`) are added to `Program.cs`'s list after `delete`. `MeshToolSupport` is untouched: merge's colour warnings are appended to the report's `warnings` in the tool's own `addIds` callback. Each worked example has a `define_mesh input: {...}` line, then `Example: {...}`. merge: a unit cube split round y = 0.5 into ten faces, built with define_mesh (T-66 has not landed), merging the front's two pieces with `{"faces":[2,3]}`. bridge: the two unit squares facing each other, `{"loops":[[0,1,2,3],[4,5,6,7]]}`.
- **Open question for the person (the spec does not settle it):** the spec calls the bridge example's result "an open-ended box". Bridging two square *faces* gives a closed box. Because the squares face each other, the walls (wound to agree with them) face in too, so `check` reports the box `inside-out` (`shells[0]`). I kept the spec's "facing each other". The tool text says the box is closed, that check finds it inside-out, and that squares facing away give an outward box. The regression test asserts exactly that one finding. If the person wants an outward box (squares facing away) or a truly open-ended example, `BridgeTool.Squares` and the test change in a line each.
- **Tests.** `tests/Carve.Core.Tests/MergeBridgeTests.cs` (unit, 22):
  - merge: two quads (the collinear vertex kept), an L of three, a ring of four round a filled square (a holed face, its hole edges manifold);
  - ids and orphans: the lowest id survives when listed out of order, the centre vertex is deleted, the stray is kept, and a later extrude appends after the slots;
  - the colour warning (none for the same colour, one for no colour), the hard mark dropped, the non-planar warning, and each merge refusal;
  - bridge of two squares: the quads and their winding, closed and manifold;
  - pairing over rotations and reversal: 5 listings give identical faces, and `Pair` gives `(1, -1)`; the tie order;
  - the holed slab's two holes bridged into a closed tube, its walls facing into the hole, and the same with the second loop listed reversed and rotated;
  - the style carry, and each bridge refusal.

  `tests/Carve.Server.Tests/MergeBridgeToolTests.cs` (regression, 3): each worked example through the client, then `check`, get_mesh, the refusals and one undo step each, plus the colour warning through `set_colour`. Core: 643 passed. The 3 regression tests pass locally.
- **The Cuts stage** (`scenarios/Carve.Scenario.Cuts/MergeBridgeStage.cs`, one line in `CutsStages.cs`):
  - The slab: T-63's plate geometry (z = +-0.1), holes 4..7 and 12..15, inner faces left out; 16 vertices, 6 faces, open. Bridged: 10 faces, closed, created faces 6..9, `check` finds no problems, wall 6 is `[4, 5, 13, 12]`.
  - Frame `bridge-slab`, from `[0.6, 0.35, 1]`: the hole's centre is the background, the -x wall (face 9) and the -y wall (face 6) are each one shade within `Exact`, and the front is one shade.
  - Frame `bridge-slab-ortho`, from `[0, 0, 1]`: every probe inside the hole is the background, since the walls are edge-on.
  - The 2 x 2 plate of four quads, merged: 8 vertices, 1 face, deleted vertices `[4]` and faces `[1, 2, 3]`, get_mesh loop `[0,1,2,5,8,7,6,3]`.
  - Frame `merge-plate-wireframe`, from `[0.6, 0.35, 1]`: the eight outline edges are drawn, checked in four stretches each (`Checks.Line`), and three points along each dissolved edge (4-1, 4-3, 4-5, 4-7) are the face fill within `Exact`.

  I ran `run-scenario.ps1` on this machine on `990b937`, and it passed (`artifacts/scenarios/cuts/1`). **`bridge-slab-ortho` is byte-identical to `holes-open-ortho`** (`5be039b1...`), and that is expected: it is the same plate seen straight on, and the tunnel walls are edge-on, so the same pixels are drawn. The local frames and their hashes are in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-68/cuts-local-990b937/`. The six earlier Cuts frames from that run match T-64's `all.sha256`.
- **Breaks** (Core and server): `C:/Users/andre/AppData/Local/Temp/t68/breaks.py` applies each break alone in a clone of HEAD, builds with warnings as errors and runs the tests. The output is in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-68/breaks.log`. Their state is in the Hand-off.

### Hand-off

From the first agent (Opus, claude-opus-5-5), handing off at the call checkpoint: the code is committed (`990b937`) and its verify is running, and the frame review is still to come.

- **Done:** `FaceMerge`, `Bridge`, both tools with worked examples, 22 unit and 3 regression tests, and the Cuts `MergeBridgeStage` with three new frames. Its local run passed.
- **Running when I left:**
  - `cairn task T-68 verify` on `990b937`. The log goes to `C:/Users/andre/AppData/Local/Temp/t68-verify.log`, and `cairn test status` shows the jobs (scenario-shed was done, scenario-cuts running, the rest queued behind Empyrean's gate jobs). Read its result; don't resubmit unless the commit changes.
  - The Core and server breaks (`breaks.py`, 27 of them) in the clone `C:/Users/andre/AppData/Local/Temp/t68/clone`. The first 13 (merge) had each failed its test when I left, as the log shows. Check the rest in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-68/breaks.log`: every line should show `Failed: N >= 1` (a "BREAK NOT APPLIED" line means the pattern moved). Then fill the As built's break table from it.
- **Left:**
  1. **The stage's pixel-check breaks**, each shown failing on a broken *product* (the rule: a break must fail a check). Suggestions: (a) the bridge leaves out one quad (e.g. the -x wall), and the `bridge-slab` probes by the -x wall should fail; (b) the merge keeps the interior edges (the shared edges not dissolved), and the dissolved-edge fill checks in `merge-plate-wireframe` should fail; (c) the renderer drawing a tombstone or a hole wrongly is T-63/T-64's ground already. Run them through `run-scenario.ps1 cuts` in a clone, so the worktree stays clean, and append to `breaks.log`.
  2. **The frame hash list.** When verify's GPU suites are done, fetch the frames (`cairn test artifacts <suite>`) and compare all 127 of `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-64/frames-737853a/all.sha256`. Write the new run's list to `.../T-68/frames-<sha>/all.sha256` (130 frames with the three new ones). Check that the three new frames are byte-identical to the local ones in `.../T-68/cuts-local-990b937/cuts-frames.sha256`.
  3. **The person's review** of the three new frames (`bridge-slab`, `bridge-slab-ortho`, `merge-plate-wireframe`) through `mcp__waystone__review`, from the evidence folder. No new frame has been opened yet: I opened 0 images. Mention that `bridge-slab-ortho` equals `holes-open-ortho` by hash.
  4. **The open question** in As built, on the bridge example "open-ended box" versus the closed, inside-out box. Put it to the person with the review.
  5. Then the break table in As built, `cairn lint`, a commit, `verify` until VERIFY PASS, and `finish` from inside the worktree.
- **Surprising:** a `heredoc` with `'EOF'` through the Bash tool failed twice here on C# raw strings and on markdown with backticks, so write files with the Write tool. A raw string can't start with a `"`: `""""faces"...` opens a four-quote raw string. The T-65 sibling had not landed by `990b937`, and I did no rebase.
- **Breaks, finished after the hand-off was sent:** all 27 Core and server breaks in `breaks.log` failed at least one test (1 to 8 each), none was "NOT APPLIED", and the unbroken run passed 22 of 22. Only the stage's pixel-check breaks are left.
- **Verify and frames, finished after the hand-off was sent:** `cairn task T-68 verify` on `990b937` gave VERIFY PASS (10 ran, viewer-cube carried, 0 failed). That run's 130 frames and their list are in `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-68/frames-990b937/` (`all.sha256`). Compared by path against T-64's `frames-737853a/all.sha256`, all 127 are byte-identical, none is missing, and the three new frames are `cuts/frames/bridge-slab.png`, `bridge-slab-ortho.png` and `merge-plate-wireframe.png`. They are byte-identical to the local run (`cuts-local-990b937`). Left: the stage's pixel-check breaks, the person's review (with the open question), the break table, lint and finish.
