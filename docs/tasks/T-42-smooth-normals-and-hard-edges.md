---
description: >-
  An agent marks a mesh smooth and marks hard edges, and render and export draw the same per-corner normals
---
# T-42 — Smooth normals and hard edges

**Status:** In Progress

**Pri:** P1

**Milestone:** M-18

**Batch:** — (standalone)

**Depends on:** T-36

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

A character's surface reads soft, with creases only where an agent puts them.

## Scope

- **Core:** per-corner normals (`src/Carve.Core/Shading.cs`) as D-6 says, one function for render and export. Flat meshes give exactly today's normals.
- **Tools:** `set_shading(mesh, flat | smooth)`, and `mark_edges(mesh, edges, hard)` with edges as vertex pairs.
- **Skinning:** posed normals come from the corner normals.
- **File:** version 3 adds `shading` and `hard_edges`; versions 1 and 2 still read.
- **Export:** corner normals in `NORMAL`. Render and loaded normals compared in a `unit` test.
- **Tests:** `unit` for a cube smooth, a cube with one hard edge, and non-manifold and open edges; frames of a smooth and a creased shape.

## Notes

- Flat stays the default so no existing frame or file changes.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Hand-off

Run on Sonnet 5.5 (third agent). Handed off at the frame review: the person REJECTED the cube frames with the note "Those are hard to judge; is it easier with a sphere or cylinder?".

- Done since the last hand-off (committed): `LookMeshes` normals look colours each triangle corner from `Shading.Triangles` corner normals (flat meshes are bit-identical, so flat frames do not change) and, for a mesh with `Shading.HasCornerNormals`, stands a spike on each corner along its corner normal instead of one per face; render tool text says so; `CLAUDE.md` `.glb` line updated; unit test `A_smooth_mesh_in_normals_carries_its_corner_normals_in_colour_and_spikes`; cube scenario frames `cube-smooth-normals-dir-1-1-1-ortho` and `cube-creased-normals-dir-1-1-1-ortho` with pixel checks (channel ordering via `CheckChannelGreater`).
- Byte-identical guard DONE: baseline = e9f4e8e (origin/milestone/M-18), ad-hoc runs of `pwsh scripts/run-scenario.ps1 <s>` (jobs J-20261110-053534-a956f7 cube, -053537-987575 crate, -053540-2b9d8c shed, -053543-00fb1f character, -053545-c39a46 export) against HEAD (cube J-20261110-053553-e661f9, crate -052338-c94db3, shed -052340-d00423, character -052342-b8b0f0, export -052343-5eae9d). sha256 of every png/glb/carve file: every shared file identical; the only differences are the 5 added cube frames (smooth, creased, all-hard, smooth-normals, creased-normals) and 2 extra viewer pngs in the suite-run export artefacts (suite versus ad-hoc run, not a change). Artefacts are in the temp folder `Temp\t42` (outside the worktree).
- New checks shown failing (local scenario-cube runs with mutated sample points or server, then reverted):

| Mutation | Check that failed |
|---|---|
| normals look used face normals | smooth normals, red across top: 188 against 188 |
| sample points mirrored in x (smooth top) | smooth normals red: 142 against 222 |
| sample points flipped in z (smooth top) | smooth normals blue: 177 against 197 |
| mirrored (creased +X face) | creased normals blue: 134 against 225 |
| flipped (creased +Z face, y) | creased normals green: 134 against 183 |
| creased top edges made soft before the normals frame | creased normals: top face should be one colour |

- Left:
  1. The person asked for a sphere or cylinder to judge smooth normals. Add a UV sphere (or a cylinder with hard cap rims) smooth frame, shaded plus normals, with pixel checks and a mirror/flip check as for the cube, then re-review. This adds frames beyond the spec, so the caller may want to confirm. The G: drive is absent on this machine, so the review ran from the temp folder with local paths.
  2. `cairn task T-42 verify` clean after the last commit (scenario suites ran PASS on this code; server-unit has not run with the new unit test, which passes locally).
  3. Write `### As built` (rulings 1 to 5, soft-edge definition, version 3 only when shaded, stale marks dropped, extrude keeps the mode, normals look now corner normals, spikes per corner on smooth meshes, model Sonnet 5.5), remove this section, `cairn lint`, `cairn task T-42 finish` from inside the worktree, quoting the merge line.
