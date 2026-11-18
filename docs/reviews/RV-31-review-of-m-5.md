---
description: >-
  Review of M-5
---
# RV-31 — Review of M-5

**Status:** Answered

**Reviews:** M-5

**Round:** 1

**Previous:** —

**Diff:** bf5b2cc8eadb5eb6df3e64a9303037ae3ca496e0 against 7d957b4

**Reviewer:** five reviewers, each claude-opus-5-5: Core topology and format; Core cut algorithms; server tools, reports, descriptions and docs; scenarios, pixel checks and suites; the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: Core topology and format (claude-opus-5-5)

### subdivide's report miscounts a mesh with a deleted face: triangles 2 short per tombstone, and a wrong `created.vertices`

**Severity:** Minor

`src/Carve.Server/SubdivideTool.cs:58` and `:77`. Rubric: correctness (consumers that still count a tombstone). The `triangles` figure is `Faces.Sum(f => f.Corners.Count - 2 + 2 * f.Holes.Count)` over every slot. `Face.Deleted` has 0 corners, so each tombstone adds -2. The same figure is checked against `SkinnedExport.MaxTriangles` for the warning. `created.vertices.first` is `edited.Vertices.Count - result.NewVertices.Count`, but `MeshEdits.Subdivide` (`src/Carve.Core/MeshEdits.cs:343`) leaves out of `NewVertices` the tombstoned face point it makes for each deleted face, and that face point sits inside the new range (at `v0 + f`). So `first` comes out too high, and `first..first+count` is not the set of new vertices. Failing input, worked through Core with the tool's formulas: the unit cube with face 3 deleted, then `subdivide` 1. This gives 26 vertex slots with vertex 11 a tombstone, and `NewVertices` 8..25 minus 11 (17 ids). The tool reports `triangles` 38 where there are 40, and `created.vertices` `{first: 9, count: 17}` where the first new vertex is 8. `SubdivideToolTests` covers only a mesh with no tombstone. As built (T-64, second agent) says `subdivide` handles tombstones, and doesn't mention this.

**Outcome:** fixed

In cdb1b01.

### The Core "undo, then a new edit reuses the undone ids" test cannot fail

**Severity:** Minor

`tests/Carve.Core.Tests/TombstoneTests.cs`, `AnUndoneDeleteFreesNothingItHeldAndANewEditUsesTheUndoneIds`. Rubric: a test that could not fail on a broken product. The test extrudes and deletes, throws the result away, and then asserts things about the original immutable `cube`: no tombstones, face 1 unchanged, and a fresh extrude of `cube` gives vertices 8..11 and faces 6..9. All of that holds whatever `MeshDelete`, `Edit` or undo do, because nothing the test asserts on ever saw the delete. T-64's break table has no row for it. The spec's unit item "undo then a new edit reusing the undone ids" is therefore not tested. `DeleteToolTests` checks that undo restores the slots, but it never makes a new edit after the undo and checks that the freed ids are given out again.

**Outcome:** fixed

In 1d02606.

### The tombstone-bounds assertion in `ReportCountsLiveElementsAndWarnsOfNoTombstone` cannot fail

**Severity:** Nit

`tests/Carve.Core.Tests/TombstoneSweepTests.cs`, the `BoundsMin`/`BoundsMax` lines with the comment "the live vertices', not the tombstone's zeroed slot". Rubric: a test that could not fail. The deleted vertex's slot is `Vector3.Zero`, which is also live vertex 0 and lies inside the asserted bounds [0, 1]³. A break that took bounds over every slot would still give (0,0,0)..(1,1,1). Deleting a vertex away from the origin, or checking a mesh whose live bounds exclude the origin, would let the assertion fail.

**Outcome:** fixed

In 9c52ea5.

## Findings: Core cut algorithms (claude-opus-5-5)

### A cut segment whose midpoint lies on the boundary is skipped silently, so the knife reports success and cuts nothing

**Severity:** Major

