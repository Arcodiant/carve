---
description: >-
  An agent sets UVs by coordinates with set_uvs and marks seams with mark_edges, and get_mesh reads both back
---
# T-54 — set_uvs, seams on mark_edges, and get_mesh

**Status:** In Progress

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-53

**Verify:** server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

Through MCP, an agent gives faces UVs by coordinates, marks and clears seams, and reads UVs and seams back.

## Scope

- **`set_uvs`** (`src/Carve.Server/SetUvsTool.cs`):
  - Arguments: `mesh` (as every tool has taken it since T-29); `faces`, a list of `{"face": id, "uv": [[u, v], ...]}` with one pair per corner in the face's loop order; and/or `clear`, the face ids to make unmapped.
  - One undo step. A failed call changes nothing.
  - Tool errors name the element: an unknown face, a corner count that does not match the face (saying how many corners it has), a non-finite value, a face both set and cleared, an unknown argument.
  - Returns how many faces were set and cleared, and how many faces of the mesh are still unmapped.
- **`mark_edges`** (T-42's tool) gains `seam`: `mark_edges(mesh, edges, hard?, seam?)`.
  - Each flag is optional and at least one is required. `true` marks and `false` clears. The flags are independent, so a call with only `seam` leaves hard edges alone.
  - A pair that is not an edge is refused naming the pair, pointing at `select` with kind `edges` (as `move` does since B-2, if B-2 has landed).
  - T-42's calls and results without `seam` are unchanged.
- **`get_mesh`** reports `uvs` (as the file writes them) and `seams` (sorted pairs). Each is left out when empty, so an existing mesh's output is unchanged.
- **The descriptions are content.**
  - Each tool's description says what a corner is, which way UVs run (glTF: origin top-left, v down), that a value outside 0..1 tiles, and that a split needs no seam.
  - Each carries a worked example on `define_mesh`'s unit cube (one face's four corners).
  - `mark_edges`'s says what a seam is for: `unwrap` cuts there, and the layout draws it.
- **Tests** (`regression` through the real `carve`, or `unit` where they can be):
  - every error, by element;
  - set, clear and undo;
  - `mark_edges` with `seam` only, with `hard` only, and with both;
  - `get_mesh` before and after;
  - `tools/list` schemas updated, as M-16's ruling allows for new arguments.

## Notes

- Sonnet: T-53 and T-42 set the shape.
- **Write down for the dependants:** the result shapes, and the exact text of the non-edge error.
- **Carried from earlier plans:** as T-53's Notes (the hash guard, a break per new check as a table, `finish` read whole, artifacts by job id, report a long `verify` wait). Evidence goes in the milestone worktree's `artifacts/review/T-54/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any loosened check, and any change to `mark_edges`' existing behaviour.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by Sonnet 5.5 (`claude-sonnet-5-5`), one agent, 2026-11-13, on `task/T-54` from `milestone/M-6` at `501bf3f`.

**Ruling of the person (asked mid-task):** `mark_edges` keeps `hard` defaulting to true when neither flag is given, so T-42's calls are unchanged; the spec's "at least one flag is required" is dropped. A call with only `seam` leaves hard edges alone; a call with only `hard` leaves seams alone.

**B-2 had not landed** on `milestone/M-6`, so the non-edge error keeps T-42's wording and does not copy `move`'s.

**Result shapes** (all one JSON object, in one text block):

- `set_uvs(faces?, clear?, mesh?)` returns `{"mesh": name, "set": n, "cleared": n, "unmapped": n}`: faces given UVs, distinct faces cleared, and live faces of the mesh still without UVs. One undo step; a failed call changes nothing. At least one of `faces`/`clear` must be non-empty ("faces: nothing to do: ..."). One pair per corner, outer loop then each hole.
- `mark_edges(edges, hard?, seam?, mesh?)` returns `{mesh, shading, marked: [[a,b]...], hard_edges: [[a,b]...]}` as before, plus `seams: [[a,b]...]` (every seam of the mesh) only when `seam` was passed.
- `get_mesh` adds `uvs: [{"face": f, "uv": [[u,v], ...]}]` (mapped faces, ascending, one pair per corner, floats shortest round-trip as in the file) and `seams: [[a,b]...]` (sorted, a < b), each left out when empty, so an existing mesh's output is unchanged.

**Exact error text, for the dependants.** The non-edge error is `edges[a-b]: not an edge of the mesh; select with kind edges lists the real edges` (a < b, once per pair), inside `mark_edges failed with N problem(s); the current mesh is unchanged:`. `set_uvs` errors (same wrapper), by element: `faces[9]: face 9 is not a face of the mesh, which has 6`; `faces[5]: face 5 is deleted, ...`; `faces[0]: 3 UVs given, but the face has 4 corners; write 4 UVs, one per corner` (with `(its loop 4, holes 4)` for a holed face); `faces[0].uv[2][0]: 1e99 is not finite: a UV must lie within ±3.40282347E+38`; `faces[0].uv[1]: a UV is a pair of numbers, [u, v]; ...`; `faces[0]: face 0 is both set and cleared; ...`; `faces[0]: face 0 is listed more than once`; `clear[12]: face 12 is not a face of the mesh, which has 6`; `color: not an argument of set_uvs; its arguments are faces, clear, mesh`.

**Code:** `src/Carve.Server/SetUvsTool.cs` (new), `ShadingTools.cs` (`MarkEdgesTool`), `GetMeshTool.cs`, `ModelStore.Rework` (a whole-mesh change under the lock, one undo step), registration in `Program.cs`. `set_uvs` uses `MeshUvs.Check` and `WithFaces`; a seam change uses `MeshUvs.WithSeams`. No Core change. Tests: `tests/Carve.Server.Tests/UvToolTests.cs` (regression, real `carve`).

**Guard:** no committed file other than the intended ones changed: `artifacts/review/T-54/files-before.txt` and `files-after.txt` (git blob hashes of every tracked file outside `src`, `tests`, `docs`, at the base and at HEAD) are identical, and `git diff --name-status` lists only the code, tests and the member's file. No frames are rendered.

**Broken-product table** (`artifacts/review/T-54/broken-product.md`; source restored after each):

| Planted break | Fails |
|---|---|
| `mark_edges` with only `seam` also changes hard edges | `Mark_edges_seam_alone_hard_alone_and_both_are_independent` |
| `mark_edges` with neither flag no longer defaults `hard` to true | `A_bad_shading_or_edge_is_refused...`, `Mark_edges_seam_alone_...`, `Shading_and_marks_set_report_undo_...` |
| `mark_edges` seam does not check the pair is an edge | `Mark_edges_refuses_a_non_edge_for_a_seam_...` |
| `get_mesh` leaves seams out | `Mark_edges_seam_alone_...`, `The_described_examples_run_and_read_back` |
| `get_mesh` writes each UV as (v, u) | `Set_and_clear_report_counts_and_undo_restores` |
| `set_uvs` counts deleted faces as unmapped | `A_deleted_face_cannot_be_given_or_cleared` |
| `set_uvs` lets a face be set and cleared | `Every_error_names_its_element_and_changes_nothing` |
| `set_uvs` skips `MeshUvs.Check` | four tests incl. `A_holed_face_takes_one_pair_per_corner_...` |

**Not checked:** no frame (none changed); UVs are not drawn until T-57.
