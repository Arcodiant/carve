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