`src/Carve.Core/FaceSplit.cs:211` with `AlongBoundary` (`:326-334`), and `Knife.Runs` (`src/Carve.Core/Knife.cs:260-262`, used at `:245`). Rubric: correctness on awkward input (a concave face, a cut along an edge). Both decide that a segment "runs along the boundary" from its **midpoint alone**, within the tolerance of any loop edge. A chord across a concave face whose middle stretch runs along an edge (or through a vertex at its midpoint) is therefore classed "along", dropped from the split, and the call succeeds. Failing input, confirmed with a throwaway program against HEAD: a U-shaped face `(0,0) (3,0) (3,3) (2,3) (2,1) (1,1) (1,3) (0,3)` and `Knife.Cut(mesh, [(0,1,0), (3,1,0)])`. The result is `Succeeded`, `split: []`, two new vertices 8 and 9 put into face 0's loop, the face left whole (area 7), and `KnifeResult.Edges` = `[(8, 9)]`, which is not an edge of the mesh. `plane_cut` on the same face at y = 1 splits it correctly into three pieces (areas 3, 2, 2), because its `Crossings` splits the line at the on-plane corners first. The knife, and `FaceSplit` itself, need the same: break a segment at the boundary vertices it passes through, or refuse it. A "segment along an edge" should mean the whole segment, not its midpoint. No test has a concave face under the knife, so nothing catches this.

**Outcome:** fixed

In 8ae768a.

### A segment that touches the boundary inside its span is refused only by the final validation, naming a face that does not exist

**Severity:** Minor

`src/Carve.Core/FaceSplit.cs:363-368` and `src/Carve.Core/Knife.cs:274-282`. Rubric: correctness on awkward input (a cut through a vertex, a concave face), and errors that name what the agent gave. `Crosses` (`PlaneGeometry.cs:46-47`) detects only proper crossings, so a segment that passes exactly through a boundary vertex, or runs collinear with part of an edge, passes every check in `Pieces` and `Runs`. The traced pieces are then pinched or fold back, and only `PolygonMesh.Edit`'s validation stops them, in terms of the would-be piece ids. `Knife`'s renaming (`Segments`, `:96-100`) maps only `cuts[k]`, so these reach the agent unchanged. Confirmed against HEAD:
- a face `(0,0) (4,0) (4,4) (3,4) (2.5,2.5) (2,4) (0,4)`, knife `(0,2.5) -> (4,2.5)` (it grazes reflex vertex 4): `faces[1]: face cannot be triangulated (degenerate outline)`;
- a W-topped face `(0,0) (4,0) (4,4) (3,2) (2,4) (1,2) (0,4)`, knife `(0,2) -> (4,2)` (it leaves the face through vertices 3 and 5): the same error;
- a U with an off-centre notch `(0,0) (3,0) (3,3) (1,3) (1,1) (0.5,1) (0.5,3) (0,3)`, knife `(0,1) -> (3,1)`: `faces[1]: face folds back on itself: ...`.

The mesh has only `faces[0]`. The first and third have a well-defined cut (split at the touched vertices, as `plane_cut` does). The second should at least be refused as leaving the face, naming the points.

**Outcome:** fixed

In 8ae768a.

### A plane cut in a face's own plane caps over that face: a coincident reversed cap, or the face renumbered and its colour lost

**Severity:** Minor

