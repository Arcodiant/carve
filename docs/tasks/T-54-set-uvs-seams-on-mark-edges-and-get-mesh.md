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
