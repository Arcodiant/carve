---
description: >-
  An agent marks a mesh smooth and marks hard edges, and render and export draw the same per-corner normals
---
# T-42 — Smooth normals and hard edges

**Status:** Done

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

### As built

Run on Sonnet 5.5 (several agents in turn; the last built the cylinder and finished).

Rulings of the person, as built:

1. Corner normals are angle-weighted.
2. An open edge is a plain boundary; an edge of three or more faces is hard. `mark_edges` accepts both.
3. Two faces sharing an edge but wound the same way make that edge hard.
4. `save` writes version 3 whenever a mesh has a shade: smooth, or flat with hard edges marked (corrected after RV-27). A file whose meshes are all flat with no mark stays version 2, byte for byte.
5. `render`'s `normals` look shows corner normals for a smooth mesh, with a spike on each corner along its corner normal; flat meshes are bit-identical to before.
6. (2026-11-10) A sixteen-sided cylinder (axis Y, radius 1) built in the cube scenario through `define_mesh`, `set_shading` and `mark_edges`, both cap rims hard: a soft side all round and a crease at each rim. Frames `cylinder-flat`, `cylinder-smooth`, `cylinder-flat-normals`, `cylinder-smooth-normals` (direction 2 2 1, orthographic; not symmetric about x = z, so a mirrored camera draws another frame). The person approved them (round-2).

**RV-27 fixes (Opus 5.5):** the normals look coloured each smooth triangle's corners B and C with each other's normals, since `MeshBridge` reverses the winding for the engine; `CornerColours` now follows its A, C, B order, and the test compares each colour with the vertex it lands on. A flat mesh drawn beside a smooth one keeps its face-centre spikes, at rest and posed (`PolygonMesh.FlatFaces`, `Shading.FaceHasCornerNormals`). `MarkEdges` lists a refused pair once in either order. New tests: a mis-wound shared edge is hard, and skinned `.glb` export writes corner normals.

Also: stale marks are dropped, extrude keeps the shading mode, `.glb` export takes corner normals from `Shading`.

Pixel checks of the cylinder frames, worked from the mesh: flat side facet is a plateau and the flat cap one shade; smooth side runs on across a facet edge (within 2) and grades across one facet; the hard-rimmed cap keeps its flat shade and breaks from the side below the rim; in normals, red falls across one smooth facet, red/blue order the two sides of the image, green puts the cap over the side.

New checks shown failing (local runs with mutated sample points, reverted):

| Mutation | First check that failed |
|---|---|
| every cylinder point mirrored in x | flat side facet is one shade: (169,169,166) against (160,160,158) |
| every cylinder point flipped in y | flat cap against the side below the rim: 171 and 171 |
| smooth step skipped | smooth side runs on across a facet edge: 166 against 171 |
| smooth-frame points mirrored in x | smooth normals, red across one facet: 254 against 253 |
| smooth-frame points flipped in y | smooth cap keeps one shade: (171,171,169) against (171,171,168) |

Cube-frame checks failing (earlier): normals look on face normals, mirrored and flipped sample points (red 188 against 188; red 142 against 222; blue 177 against 197; blue 134 against 225; green 134 against 183).

Hash guard: baseline e9f4e8e (origin/milestone/M-18). Cube job J-20261110-143023-5f8705 (scenario-cube, verify run): all 20 frames of the baseline cube run are byte-identical; the only additions are the cube smooth/creased/all-hard/normals frames and the four cylinder frames. Crate, shed, character and export were guarded earlier on this product code (ad-hoc runs, every shared file identical); since then only the cube scenario file changed. Not re-fetched by job id after the cylinder commit.

Not checked: cylinder frames were not opened by the agent (the person reviewed them); the thresholds 2 and 3 on the shaded ramp are calibrated to measured values (flat steps across facets are only 5 in green), the normals frames carry the strong checks.
