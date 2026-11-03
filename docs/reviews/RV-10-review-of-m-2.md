---
description: >-
  Review of M-2
---
# RV-10 — Review of M-2

**Status:** Answered

**Reviews:** M-2

**Round:** 1

**Previous:** —

**Diff:** aa5a8496fca21a3d9d936a942be8acebe6332406 against origin/main

**Reviewer:** four reviewers, each claude-opus-5-5: Core kernel; Server edit tools and history; render and scenarios; the whole diff

## Findings: Core kernel (claude-opus-5-5)

Checked and found sound: the edge table (directions, open, non-manifold and flipped input), the id contract (`Edit` clones and the cap keeps its id), the wall winding for both signs, the mitre formula, and input immutability. A throwaway randomised probe ran 50k concave star-shaped faces with random insets. It found no false accepts: no accepted ring had flipped orientation, fell outside the face or came closer than `inset` to any edge. It found no false refusals against an independent mitre ring check. All 116 Core tests pass.

### Errors are not exhaustive: Create-level problems are hidden behind any argument error

**Severity:** Minor

`MeshEdits.cs:63-64` (Move) and `:138-139` (Extrude) return `Refused(errors)` before `mesh.Edit` runs whenever an argument error exists. So any geometry refusal that `Create` would add never shows up. Example: on a unit quad, `Move(mesh, [2, 9], null, null, (-1,-1,0))` reports only `vertices[9]`. Fix that id and the next call is refused again with `faces[0]: face folds back on itself`. The same happens to Extrude: one face's too-large inset hides another face's zero-area walls. T-10 Scope says "Errors list every problem, like `define_mesh`". As built admits this ("Create's own refusals come back only when the arguments are clean") but the spec does not allow it. Suggested fix: when the only errors are per-element ones, still call `Edit` with the valid subset and append its errors. `Edit` already merges its own id errors with `Create`'s (`PolygonMesh.cs:308`). Keep the early return only for `by`, `distance` or `inset` being non-finite or zero.

**Outcome:** fixed

`Move` and `Extrude` now run the edit on the ids that exist and append what `Create` refuses to the id errors; they still stop early on a bad or zero `by`/`distance`/`inset` or nothing listed (fixer A, df59ba9; test shown failing on the old code).

### Edit messages format numbers with the current culture

**Severity:** Minor

`MeshEdits.cs:90` and `:122` interpolate `{inset:G6}` with the current culture. Under `de-DE` the probe gave `faces[0]: the inset 0,6 is too large for this face: …`. So agent-facing text depends on the host's locale, and `ExtrudeTests.RefusesAnInsetTooLargeForTheFace` (asserts `"inset 0.6 is too large"`) fails on any machine with a comma decimal separator. The Server already formats its numbers with `CultureInfo.InvariantCulture` (`RenderTool.cs:168`, `DefineMeshTool.cs`). Fix: use `string.Create(CultureInfo.InvariantCulture, $"…")` or `inset.ToString("G6", CultureInfo.InvariantCulture)`. `MeshReport.cs:379` (`{worst:G3}`, `{limit:G3}`, older code) has the same problem.

**Outcome:** fixed

a `MeshEdits.Invariant` helper formats every number in Core's edit, report and index messages; text in the invariant culture is unchanged (df59ba9; `Numbers_and_signs_in_edit_text_do_not_follow_the_current_culture` shown failing).

### A too-small or too-large distance is refused against wall ids the caller has never seen

**Severity:** Minor

When `Create` refuses the walls, the errors name the appended ids with no link to the extrude. On a single-quad mesh:

- `Extrude(mesh, [0], 1e-30f)` returns `faces[1]: face has zero area | faces[2] … | faces[3] … | faces[4] …`.
- `distance = 3e38f` gives the same four errors, because the walls are too thin for their size under `ZeroAreaTolerance`.

`faces[1..4]` do not exist in the mesh the agent sent. Nothing says the distance caused it or which listed face is involved. Suggested fix: in `Extrude`, map a `Create` error whose element is an appended wall back to its source face. For example, `faces[0]: the distance 1E-30 is too small for this face: its wall faces[1] has zero area`.

**Outcome:** fixed

