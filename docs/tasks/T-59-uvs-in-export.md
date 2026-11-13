---
description: >-
  export writes each corner's UV as TEXCOORD_0, splitting vertices where UVs differ, and warns of unmapped faces
---
# T-59 — UVs in export

**Status:** In Progress

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-54

**Verify:** server-unit, server-regression, scenario-export, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

A model's UVs reach a game exactly: the engine reads back each corner's UV as Carve holds it.

## Scope

- **Every mode that writes a mesh** (`model`, the skinned model, and `piece` if T-50 has landed): when any exported face has UVs, each triangle corner carries its face corner's UV as `TEXCOORD_0`.
  - The corners of unmapped faces are written as `(0, 0)`.
  - No conversion: Carve's UVs are glTF's.
  - A vertex is split wherever its corners' UV or normal differ. SharpGLTF's equal-attribute merge does it, with `VertexTexture1` beside the normals.
  - Ids and the Carve model are untouched.
- **No UVs anywhere means the same bytes as today.** That is guarded by hash on every exported file in `scenario-export` and `scenario-character`.
- **Warning**, in the result: `<mesh>: N faces have no UVs; their corners are written as (0, 0)`, naming the first few face ids, as other warnings name elements.
- **`clips` mode** is unchanged.
- **The description** stops saying "no UVs", and says what is written and the warning.
- **Tests:**
  - `regression`: read back through the engine's `IModelGeometryLoader.Load` from a content folder, as `tests/Carve.CharacterLoad.Tests` does. `TEXCOORD_0` per corner equals Carve's, compared as floats bit for bit, for a static cube with UVs on some faces and for the skinned two-bone chain with UVs.
  - The vertex count per primitive equals the number of distinct (position, normal, UV) corners.
  - The warning text.
  - The hash guard above.

## Notes

- Sonnet: the format is glTF's and the conventions match, as planning's probe showed (a `.glb` with `TEXCOORD_0` and an embedded checker loaded and drew in the viewer, with (0,0) at the image's top-left).
- Runs side by side with T-55, T-56, T-57 and T-60. It owns `GlbExport.cs`, `SkinnedExport.cs`, `ExportTool.cs` and the piece export's file.
- No texture or material change: textures are M-7's.
- **Carried from earlier plans:** as T-53's Notes (the hash guard, a break per new check as a table, `finish` read whole, artifacts by job id, report a long `verify` wait). Evidence goes in the milestone worktree's `artifacts/review/T-59/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any loosened check, and any change to an existing export's bytes.
- Run `finish` from inside the worktree and leave the folder.

### As built

Written by Sonnet 5.5.

- **Corners.** `GlbExport.UvAt(mesh, face, vertex)` reads `mesh.Uvs.Of(face)[face.CornerOf(vertex)]` (hole corners included), (0, 0) for an unmapped face. `TEXCOORD_0` is written for a mesh only when some live face has UVs (`GlbExport.HasUvs`); seams alone write none. The writers are generic over the vertex material (`VertexEmpty` or `VertexTexture1`), so the no-UV path writes the same bytes as before (pinned by SHA-256 in `UvExportTests`, `ExportViewerTests` and `CharacterLoadTests`). SharpGLTF's equal-vertex merge does the splitting on (position, normal, UV).
- **Static export** pairs `Shading.Triangles` with `Triangulator.Triangulate` (same order) to get each triangle corner's vertex id; `ShadedTriangle` is unchanged. **Skinned export** uses each piece's own mesh, so it needed nothing from `Combined`; a model with UVs on any piece writes `TEXCOORD_0` on every primitive.
- **Warning**, one per piece with unmapped live faces, only when some piece has UVs: `<mesh>: N faces have no UVs; their corners are written as (0, 0) (faces a, b, c, d, e and K more)`, the first five ids; one face reads `<mesh>: 1 face has no UVs; its corners are written as (0, 0) (face a)`. `<mesh>` is the piece's name (`mesh` for a default model). In the static result it joins the report's warnings in `N warning(s)`; in the skinned one it is in `SkinnedGlb.Warnings`. `GlbExport.UvWarnings(Model)` builds it.
- **For T-61:** an export of a model with UVs writes `TEXCOORD_0` as Carve holds them (glTF axes); read it back with `IModelGeometryLoader.Load`, vertex field `TexCoord`. The description of `export` says what is written.
- **Ruling (the person, 2026-11-13):** the warning's singular form `<mesh>: 1 face has no UVs; its corners are written as (0, 0) (face a)` is kept as built.
- **Multi-mesh static export** goes through `Model.Combined`, which T-57 now fills with the offset faces' UVs (`Combined` unchanged here). A test writes a model of two meshes with UVs, reads it back through `Load` and checks each corner's UV bit for bit in mesh order, plus the warning per piece (only the second piece has unmapped faces). The code needed no change.
- **Model run on:** Sonnet 5.5 (both sessions; the second rebased onto T-57 and added the multi-mesh test). Checked on the unit and regression fixtures (cube, smooth cube, a two-bone chain) and through the `set_uvs` and `export` tools; not on a real character.
