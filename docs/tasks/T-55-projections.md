---
description: >-
  project_uvs lays chosen faces' UVs by planar, box or cylindrical projection into a rectangle
---
# T-55 — Projections

**Status:** In Progress

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-54

**Verify:** core-unit, server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent gives a set of faces UVs in one call by projecting them flat along a direction, onto a box or round a cylinder, placed in a rectangle of UV space it chooses.

## Scope

- **Core** (`src/Carve.Core/UvProjection.cs`), engine-free and deterministic: each method takes a mesh, a face set and its options, and returns corner UVs for those faces only. Other faces keep theirs.
- **The tool, `project_uvs`** (`src/Carve.Server/ProjectUvsTool.cs`):
  - Arguments: `mesh`, `faces` (default all), `method` (`planar` | `box` | `cylindrical`), the method's options, and `into: [u0, v0, u1, v1]` (default `[0, 0, 1, 1]`).
  - One undo step. A refused call changes nothing.
  - Returns the faces mapped and the UV bounds of the result; for `box`, which faces went to which axis.
- **planar:** takes `view` (render's names: `+x` ... `-z`, `front`, `back`) or `direction`, exactly as `render` takes them.
  - The faces' corners are laid out as `render` from that direction shows them: u to the image's right, v down, with render's own up rule (+Y, or -Z within 1 degree of vertical).
  - A face seen from its own outside side therefore comes out unflipped (rule below).
  - One uniform scale fits the projected bounds into `into`, keeping their aspect, centred.
- **box:** each face goes to the axis its normal leans on most (ties go to x, then y, then z), and is projected planar as that axis's head-on view shows it.
  - The six groups sit in a 3 x 2 grid of equal cells inside `into`: `+x`, `-x`, `+y` on the first row and `-y`, `+z`, `-z` on the second.
  - All groups share one scale, so texel density is even, and each is centred in its cell. An empty group leaves its cell empty.
- **cylindrical:** takes `axis` (default `[0, 1, 0]`), `centre` (a point on the axis; default the faces' bounds centre) and `seam` (a direction across the axis; default `[0, 0, -1]`, the back).
  - u is the angle round the axis from the seam, increasing to the right as seen from outside with the axis pointing up.
  - v is the distance along the axis, downward from the faces' highest point.
  - Arc length and height share one scale (at the faces' mean radius), fitted into `into` keeping aspect.
  - A face crossing the seam keeps its corners together, so it may run past the right edge of `into`.
  - Faces square to the axis (caps) come out with zero UV area. The description says to project caps `planar`.
- **Unflipped** (shared with T-60, decided at planning): a face's UVs are unflipped when, drawn with u right and v down as on screen, its corners in loop order run counterclockwise. Equivalently, the shoelace sum over `(u, v)` is negative.
- **Refusals**, each a tool error by element: an unknown method or face; an option the method does not take; `view` with `direction`; a zero `direction`, `axis` or `seam`; a `seam` along the axis; an `into` that is not finite or has no area.
- **The description is content:** it says what each method suits (planar for one side, box for crates and buildings, cylindrical for barrels, posts and limbs) and carries a worked example on `define_mesh`'s unit cube.
- **Tests:**
  - `unit`: planar of the unit cube's +Z face from `+z` gives its corners exactly; box of the cube gives six squares in the grid at one scale, every face unflipped; cylindrical of `TestMeshes.Barrel()`'s side faces 0 to 31 puts the seam at the -Z vertex column (vertices 2, 10, 18, 26, 34), with u rising across each ring and v rising down the rings, and no face crossing the seam; the default `into` and a given one.
  - `regression`: the tool through the real `carve`, every refusal, and undo.

## Notes

- Sonnet: the methods are set here, and the arithmetic is small.
- Runs side by side with T-56, T-57, T-59 and T-60. It owns `UvProjection.cs` and `ProjectUvsTool.cs`. Its only shared edits are the tool list (add only) and `TestMeshes` (add only); keep both sides in any conflict.
- **Write down for the dependants:** the result shape; T-61 box-projects the crate with it.
- **Carried from earlier plans:** as T-53's Notes (the hash guard, a break per new check as a table, `finish` read whole, artifacts by job id, report a long `verify` wait). Evidence goes in the milestone worktree's `artifacts/review/T-55/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, and any loosened check.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`), one agent, 2026-11-13, on `task/T-55` from `milestone/M-6`.

**Core** (`src/Carve.Core/UvProjection.cs`): `Planar(mesh, faces, direction, Rect into)`, `Box(mesh, faces, into)` (returns `BoxResult { Uvs, Axes }`) and `Cylindrical(mesh, faces, axis, centre?, seam, into)`, each giving `face -> Vector2[]` (one per `Face.Corners` entry, holes included) for the faces asked only. Maths in double, each UV rounded once. `UvProjection.Rect(U0, V0, U1, V1)` with `IsValid` (finite, u1 > u0, v1 > v0). Signed area is not in Core: the unflipped test is private to the tests.

**The tool `project_uvs`** (`src/Carve.Server/ProjectUvsTool.cs`):

- Arguments: `method` (required: `planar` | `box` | `cylindrical`), `faces` (default every live face), `into` (default `[0,0,1,1]`), `mesh`; planar `view` (render's eight names, default `front`) or `direction`; cylindrical `axis` (`[0,1,0]`), `centre` (default the faces' bounds centre), `seam` (`[0,0,-1]`). Box takes none.
- Result: `{"mesh", "method", "mapped": [face ids, ascending], "bounds": [u0, v0, u1, v1] (min/max of the UVs written), "axes": {"+x": [ids], "-x": [], "+y", "-y", "+z", "-z"}}`; `axes` only for box, all six keys always present. One undo step (`ModelStore.Rework`); other faces keep their UVs.
- Errors (in `project_uvs failed with N problem(s); the current mesh is unchanged:`): `method: required but missing`; `method: 'x' is not a method; the methods are planar, box, cylindrical`; `faces[9]: face 9 is not a face of the mesh, which has 6`; `faces[5]: face 5 is deleted, so it cannot be mapped`; `faces[1]: face 1 is listed more than once`; `faces: no faces to map; ...` (empty list); `view: not an option of box; box takes no options` (likewise `axis: ... planar takes view, direction`); `view and direction: give one, not both`; `view: 'up' is not a view; the views are ...`; `direction|axis|seam: must not be zero, and must be a usable size`; `seam: lies along the axis; give a direction across it`; `into: [..] has no area; u1 must exceed u0 and v1 exceed v0`; `into: expected 4 numbers`; `into[2]: expected a number` / `... is out of range`; unknown arguments; no mesh.

**Choices the Notes did not settle.** An `into` with u1 < u0 or v1 < v0 is refused as having no area (no flipped rectangles). Duplicate face ids are refused (as `set_uvs`). Planar with neither `view` nor `direction` is `front`, as `render`. Box's axis tie uses a tolerance of 1e-6 on the normal's components. Cylindrical fits the bounds of the faces' own angles, capped at one turn: a full ring spans `into`'s width exactly, and a face crossing the seam overruns the right edge. A corner on the axis takes its face's first off-axis corner's angle. "Mean radius" is the mean over the faces' distinct vertices. A face edge-on to a planar view has no area; a group or selection with no extent at all collapses to the rectangle's centre.

**For T-61 and T-62.** The crate's `box` call is `project_uvs {"method":"box"}` (or with `mesh`, `faces`, `into`); a crate of one cube's six sides gives six squares in the 3 x 2 grid at one scale, every face unflipped. For a barrel's side, `{"method":"cylindrical","faces":[0..31]}` puts the seam at the vertices 2, 10, 18, 26, 34 column (-Z) with u rising along the loop; its caps want `{"method":"planar","view":"+y"}` or `-y`. Read results back with `get_mesh` (`uvs`).

**Tests.** unit: `tests/Carve.Core.Tests/UvProjectionTests.cs` (11); regression: `tests/Carve.Server.Tests/ProjectUvsToolTests.cs` (the described examples, planar/direction/undo, a holed face, cylindrical options, 21 refusals each leaving the mesh unchanged and undo to the earlier step, a deleted face and no mesh).

**Guard.** The git blob hashes of every tracked file outside `src`, `tests` and `docs` are identical at the base and at HEAD (86 files: `artifacts/review/T-55/files-before.txt` and `files-after.txt`, identical). The diff lists only the code, tests, `Program.cs` (one tool-list entry) and this file. No frame is rendered or changed.

**Broken-product table** (`artifacts/review/T-55/broken-product.md`, made by `plant.py`; source restored after each):

| Planted break | Fails |
|---|---|
| planar v is not negated (v up) | four unit tests: the +z face's exact corners, the given-rectangle fit, render's up rule, box unflipped |
| box tie goes to y before x | `Box_sends_a_tie_to_x_then_y_then_z` |
| box groups each get their own scale | `Box_leaves_an_empty_group_s_cell_empty_and_shares_the_scale_across_groups` |
| cylindrical angle runs the other way | `Cylindrical_of_the_barrel_side_puts_the_seam_...` |
| cylindrical splits a seam-crossing face | `Cylindrical_keeps_a_face_that_crosses_the_seam_together` and the barrel test |
| projection skips hole corners | `A_holed_face_gets_one_uv_per_corner_holes_included` |
| an option the method does not take is accepted | `Every_refusal_names_its_element_and_changes_nothing` |
| `into` with no area is accepted | the same theory |
| seam along the axis is accepted | the same theory |
| view with direction is accepted | the same theory |
| a deleted face is mapped | `A_deleted_face_and_an_empty_model_are_refused` |

**Not checked:** no frame (UVs are not drawn until T-57).