a refused wall or ring vertex is named by its source face and the distance or inset that caused it (df59ba9; shown failing at 1e-30 and 3e38).

### A move too small to change any coordinate succeeds as a no-op

**Severity:** Nit

`MeshEdits.cs:30` only refuses `by == Vector3.Zero`. `Move(mesh, [1], null, null, (1e-10f, 0, 0))` on a vertex at x = 1 succeeds with `MovedVertices = [1]` and a bit-identical mesh. That leaves an undo-history entry that changes nothing, and the error text ("nothing would change") is meant to prevent exactly that. Suggested fix: after computing the new positions, refuse with the same `by` error if every moved vertex is bit-equal to its original.

**Outcome:** fixed

a move that leaves every vertex bit-equal is refused with the zero-move `by` error (df59ba9; shown failing).

### The corner fold-back refusal is unreachable and untested

**Severity:** Nit

The check at `MeshEdits.cs:175-181` (`1 + p₁·p₂ ≤ 0`) needs two adjacent edges running exactly opposite. `Create` already refuses such a face, as a fold for 4 or more corners and as zero-area for a triangle, so no accepted mesh reaches this check. No test covers it, and the As built break table lists no break for it. Either delete it, or keep it as a guard with a comment saying it is defensive, so it is not counted as covered.

**Outcome:** fixed

kept, commented as a defensive guard that no test covers (df59ba9).

## Findings: Server edit tools and history (claude-opus-5-5)

What holds up: `ModelStore` is sound. `Edit` reads the current mesh, runs the kernel and pushes the result all under one lock, so no edit can commit over a newer snapshot. `Undo` and `Replace` take the same lock. `Current` is a single record published with `Volatile.Write`, so it cannot be torn, and `Version` only ever rises by 1. The undo window keeps the current mesh plus 100 older ones, and the tests would catch an off-by-one in either direction (`UndoDepth` 99 or 101). A failed or throwing edit pushes nothing. The tool reports are built from the snapshot that was committed. The JSON field names match T-11. `get_mesh` with no mesh is a tool error. Nothing in these files writes to stdout.

### Parse errors number a list by position, the kernel's errors by id

**Severity:** Minor

`MeshToolSupport.Ids` and `Pairs` (MeshToolSupport.cs:84-120) name a bad entry by its position in the list (`{name}[{k}]`). The kernel's refusals name the id instead (`faces[9]` means face 9; the test at EditToolTests.cs:162 relies on this with `vertices[98]`). So `move {"faces":[3,"x"],"by":[0,1,0]}` reports `faces[1]: expected an integer id, got a string`, and `faces[1]` is also a real face, the +Z one. `{"faces":[3,9]}` reports `faces[9]`. An agent reading `faces[1]` will think face 1 was refused. Suggested fix: name the position in words, as Core does ("the 2nd listed entry ..."), or show the bad value instead of a bracketed index.

**Outcome:** fixed

a bad entry is named by its place in words, e.g. `faces: the 2nd listed entry: ...` (4f1d968; rows shown failing).

### Edit tools reject `3.0` as an id, though `define_mesh` accepts it

**Severity:** Minor

`Ids` and `Pairs` accept only what `TryGetInt32` takes (MeshToolSupport.cs:93, 114-115). That refuses `3.0` and `3e0`. `define_mesh`'s `TryIndex` (DefineMeshTool.cs:430) deliberately accepts whole numbers written that way, and DefineMeshTests.cs:157-158 tests it. So `define_mesh` takes `faces:[[0,1.0,2]]`, but `move {"faces":[3.0],"by":[0,1,0]}` fails with `faces[0]: expected an integer id, got 3.0`. Clients that serialise numbers as floats, such as Python's `json.dumps(3.0)`, hit this. Suggested fix: move `TryIndex` into `MeshToolSupport` and use it in `Ids` and `Pairs`.

**Outcome:** fixed

`TryIndex` moved into `MeshToolSupport` and is used by `define_mesh`, `Ids` and `Pairs`; `3.5` stays an error. The person chose to accept it (4f1d968; shown failing).

### Unknown or misspelled arguments are ignored and the edit goes through

**Severity:** Minor