`src/Carve.Core/PlaneCut.cs:107-111` (the border), with `IsAbove` at `:145`. Rubric: correctness on awkward input (a cut in a face's own plane); id and `deleted` reports matching what changed. The border takes every kept face's edge that a deleted face runs the other way, with both ends on the plane. It does not leave out a kept face that itself lies in the plane, and it does not notice a deleted face in the plane whose loop is the cap. Confirmed on the unit cube against HEAD:
- `plane_cut` through `(0,1,0)`, normal `+y`, `keep: above`: the top face 3 `[3,7,6,2]` is kept and a cap 6 `[2,6,7,3]` is added on the same four vertices, facing down. The result is a zero-thickness two-sided sheet that `check` will call closed.
- `keep: below` at the same plane (the case `PlaneCutTests` pins), or `(0,0,0)` with `keep: above`: nothing is cut away, but the face in the plane is deleted and re-made as cap 6, so its id goes to `deleted` and its colour and groups are lost (caps take none, by ruling).

The person ruled that a face in the plane goes with its normal's side. The fill making a coincident face over it was not ruled. A kept face in the plane should not count towards the border, and a deleted face in the plane whose loop is exactly a cap loop should arguably be kept instead of re-made.

**Outcome:** fixed

In 97c024e.

### `merge_faces` accepts a fold whose outline is planar, with no warning

**Severity:** Minor

`src/Carve.Core/FaceMerge.cs:128-145`, and the planarity warning taken from the result's own outline. Rubric: a merge leaving a bad outline. The outer loop is chosen by winding against the listed faces' summed normal, and the only geometric check is the merged face's planarity. Faces that wrap around do produce a planar outline. Confirmed against HEAD: the unit cube, `merge_faces [0, 2, 3, 4, 5]` (all but the top). It succeeds with no warning. Face 0 becomes `[4,7,6,5]`, coincident with face 1 `[4,5,6,7]` and facing the other way, and the mesh is a closed two-face sheet. A check that the merged face's normal agrees with each listed face's normal (or that the listed faces are near-coplanar) would catch this. The summed normal is already computed.

**Outcome:** fixed

In 0bc13ce.

### The ring walk treats any revisited quad as the ring closing

**Severity:** Nit

`src/Carve.Core/LoopCut.cs:73-79`. Rubric: correctness on awkward input (the ring's stops). `seen.Add(face)` failing is read as "back at the start". A ring that re-enters an earlier quad across its other pair of sides (a ring that crosses itself, as on some grid-on-torus layouts) has `cur != (a, b)` there, and is refused with B-3's message, "comes back to it with its ends swapped". That message describes a different situation. The refusal is safe, but its fact is wrong. Checking that the revisited quad is the first quad, and that `cur` is the start edge either way round, would separate the two.

**Outcome:** fixed

In 2ce4886.

## Findings: Server tools, reports, descriptions and docs (claude-opus-5-5)

### `subdivide`'s report gives wrong new ids and too few triangles on a mesh with tombstones

**Severity:** Major

`src/Carve.Server/SubdivideTool.cs:77` and `:59`. Rubric points: report fields, and a server tool working out mesh behaviour that belongs in the Core. `created.vertices` is written as `{first: Vertices.Count - NewVertices.Count, count}`, which assumes the new ids run on without a gap. Since T-64, `MeshEdits.Subdivide` tombstones the face point it makes for each deleted face (`MeshEdits.cs:333-344`) and leaves it out of `NewVertices`, so there is a gap. `triangles` is worked out in the server as `Corners.Count - 2 + 2 * Holes.Count` over every face slot, so each tombstone (0 corners) takes 2 off the count. That count is also what the `MaxTriangles` warning is checked against. Failing input, run on the built server: `define_mesh` the unit cube, `delete {"faces":[3]}`, `subdivide {"levels":1}`. The report says `"triangles":38` and `created.vertices {first:9, count:17}`. The right values are 40 triangles (20 live quads), and new vertices 8..25 with slot 11 `null` in `get_mesh`. So the report leaves out id 8, which is new, and its range includes id 11, which is a tombstone. After `delete {"faces":[1,4]}` it says 18 triangles where there are 24. No test runs `subdivide` after a `delete`.

**Outcome:** fixed

In cdb1b01.

### The README's *Cutting* examples for `merge_faces` and `bridge` fail on the unit cube the section names

**Severity:** Minor

`README.md:69`, `:74` and `:76`. Rubric point: docs that disagree with the code. Line 69 says "the examples are on `define_mesh`'s unit cube". On that cube, `merge_faces {"faces":[2,3]}` is refused (`faces[3]: shares no edge ... with faces[2]`, because they are the bottom and the top). `bridge {"loops":[[0,1,2,3],[4,5,6,7]]}` is refused with 8 problems (`edges[0-1] is used by 2 faces, not open`, and the same for the other seven edges). The tools' own examples are on other inputs: the cut cube for merge, and two squares for bridge. Line 74 also says `merge_faces` joins "coplanar" faces, but the tool merges faces that are not coplanar and only warns: `merge_faces {"faces":[1,5]}` on the cube succeeds with `faces[1]: face is not planar`, which its description states.

**Outcome:** fixed

In 4389b62.

### Descriptions' "Returns JSON" shapes leave out `deleted` and `split`, which every edit now returns

**Severity:** Minor

Rubric points: descriptions that disagree with the code, and report fields consistent across tools. `MeshToolSupport.Edit` (`MeshToolSupport.cs:170-171`) adds `deleted` and `split` to every edit's body, and `SubdivideTool.cs:80-81` and `DefineMeshTool.cs:174-176` add them too. Only `plane_cut`, `loop_cut` and `knife` list both fields. Several descriptions leave fields out:
- `merge_faces` (`MergeFacesTool.cs:27`), `delete` (`DeleteTool.cs:19`) and `bridge` (`BridgeTool.cs:25`) leave out `split`.
- `define_mesh` (`DefineMeshTool.cs:33`), `move` (`MoveTool.cs:20`), `extrude` (`ExtrudeTool.cs:22`) and `subdivide` (`SubdivideTool.cs:23`) leave out both.

So an agent reading `move` or `extrude` is not told that `deleted` or `split` exist. Also, `define_mesh` now returns `deleted` and `split` but no `created`, so the body differs from the other edits.

**Outcome:** fixed

In ce879cf.

### Argument errors in `bridge` and `loop_cut` add a second problem that states something false

**Severity:** Minor

`src/Carve.Server/BridgeTool.cs:69-70` and `src/Carve.Server/LoopCutTool.cs:63-64`. Rubric point: error texts state facts only. In `bridge`, a loop that is not an array gets two problems. `Ids` reports the real one, then the tool adds `got null`: `{"loops":[[0,1,2],5]}` returns `loops[1]: expected an array of integer ids, got 5` and also `loops[1]: expected an array of vertex ids, got null`. In `loop_cut`, an entry that is not an integer is dropped from the list, and then the length check runs on what is left: `{"edge":[0,3.5],"at":[0.5]}` returns `edge: the 2nd listed entry: expected an integer id, got 3.5` and also `edge: expected a pair [a, b] of vertex ids, got 1 entry`, though two entries were sent.

**Outcome:** fixed

In eaac926.

### The normals look puts a holed face's spike at the outer loop's centre, which can be inside the hole

**Severity:** Minor

`src/Carve.Server/LookMeshes.cs:222` (`Spikes`), and the render text at `RenderTool.cs:204` and `:212`. Rubric point: render of holed faces. T-63 moved face labels to `FaceLabelPoint` so that a label is never drawn in a hole. The flat spike still stands at `FaceCentre`, the mean of the outer loop's corners. For the knife's closed-square example (front face 1 holed, inner face 6), both faces' spikes start at (0.5, 0.5, 1), so the holed face's spike looks like it belongs to the inner face. With the inner face deleted (the gatehouse window), the spike stands in empty space. The render description still says "a spike on its centre" and "A face's id sits on its centre". The second is no longer true for a holed face.

**Outcome:** fixed

In 41e58df; all 187 frames byte-identical on 6626bd2 (the cube's 32 from a local fresh run).

### `merge_faces`'s mixed-winding error tells the agent what to do

**Severity:** Nit

`src/Carve.Core/FaceMerge.cs:65` (the text is read through the tool). Rubric point: M-4's facts-only ruling. The text is `... so they cannot be merged: make them wind alike first`. The last clause is an instruction, not a fact. The person approved advice for the knife's one-point message (T-67, 2026-11-12), but not for this one.

**Outcome:** fixed

In 6a8130b.

### `knife`'s type error for `points` names the kind differently from every other argument error

**Severity:** Nit

`src/Carve.Server/KnifeTool.cs:73`. It writes `ValueKind` lowercased (`points: expected an array of [x, y, z], got object`, `got string`). Every other tool writes these through `Describe`, which gives `an object` and `a string`.

**Outcome:** fixed

In 355fe44.

### `render`'s first sentence still names only move, extrude and undo as what changes the mesh

**Severity:** Nit

`src/Carve.Server/RenderTool.cs:179`: "as set by define_mesh or open and changed by move, extrude or undo". It does not name the six M-5 tools, or `subdivide`. `get_mesh`'s new sentence about `null` slots (`GetMeshTool.cs:14`) is put in the middle of the JSON shape, between `faces:[...]` and `edges:[...]`, which makes the shape hard to read.

**Outcome:** fixed

In c53fe03.

## Findings: Scenarios, pixel checks and suites (claude-opus-5-5)

### `loop-cut-band-extruded` cannot see the extrude: a band drawn flush passes

**Severity:** Major

`scenarios/Carve.Scenario.Cuts/LoopCutStage.cs:42-47, 99-111`. Rubric point: a frame with no check that could fail on what it shows. This is the same failure T-67's review found in `knife-slab-recess` (T-67 As built, R5 "not caught: the scenario passed"), and the person's rulings fixed it there with `Predicted`. Here it is still open:

- The band's top and right walls are probed only through `CutsChecks.Look`, which uses `Solid.Seen` (three pixels round must agree). Worked with the shed camera for this box: the top wall (y = 1.1, z 1.0 to 1.1) is about 4.7 px thick on screen, and the right wall about 9.4 px wide. The probes sit at 0.3 and 0.7 across, about 2.8 px from an edge. So `Seen` is never sure of any wall probe, and they are skipped silently (`Look`'s `seen == -2` → `continue`). Nothing makes at least one of them sure.
- What is left: the band's front, the box's front pieces and the right/top faces. Each is checked only against its own first sure probe (`Shades`, lines 120-135, read from the same frame). The band's front has the box front's normal, so it is the same grey.
- So if the band is drawn at its old depth, or its walls are not drawn, every probe still shows a non-background grey that matches itself. The T-66 break row "the band out by 0.1 / distance 0.05" passed the frame, and was caught only by the `get_mesh` z check added afterwards (line 104), which reads no pixel.

The gatehouse's cornice (stage 2, wall 14, `GatehouseFrames.Look` shades from the first probe) rests on the same self-calibrated shade. I did not run either.

Fix: T-67's rule. Use `SeenNarrow` probes on the band's walls, with the +Y wall predicted from the box's top. Show it failing on a flush-band render break.

**Outcome:** fixed

In 975a25b; the gatehouse cornice's same weakness fixed in e9ab5d3.

### The +y and -y axis frames and their sheet tiles have no check worked from the geometry

**Severity:** Major

`scenarios/Carve.Scenario.Gatehouse/GatehouseFrames.cs:176-183`. Rubric point: a frame with no check a wrong camera would fail. The tile loop skips the ray-test `Look` for `+y` and `-y` (`if (Views[tile][1] is 'y') continue;`). The shed `Camera` builds its view with `UnitY` as up (`ShedGeometry.cs:123,137`), which is degenerate for a view along Y. Nothing records why the skip is there.

What remains for `gate-7-axis-plusy-ortho-256`, `gate-7-axis-minusy-ortho-256` and their two sheet tiles:
- `Tiled`: the sheet tile against the single frame, product against product;
- a white pixel in the caption corner;
- `NotBackground` at the tile centre.

The wall seen from above and from below is a strip through the centre either way. So a view table with `+y` and `-y` swapped passes, as does a top view drawn with the wrong up vector. B3 (columns reversed) was caught only because it moved `+x` onto `+y`.

This contradicts T-69's acceptance line "Every frame has a positive pixel check from the ray test". The person was not sent the six axis tiles separately (T-69 As built, Review).

**Outcome:** fixed

In 88644c5.

### Suite inputs miss three files the projects link (one dropped by the refactor)

**Severity:** Major

`tests.json:260-281` (scenario-cuts) and `tests.json:283-310` (scenario-gatehouse). Rubric point: suite inputs that miss a linked file. cairn's `dotnet-closure` is only the project directories, following `ProjectReference`s (`<cairn>\scripts\cairn\generators.py:159-174`). It does not include `<Compile Include=... Link=...>` files from other directories. That is why the other suites list their links by hand (scenario-soft lists `RunContent.cs`, line 247). Three are missing:

- **scenario-cuts** omits `scenarios/Carve.Scenario.Crate/ColourRule.cs`, linked by `Carve.Scenario.Cuts.csproj:23`. It drives `KnifeStage.Predicted` and `CutsChecks.Predict`.
- **scenario-gatehouse** omits `scenarios/Carve.Scenario.Cuts/CutsChecks.cs`. The refactor `dc27cde` added that link (`Carve.Scenario.Gatehouse.csproj:20`) but did not add it to the inputs. The gatehouse takes `Predict`, `Summary`, `ToVector` and `Expect` from it.
- **scenario-gatehouse** omits `tests/Carve.CharacterLoad.Tests/RunContent.cs`, linked by `Carve.GatehouseLoad.Tests.csproj:24`. It is the loader and sample reader of every load check.

A change to any of these alone keeps a carried pass on the suite that compiles it. For example, an edit to `ColourRule.Scale` re-runs scenario-crate and scenario-gatehouse, but not scenario-cuts.

**Outcome:** fixed

In fc20c5c; every Compile Link checked, and server-unit and server-regression also lacked CameraPixels.cs.

### Through the window: the sure-probe count cancels for the window's own probes

**Severity:** Minor

`scenarios/Carve.Scenario.Gatehouse/GatehouseFrames.cs:223-228, 268-272`. Rubric point: a check skipped silently when no probe is found.

- `WindowBackground()`'s six probes are filtered out of `all` when the ray test is not sure of them, before the `Equal(-1, seen)` at line 237.
- The count check is `expected.Sure + window == tested`, where `window` is the number of window probes that *survived* the filter. So it holds whatever number of window probes are sure, zero included.
- In `gate-7-bridged-window-ortho`, the six background probes, the frame's only positive "background through the window" check, can therefore all drop out with no failure.

Core already shows all six are sure (`GatehouseGeometry.SeenStraightThroughTheWindowIsTheBackground`). The scenario should pin `window == 6`, or add 6 to `Expected[(7, "straight")]`.

**Outcome:** fixed

In 66bf323.

### Two scenario counts are typed, not worked in Core

**Severity:** Minor

Rubric point: a count asserted in the scenario but not worked from the geometry.

- `GatehouseFrames.cs:140`: "reveal faces the camera surely sees" is the constant 2. That depends on `SeenNarrow` at each reveal's middle under the front camera. Unlike `Expected` and `Wires`, `GatehouseGeometry` works no such count; it has no labels test at all.
- `PlaneCutStage.cs:320` ("the cut edges in sight", 3) and `LoopCutStage.cs:83` ("the loop edges in sight", 4) are hand counts with a normal-dot > 0.2 rule. I checked both by hand and they are right, but no test works them.

**Outcome:** fixed

In d70603f.

### The load check's "live triangles only" is measured against the product's own triangulation

**Severity:** Minor

`tests/Carve.GatehouseLoad.Tests/GatehouseLoadTests.cs:46-51`. Rubric point: a probe whose expected value comes from the thing checked. Both the expected index count and the `live` corner set are `Carve.Core.Shading.Triangles(mesh)`, the same function the export draws with.

If `Triangles` gave a tombstoned face triangles, the export and the expectation would agree, and the test named "live triangles only" would pass. The test's comment says a deleted vertex at the origin draws nothing, but the origin is not excluded independently.

The count can be worked from the 19 live faces (n - 2 + 2·holes each). Alternatively, assert that no index lands on a position only a deleted vertex has.

**Outcome:** fixed

In 3061e05.

### "The asymmetry a mirrored camera fails" reads no pixel

**Severity:** Nit

`tests/Carve.Core.Tests/GatehouseGeometry.cs:231-247`. The scenario's matching "faces shown" check (`GatehouseFrames.cs:272`) has the same issue. Both are worked by the scenario's own ray test from the geometry, so neither can fail on a camera fault in the product. The mirrored camera is caught by the silhouette `Background` and `NotBackground` checks (T-69 B1, 404 failures). The test's comment and T-69's "Asymmetric checks are included that a mirrored camera would fail" credit the wrong check.

**Outcome:** fixed

In 8b3ea61; renamed and its comment corrected; T-69's As built left as the record it is.

## Findings: the whole diff (claude-opus-5-5)

### subdivide reports the wrong created vertex range on a mesh with a tombstone

**Severity:** Major

`src/Carve.Server/SubdivideTool.cs:77` (`first = edited.Vertices.Count - result.NewVertices.Count`) against `src/Carve.Core/MeshEdits.cs:333-343`. This is where M-18's `subdivide` meets T-64's tombstones (rubric 1). T-64 made `Subdivide` keep a deleted face's slot, and it makes that face's face point a tombstone from birth. `NewVertices` leaves that vertex out, but it still takes a slot inside the appended block, in face order before the edge points. So the tool's `{first, count}` range is shifted. It names a tombstone as created and leaves out a real new vertex. The same body's `triangles` (`:63`) adds `Corners.Count - 2` for each tombstone slot, so every deleted face takes 2 off the count. That count drives the "more than 5000 triangles" warning.

Failing input (run against Core at 7fa17dd): `define_mesh`'s unit cube, `delete {"faces":[3]}`, then `subdivide {"levels":1}`. The new vertices are 8, 9, 10 and 12 to 25, and 11 is a tombstone. The tool reports `created.vertices = {first: 9, count: 17}`, which is 9 to 25: it includes 11, which `get_mesh` shows as `null` and an edit refuses as `deleted`, and it misses 8. The real faces are 20 quads, which is 40 triangles; it reports `triangles: 38`. Only Core's `TombstoneSweepTests.SubdivideKeepsTombstonesAndAddsNoLiveStray` covers subdivide after a delete. No tool test covers it, and D-1 records that agents track ids from `created`.

**Outcome:** fixed

In cdb1b01, the same defect as the server finding.

### Suite inputs miss three linked files, two of them added by M-5's own moves

**Severity:** Minor

`tests.json` (rubric 6, and 7 for the refactor). cairn's `dotnet-closure` covers project directories and `ProjectReference`s only, not `<Compile Include=... Link=...>`. That is why the other suites list their linked files by hand (`scenario-soft` lists `tests/Carve.CharacterLoad.Tests/RunContent.cs`). Three are missing:
- `scenario-gatehouse` lacks `scenarios/Carve.Scenario.Cuts/CutsChecks.cs`. The refactor `dc27cde` linked it into `Carve.Scenario.Gatehouse.csproj` but did not add it to the suite.
- `scenario-gatehouse` lacks `tests/Carve.CharacterLoad.Tests/RunContent.cs`, which `Carve.GatehouseLoad.Tests.csproj` links.
- `scenario-cuts` lacks `scenarios/Carve.Scenario.Crate/ColourRule.cs`, which `Carve.Scenario.Cuts.csproj` has linked since T-67 and which `CutsChecks.Predict` uses. `scenario-gatehouse` does list it.

For example, if `CutsChecks.cs` changes, `scenario-cuts` runs again, but `scenario-gatehouse` carries its old pass even though its checks were compiled from the changed file.

**Outcome:** fixed

In fc20c5c; the same defect as the scenarios finding "Suite inputs miss three files the projects link".

### Several tool descriptions leave out the `split` and `deleted` fields their reports carry

**Severity:** Minor

The seam is between T-64 and T-65, and T-68 ran beside T-65 (rubric 1, on the descriptions as content). M-5's planning ruled that "every edit's report gains `split` and `deleted`". `MeshToolSupport.Edit` (`src/Carve.Server/MeshToolSupport.cs:170-171`), `SubdivideTool` and `DefineMeshTool` add both fields to every report. But the "Returns JSON {...}" text of these tools omits them:
- `split` is missing from `delete` (`DeleteTool.cs:19`), `merge_faces` (`MergeFacesTool.cs:27`) and `bridge` (`BridgeTool.cs:25`).
- Both fields are missing from `define_mesh` (`DefineMeshTool.cs:33`), `move` (`MoveTool.cs:20`), `extrude` (`ExtrudeTool.cs:22`) and `subdivide` (`SubdivideTool.cs:23`).

Only the three cut tools list both. An agent reading `bridge`'s or `merge_faces`'s text is not told that `split` is there.

**Outcome:** fixed

In ce879cf.

### README "Cutting": two examples fail on the cube the README says they use

**Severity:** Minor

`README.md:69` says "the examples are on `define_mesh`'s unit cube", which does not match the code (rubric 1). On that cube (`DefineMeshTool.CubeExample`):
- `merge_faces {"faces":[2,3]}` names the bottom face (`[0,1,5,4]`) and the top face (`[3,7,6,2]`). They are not connected, so the call is refused. The tool's own example is on a cube that `loop_cut` has split (`MergeFacesTool.cs:31`).
- `bridge {"loops":[[0,1,2,3],[4,5,6,7]]}` names loops whose edges each have two faces, not one, so the call is refused. The tool's own example is on two separate squares (`BridgeTool.cs:29`).

The same section also says `merge_faces` joins "coplanar" faces, but the ruling is "connected faces only, a non-planar result is a warning". And it says `plane_cut` slices "keeping a side and capping it", but `keep` defaults to `both`, which only splits.

**Outcome:** fixed

In 4389b62.

### D-1 does not record faces with holes or Carve file version 4

**Severity:** Minor

`docs/design/D-1-carve-concept.md:95-97`, *Polygon meshes* (Settled, Rule), still says faces are "stored as plain indexed vertex loops". Since T-63 a face is an outer loop plus hole loops, and that is how `define_mesh`, `get_mesh` and the file take it. D-1 records no exception or extension for this, although T-64's tombstones were added to the same paragraph. *A Carve file* (`:127-131`) is extended by D-6 but not by M-5: version 4, holed faces and `null` tombstones, and the person's "lowest version that holds the model" rule (2026-11-11) live only in the milestone file and in `CarveFile.cs`'s remarks (rubric 5).

**Outcome:** fixed

In 6626bd2, documentation only, no test.

### The milestone Retrospective still says B-3 is open

**Severity:** Minor

`docs/milestones/M-5-cut.md`, under *Never verified*: "filed as B-3 under M-5, for the fix round before `finish`, and is not done". Under *Known and left*: "B-3: ... It is under M-5, open." But B-3 is `**Status:** Fixed` and merged (`f21a8e6`), and its fix is in `LoopCut.cs:76-78` with tests (rubric 7). Also, "12 suites passing" predates the rebase: `tests.json` now has 13, with `scenario-rig` from M-17. The Retrospective also does not mention the three refactor commits made after it (`dc27cde`, `6322324`, `7fa17dd`) or their run (`artifacts/review/refactor/`).

**Outcome:** fixed

In c4b7540: B-3 is recorded as fixed, the suites as 13, and the refactor and this review round are added.

### CLAUDE.md's Layout table is behind the code in several rows

**Severity:** Nit

`CLAUDE.md` Layout:
- The `Carve.Scenario.Cuts` row describes only `HolesStage.cs`. It names neither the five later stages nor `CutsChecks.cs`.
- The `Carve.Scenario.Gatehouse` row says it links "the shed's camera and the cuts' ray test". It also links the shed's checks, `CutsChecks.cs` and the Crate's `ColourRule.cs`.
- The `Carve.Scenario.Crate` row does not mention `ColourRule.cs`, which Cuts and the gatehouse now share.
- `scripts/run-gatehouse-scenario.ps1` has no row, unlike each of the other `run-*-scenario.ps1` scripts.
- The `tests/Carve.Core.Tests` row does not say that the project now references `Carve.Debug` and links four scenario files for `GatehouseGeometry`.

**Outcome:** fixed

In 2d0aa12.

## Summary

Round 1 of M-5's review, of the milestone rebased onto `main` (`7d957b4`) at `7fa17dd`, after the refactor pass. The diff is 113 files and about 10,000 lines, touching persistence (Carve file version 4), topology, five cut algorithms, the MCP surface and a large body of pixel checks. So it was split by area, with four reviewers, plus one pass over the whole diff for what falls between them. All five ran in parallel, each on Opus, at least as strong as every implementer.

30 findings: 6 Major, 17 Minor, 7 Nit. Two defects were each found by more than one reviewer:

- `subdivide`'s report after a delete was found by three: Core topology, server tools and the whole diff.
- The suite inputs missing linked files were found by two: scenarios and the whole diff.

Each is answered once and cross-referenced at the other.
