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

Run on Sonnet 5.5 (second agent; the first stopped at the open rules, which the person ruled on 2026-11-09: corner normals angle-weighted; an open edge is a plain boundary; an edge of three or more faces is hard; `mark_edges` accepts both).

- **Done and committed, `cairn task T-42 verify` is VERIFY PASS** (server-unit, scenario-cube, scenario-crate, core-unit, server-regression):
  - Core: `src/Carve.Core/Shading.cs` (`ShadingMode`, `MeshShade`, `ShadedTriangle`, `Shading.Triangles/Corners/CornersOf`); `PolygonMesh.Shade`, `ShadingMode`, `HardEdges`, `WithShade`, `PosedCorners` (internal); soft edge = exactly two faces wound opposite ways and not marked hard; a corner averaging one face keeps `FaceNormal` bit for bit, so flat meshes equal `FlatShading`. `EditResult` carries the shade through move/extrude and drops marks on edges that no longer exist. `Model.Combined` keeps shading per mesh (a flat piece of a mixed model has all its edges hard). `Skinning.CornerNormals` and `Skinning.Posed` carry posed corner normals for smooth weighted meshes.
  - File: `Write` gives version 3 only when a mesh has a shade (so every existing flat file stays version 2, byte for byte), else 2; reads 1, 2, 3; v3 keys `shading`, `hard_edges` (a v2 file with them is refused). The version-text tests now say "versions 1, 2 and 3", and the version-3 refusal tests use 4.
  - Server: `set_shading`, `mark_edges` (`ShadingTools.cs`, `ModelStore.Reshade`); `MeshBridge`, `GlbExport`, `ExportTool`, `SkinnedExport` take normals from `Shading`. The normals look still shows face normals.
  - Tests: `ShadingTests`, `ShadingFileTests` (core, unit); `ShadingNormalsTests` (render versus loaded normals bit for bit, unit) and `ShadingToolTests` (regression); the two tools in `NamedMeshTests`' every-tool theory.
  - Frames: section 2c of `CubeScenario.cs`: `cube-smooth-dir-1-1-1-ortho`, `cube-creased-dir-1-1-1-ortho` (top face's four edges hard), `cube-smooth-all-hard-dir-1-1-1-ortho` (checked equal to the flat hexagon frame pixel for pixel). Pixel checks passed on the first run.
- **Left:**
  1. The byte-identical guard on existing frames by hash before and after: no baseline run was found (`cairn test status` lists only this branch's jobs). Fetch scenario-cube and scenario-crate for a pre-T-42 commit (or run the suites on `origin/milestone/M-18` in a throwaway worktree), compare `run.json` image hashes with this branch's jobs `J-20261110-045904-6b39c4` (cube) and `J-20261110-045909-fe46c1` (crate; fetch by job id, to `--out` a Windows path, since bash `/tmp` differs).
  2. Look at the three new frames (crops; none opened yet) and the "each new check shown failing, as a table" item (for instance by running the smooth section against a flat mesh, or with a mirrored sample point, and listing which check fails).
  3. Write `### As built` (the rulings, soft-edge definition, version 3 only when needed, stale marks dropped, extrude keeps the mode, normals look unchanged, model Sonnet 5.5), remove this section, `cairn lint`, `cairn task T-42 finish` from inside the worktree, quoting the `Merge task/T-42` line. Consider adding a CLAUDE.md layout/architecture note (the `.glb` export no longer always has flat normals).
- **Surprising:** the first bash heredocs that held an apostrophe in text failed; use the Write tool for such files.