The schemas leave out `"additionalProperties": false`, and `Run` never looks at argument names it was not given as parameters. The SDK's binder drops extra keys without a word. So `extrude {"faces":[1],"distance":-0.2,"Inset":0.1}` succeeds as a plain recess with no inset. It also becomes an undo step, and the agent gets no hint that its inset was dropped. `move {"face":[3],"by":[0,1,0]}` fails only by luck, as "nothing to move". `define_mesh` has behaved this way since M-1, but in the edit tools a silent success does more harm. Worked out from the code and the SDK's binding; not run. Suggested fix: inject the raw `CallToolRequestParams.Arguments`, report each unknown key as a problem in the same tool error, and add `additionalProperties: false` to each schema.

**Outcome:** fixed

by the person's decision, all six tools report every unknown key in one tool error with the mesh unchanged, and every schema has `additionalProperties: false`; `render` is now registered through `RenderTool.Create` in place of `WithToolsFromAssembly` (4f1d968; rows shown failing).

### The argument-parsing branches have no tests

**Severity:** Minor

Of the parsing paths in MeshToolSupport.cs, only `by` with two elements, a missing `by`, a missing `distance` and missing `faces` are tested (EditToolTests.cs:155-168). These have no test:

- an `Ids` value that is not an array, or holds a non-integer;
- a malformed pair in `Pairs`, such as `edges:[[0]]`, `[[0,1,2]]` or `[["a",1]]`;
- a non-number `distance` or `inset`;
- a non-number inside `by`.

For example, if `Pairs` took the first two elements of `[0,1,2]`, no test would fail, yet "edges as `[a, b]` pairs" is a Scope point. The Scope also requires each such error to come back as a tool error that leaves `get_mesh` unchanged, and that is unchecked for these cases. Suggested fix: add these inputs as rows of `A_bad_edit_is_a_tool_error_naming_the_problem_and_changes_nothing`.

**Outcome:** fixed

rows added for non-array and non-integer ids, `3.5`, malformed pairs, and non-number `distance`, `inset` and `by` (4f1d968).

### A number too large for a float gives a misleading error

**Severity:** Nit

`Number` and `Vec3` cast the parsed double straight to float (MeshToolSupport.cs:142, 164). A value such as `distance: 1e39` becomes infinity, and Core then reports "the distance is not finite" for a finite number. `1e400` fails `TryGetDouble` and is reported as "expected a number, got 1e400", though it is a number. `define_mesh` handles both on purpose (DefineMeshTool.cs:385). Suggested fix: report "out of range" for any value beyond float range.

**Outcome:** fixed

reported as out of range for `distance`, `inset` and `by[k]` (4f1d968; shown failing).

### The `get_mesh` sample output is never checked against the real output

**Severity:** Nit

The `Output:` line in GetMeshTool.cs:174 is excluded from the worked-example test (As built says so). `Get_mesh_lists_vertices_faces_and_edges_with_the_report_fields` happens to assert face 3 and edge `[0,1]`, but not that edge's faces `[0,2]`, and nothing ties the description text to the code. The sample can go stale without any test failing. Suggested fix: parse the entries shown in the `Output:` line and assert each one appears in the cube's real `get_mesh` result.

**Outcome:** fixed

a test parses the `Output:` line and checks each entry shown against the cube's real output (4f1d968).

## Findings: render and scenarios (claude-opus-5-5)

Checked with no finding:

- **Camera:** the up rule at the 1° cone and exactly along ±Y has no NaN path. The eye stays outside the bounding sphere for any target, because |eye| ≥ distance ≥ 1.2, and the near and far planes still hold the sphere. Zoom never moves the eye. The target is mapped through `DrawnSpace`.
- **Framing:** a flat mesh or one with zero extent on an axis frames correctly.
- **Errors and B-1:** every argument error on T-9's list is checked before the mesh is read. B-1's float cast adds no false accept beyond half a float ulp.
- **Pixel checks:** no tolerance is looser than 10/255, and the crate's checks fail on a blank frame and on a missing bar.

Not run: the evidence is the code, plus md5 hashes of the frames kept in `artifacts/scenarios/` (no image was opened).

### The `[0, -1, 0]` cube frame is byte-identical to the `[1, 0, 0]` frame, so it proves nothing about up or "from below"

**Severity:** Major

