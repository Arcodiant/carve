---
description: >-
  export writes each corner's UV as TEXCOORD_0, splitting vertices where UVs differ, and warns of unmapped faces
---
# T-59 — UVs in export

**Status:** Open

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
