---
description: >-
  check warns of unmapped, flipped, zero-area and overlapping UV faces
---
# T-60 — UV faults in check

**Status:** Done

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

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5).

**The shared helper** (own early commit, `src/Carve.Core/UvArea.cs`, for T-55 and T-56): `static double UvArea.Signed(Face face, IReadOnlyList<Vector2> uvs)`. The signed shoelace area (half the sum) over `(u, v)` in double: the outer loop's plus each hole's, one UV per `Face.Corners` entry in that order. Holes wind opposite the outer loop, so a hole's area is subtracted. Negative is unflipped, positive flipped, magnitude is the UV area. Unchecked on the corner count.

**Warnings** (in `check`'s `warnings`, as `element: message`, only when some face is mapped; after `MeshReport`'s warnings, per-face ones by face id, then overlaps):

- `faces[n]: no UVs, so the face samples the texture's corner`
- `faces[n]: UVs flipped, so the texture is mirrored on the face` (area > 0)
- `faces[n]: UVs of zero area, so the face is one smeared texel` (|area| not above `PolygonMesh.ZeroAreaTolerance` x (UV bounds' diagonal)^2; a smeared face is not also called flipped, and takes no part in overlap)
- `faces[lo]: UVs overlap faces[hi], so the two faces show the same texels; mirrored halves sharing a texture area are deliberate and still warned of`, once per pair, under the lower id, when the triangles' common area is above 1e-6.

**Stats** gain `uvs: {mapped, unmapped}` (live faces), left out for a mesh with no mapped face (seams alone count as none). `HealthStats.Uvs` is a `UvStats?`; `MeshWarningKind` gains `UvUnmapped`, `UvFlipped`, `UvZeroArea`, `UvOverlap`. The code is `src/Carve.Core/MeshUvHealth.cs`: each face is triangulated as in space (holes bridged, on corner indices, so a hole is not covered), the triangles are oriented, pairs are filtered by UV bounds and overlap is Sutherland-Hodgman clipping in double, in face-id order. The description (`CheckTool.Text`) says each warning's meaning and fix.

**Decisions to know.** Overlap tolerance and the flip rule are the planner's; unmapped is warned only when the mesh has some UV (the spec's "when a mesh has any UVs"), so the shed's `check` is unchanged. The overlap fix in the description is `unwrap` or a seam where islands collide; mirrored halves are named as deliberate.

**Tests.** `tests/Carve.Core.Tests/MeshUvHealthTests.cs`, `UvAreaTests.cs` (unit) and `CheckToolTests.cs` (regression, through the tool). Planted breaks are in `artifacts/review/T-60/breaks.md`.

### Ruling after the fact (the person, 2026-11-13)

A zero-area UV face is warned of only as zero-area, not also as flipped: kept as built.