T-9's Scope says the cube from `[0, -1, 0]` "proves the -Z up". In the kept run, `artifacts/scenarios/cube/1/frames/cube-dir-0--1-0.png` and `cube-dir-1-0-0.png` have the same md5 (`f7e6d571…`). The light turns with the camera, so a face seen head-on gets the same shade from any axis. A cube is symmetric, so its head-on image is the same square whatever the up vector.

The checks at `scenarios/Carve.Scenario.Cube/CubeScenario.cs:84-87` (corners, 1 shade, centre) therefore still pass in these broken cases:

- up is +X or +Z instead of -Z;
- the direction's sign is flipped, giving `[0, 1, 0]`;
- the camera looks from the side.

Only a NaN failure would be caught. T-9's As built shows the break only as a render error, and the up choice is tested only at unit level (`CameraFramingTests.Up_is_minus_Z_only_inside_one_degree_of_vertical`).

Fix: render the L-block (already in the scenario) from `[0, -1, 0]`, and sample one pixel that is mesh under -Z up and background under +Z up or from above, for example on the notch side. Show it failing with up swapped.

**Outcome:** fixed

added `lblock-dir-0--1-0-corner`, the L-block from `[0,-1,0]`, zoom 3, target `[0,0,0]`. The review's sample could not work, because from -Y the L-block is a plain rectangle under either up; with the target on the corner, the quarter that holds mesh tells up and direction. Shown failing with up swapped to +Z; the person approved the frame (fixer B, 55a21b5).

### A render's target is checked against the mesh at call time, but the frame may show a newer mesh

**Severity:** Minor

Where it happens:

- `RenderTool.cs:94-111` checks the target against `store.Current`.
- `RenderQueue.Post` (`RenderQueue.cs:164`) then reads the version again, separately.
- The pump shows the newest mesh whenever the queue's head isn't due (`RenderPump.cs:43-47`).
- `RenderSystem.ApplyView` (`RenderSystem.cs:125`) maps the target through that newer mesh's `DrawnSpace`.

Failing input: a client sends `render{direction:[2,1,1], target:[1.02,0.4,0.5]}` on the crate, and at the same time a `define_mesh` (or a `move`) that shrinks the mesh to the unit cube. The render can succeed: the caption gives the new mesh's counts and a target outside its bounds, where the spec says that is an error. The frame is still sane, because the eye bound holds for any target. Nothing hangs. Without concurrent calls it cannot happen, because the next render re-checks.

Fix: re-check the target against the shown mesh's report on the frame thread, in `ChangeView` or in `ShowMesh`'s re-apply. Fail the request with the same error text.

**Outcome:** fixed

a shared `TargetCheck` is used by `RenderTool` and by the pump against the mesh actually shown, with the same text; a pump test reproduces the race, shown failing with the re-check off (55a21b5).

### The margin test cannot fail on a frame that is too far away

**Severity:** Nit

`CameraFramingTests.cs:55-56` says "the others reach the margin" but asserts only `tightest ≤ Inner`. If every non-M-1 direction framed the mesh at half size (say, the distance doubled), the test would still pass. Only `front` and `back` are pinned, by `Front_and_back_are_the_M1_frame_exactly`.

Fix: for every case except the edge-on flat quad, also assert `tightest ≥ Inner - 1e-3`.

**Outcome:** fixed

the lower bound is asserted for every case but the edge-on quad; shown failing (55a21b5).

### The pump tests cannot see a change of zoom or target

**Severity:** Nit

The stub scene reduces every camera to Front or Back by the sign of `Direction.X` (`RenderPumpTests.cs:51`). So no test covers:

- a request that differs only in `Zoom` or `Target` causing a `ChangeView`;
- `ShowMesh` re-applying a camera with a target after a new mesh, which is what maps the target through the new `DrawnSpace`.

The code is right (record equality at `RenderPump.cs:62`), but a regression to comparing only `Direction` would pass every test here. End to end, only the zoom-only change is covered (`RenderTests` `A_direction_along_the_front_view_…`).

Fix: log the whole `CameraSpec` in the stub, and add those two cases.

**Outcome:** fixed

the stub logs the whole `CameraSpec`; zoom-only, target-only and target-after-new-mesh cases added (55a21b5).

### Caption `view back, zoom 2` is a reading the spec did not settle

