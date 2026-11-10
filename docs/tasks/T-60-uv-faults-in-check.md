---
description: >-
  check warns of unmapped, flipped, zero-area and overlapping UV faces
---
# T-60 — UV faults in check

**Status:** Open

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-54

**Verify:** core-unit, server-unit, server-regression, scenario-shed

**Perf:** skip: Carve declares no heavy suites

## Goal

`check` tells an agent, by face id, what is wrong with its UVs and what a game will make of it, so faults are found without reading a frame.

## Scope

- **When a mesh has any UVs**, `check` adds warnings in its existing form (`element: message`), each saying what it means for a game:
  - `faces[n]`: no UVs, so the face samples the texture's corner;
  - `faces[n]`: UVs flipped, so the texture is mirrored on the face. The rule is T-55's: unflipped when the shoelace sum over `(u, v)` is negative;
  - `faces[n]`: UVs of zero area, so the face is one smeared texel. The test is the area below `PolygonMesh.ZeroAreaTolerance` times the UV bounds' diagonal squared, as for faces in space;
  - `faces[n]`: UVs overlap `faces[m]`. Two faces' UV triangles overlap by more than 1e-6 in UV area (about a fifteenth of a texel of the 256 checker). Each pair is listed once, the lower id first. Mirrored halves sharing a texture area are deliberate and still warned of, and the description says so.
- **Stats** gain `uvs: {mapped, unmapped}`, left out for a mesh with none.
- A mesh with no UVs gets exactly today's output.
- UV warnings are in `check` only: the edit tools' reports do not change.
- **Core** (`src/Carve.Core/MeshHealth.cs` or a sibling): overlap by clipping UV triangles against each other, pairs first filtered by bounds, in a fixed order.
- **The description** says each warning's meaning and fix: `project_uvs` or `unwrap` the face, `set_uvs` to turn it, a seam where islands collide.
- **Tests:**
  - `unit`: each warning on a small mesh worked by hand, its absence on the clean case, a shared edge not counted as overlap, and an overlap just over and just under 1e-6;
  - `regression`: through the tool;
  - the leaky shed's `check` output unchanged.

## Notes

- Sonnet.
- Runs side by side with T-55, T-56, T-57 and T-59. It owns `MeshHealth.cs` (or its sibling) and `CheckTool.cs`.
- The four warnings were agreed by the person at planning (2026-11-09). The 1e-6 overlap tolerance was set by the planner and is listed in M-6's file for the person's objection. A change to either goes back to the person.
- **Carried from earlier plans:** as T-53's Notes (the hash guard, a break per new check as a table, `finish` read whole, artifacts by job id, report a long `verify` wait). Evidence goes in the milestone worktree's `artifacts/review/T-60/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, and any loosened check.
- Run `finish` from inside the worktree and leave the folder.