**Severity:** Nit

T-9's caption spec gives two forms: the view form, for zoom 1 with no target, and "otherwise" the direction form. The code shows `view` with a zoom or target as `512x512, view back, zoom 2, …` (`RenderTool.cs:147`), which is tested in `RenderTests` (`The_caption_echoes_…`). It is a sensible reading and is recorded in T-9's As built. But it is missing from the Retrospective's list of "Readings members landed that the spec did not settle".

Fix: add it to that list, or confirm it with the person.

**Outcome:** no change needed: in code. Added to the Retrospective's list of readings members landed (96ac2af).

### Crate `back` check is labelled as the bar's top but samples its underside

**Severity:** Nit

`CrateScenario.cs:59` names the constant `BackBarTop`, and line 125 reports "the -X bar's top". The comment and T-12's As built both say the pixel is the bar's underside (118), because `back` looks up from below. A failure would name the wrong face.

Fix: rename the constant to `BackBarUnderside` and fix the message.

**Outcome:** fixed

renamed to `BackBarUnderside`, with the message saying underside (55a21b5).

## Findings: the whole diff (claude-opus-5-5)

### Edit ids are parsed more strictly than `define_mesh` indices

**Severity:** Minor

`define_mesh` (M-1, `DefineMeshTool.TryIndex`) accepts a whole number written as `1.0` or `1e0` as an index. Its comment says it does this because `TryGetInt32` refuses those forms. T-11's shared readers, `MeshToolSupport.Ids` and `Pairs` (`src/Carve.Server/MeshToolSupport.cs:95-130`), use only `TryGetInt32`. So `move {"faces":[3.0],"by":[0,0.5,0]}` fails with `faces[0]: expected an integer id, got 3.0`, while `define_mesh` takes `[0,3.0,2,1]` as a valid face. A client that writes JSON numbers as floats can define a mesh but cannot edit it. The two tools also word the same mistake differently. Suggested fix: move `TryIndex` into `MeshToolSupport` and use it in `Ids` and `Pairs`, then add an `EditToolTests` case with `3.0`. (The same as the Server reviewer's second finding.)

**Outcome:** fixed

by the same change as the Server finding (4f1d968).

### "Ids are never reused" is untrue once undo exists

**Severity:** Minor

Three places state the id rule without a qualifier:

- the `PolygonMesh` remarks (`src/Carve.Core/PolygonMesh.cs`): "an id is never reused within a mesh's history";
- D-1 *Polygon meshes*: "an edit never renumbers or reuses an id";
- the milestone decision: "edits never renumber or reuse one".

T-11's undo breaks this. Scenario: `extrude {"faces":[1],"distance":0.3}` reports walls 6-9 and ring vertices 8-11. Then `undo`, then `extrude {"faces":[3],"distance":0.3}`. Walls 6-9 and vertices 8-11 now name different elements. An agent that kept ids from the undone report, which T-13 says agents do ("tracked ids from each report's `created` and `extruded` fields"), edits the wrong elements with no error. `undo`'s description says only "with the ids it had". Suggested fix: limit the rule to "never reused along the current chain of edits". Add one sentence to `undo`'s text: ids created by the undone step will be given out again by the next edit.

**Outcome:** fixed

by the person's decision, the rule is qualified to the current chain of edits in the `PolygonMesh` remarks, D-1 and the milestone decision, and `undo`'s description says the next edit gives the undone ids out again (96ac2af, 4f1d968).

### No test covers an edit that the whole-mesh validator refuses

**Severity:** Minor

A move or extrude can pass its argument checks and still be refused: `PolygonMesh.Edit` runs `Create`'s validation on the result. That path (`EditResult.From` → `MeshToolSupport.Failed`, mesh and undo unchanged) is the only edit failure an agent actually hit: T-13 round 2, a face folding mid-`move`. Commit `68ab036` then promised in `move`'s description that this is checked "as a whole, not corner by corner".

Nothing tests it:

- `MoveTests` has no refusal of this kind;
- every `ExtrudeTests` refusal is caught before `Edit`;
- `EditToolTests.A_bad_edit_is_a_tool_error_naming_the_problem_and_changes_nothing` covers only argument errors;
- the crate scenario's two error calls are both argument errors.

`MeshTopologyTests.EditRefusesWhatCreateWouldRefuseByElement` covers `PolygonMesh.Edit` alone. Suggested fix: add a `MoveTests` case and an `EditToolTests` case for a bow-tie move, e.g. vertex 6 of the cube moved by `[-2,0,0]`, or whatever input `Create` refuses. Each should check that the error names the face, that `get_mesh` is unchanged and that `undo` still undoes the previous step. Also add the description's "as a whole" claim: two corners moved together legally, one at a time refused.

**Outcome:** fixed

Core tests for the bow-tie move and the "as a whole" claim, and a tool row checking that `get_mesh` is unchanged and `undo` still undoes the previous step (df59ba9, 4f1d968). These are coverage tests; they pass on the old code, which was right.

### Texts written for M-1 still say every mesh comes from `define_mesh`

**Severity:** Nit

T-11 added `move`, `extrude` and `undo`, but some text was not updated:

- `RenderPump.ShowMesh` (`src/Carve.Server/RenderPump.cs:~85`) reports "The mesh from define_mesh could not be put in the scene", although the mesh may now come from `move`, `extrude` or `undo`.
- `render`'s description still says "fix the winding and call define_mesh again" and "define_mesh still works there". The edit tools also work with no Vulkan device.

Suggested fix: write "The current mesh could not be put in the scene" and name the edit tools beside `define_mesh`.

**Outcome:** fixed

`RenderPump.ShowMesh` says "The current mesh could not be put in the scene" (55a21b5); `render`'s description names the edit tools beside `define_mesh` (4f1d968).

### `extrude`'s error list leaves out the zero edit

**Severity:** Nit

Following the milestone's *Bad edits* decision, Core refuses `distance 0, inset 0` (`MeshEdits.Extrude`, element `distance, inset`). `move` lists "a zero ... by" among its errors, but `ExtrudeTool.Text`'s "Bad input (...)" list does not mention this case. Suggested fix: add "distance and inset both 0" to that list.

**Outcome:** fixed

"distance and inset both 0" added (4f1d968).

### The changelog and the `CLAUDE.md` Layout row say things that are not so

**Severity:** Nit

- `docs/changelog/UNRELEASED.md`: the B-1 line describes the bug ("is refused ... because ...") rather than the fix.
- The same file's T-13 line says "recovering from a planted mistake". The Retrospective says the plant was never hit.
- `CLAUDE.md` Layout still describes `Cube` as "a cube, an L-block, a long box, both views". T-9 added four direction and zoom frames to that scenario.

Suggested fix: reword the three lines.

**Outcome:** fixed

the B-1 and T-13 changelog lines and the Layout row reworded (96ac2af).

These checks found nothing:

- **Refactor `70e99c6`:** it changes no behaviour, tool text, error text or schema. The JSON key order is the same, the error texts are identical, and the dropped `length == 0` test is covered by `< 1e-20f`.
- **Architecture:** no stdout write, clock or `System.Random` is added. Server edit logic only parses arguments. Scenarios reference only `Carve.Extensibility` and `Carve.Debug`.
- **Tests:** `Tier` traits are correct, and the `scenario-crate` suite is present.
- **D-1:** its updates match the build, and no new engine workaround needs citing.
- **Contracts:** the `CameraSpec` target (mesh space, mapped in `RenderSystem.ApplyView`) and the `MeshTopology`/`EditResult` contracts are used as their writers meant.

## Summary

The diff is about 2.8k lines over seven members (T-8 to T-13 and B-1) and the refactor commit `70e99c6`, reviewed at that commit. It was split by area, because the members are layered and each area holds one risk:

- the Core kernel (T-8, T-10): geometry and the id contract;
- the Server edit tools and history (T-11, T-13): concurrency in `ModelStore`, argument parsing and tool text;
- render and the scenarios (T-9, T-12, B-1): the camera and whether the pixel checks can fail;
- one pass over the whole diff, for contracts between members, the refactor, architecture rules and test coverage.

All four ran in parallel, on Claude Opus 5.5, at least as strong as every implementer.

Round 1 raised 1 Major, 11 Minor and 11 Nits (23 findings). Two findings are duplicates: the strict id parsing appears under both the Server and the whole-diff parts.
