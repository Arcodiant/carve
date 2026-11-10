---
description: >-
  subdivide smooths a mesh with Catmull-Clark, keeping ids, colours, groups, hard edges and weights
---
# T-43 — Subdivide

**Status:** In Progress

**Pri:** P1

**Milestone:** M-18

**Batch:** — (standalone)

**Depends on:** T-42

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

Extruded blocks become soft shapes in one call.

## Scope

- **Core:** Catmull-Clark in `MeshEdits`, as D-6's *Subdivide is a destructive edit* says. Hard edges stay creased. Open edges follow the boundary rule. Colours, groups, hard edges and weights carry through; weights use the same stencil, capped at four.
- **Tool:** `subdivide(mesh, levels)`, with `levels` 1 to 3. The result gives counts and warns past the 5,000-triangle budget (T-34's constant).
- **Tests:** `unit` for cube counts, a limit surface check, id rules, weights summing to 1, and determinism; undo in one step; a frame before and after.

## Notes

- D-1's id rule is the hard part: stop and ask if it cannot hold. Stronger model.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Hand-off

Run on Opus 5.5 (first agent; stopped at the checkpoint: code committed and verified, capture review still to come).

**Done** (commit d8c6f58):
- Core: `MeshEdits.Subdivide(mesh, levels)` (1 to 3, `MaxSubdivideLevels`), one level in `Subdivision.Once`. Every new point is a stencil over the level's vertices and the same stencil gives its position and its weights. Ids: each vertex keeps its id at its vertex point; appended per level are the face points (face order), then the edge points (edge-table order, lo then hi); face `f`'s child 0 keeps id `f`, and its children 1..n-1 are appended face by face. Child k is `[ck, ep(ck,ck+1), fp, ep(ck-1,ck)]`, wound as before. Each level is narrowed to float and capped, so `levels 2` is exactly `levels 1` twice (tested).
- Readings (textbook Catmull-Clark, Hoppe/DeRose, not asked): a sharp edge is one marked hard, or any edge `Shading` does not smooth across (open, 3+ faces, same-way winding). A sharp edge's point is its midpoint and a hard mark goes to both halves. A vertex on 0 or 1 sharp edges takes the smooth rule (a dart), on 2 the crease rule `(a+b+6P)/8` (which is the boundary rule on an open rim), and on 3 or more it is a corner and stays. A vertex no face uses stays. Hard marks crease even while the mesh is flat. Weights: an unweighted source adds nothing, a bone whose blend is ≤ 0 is dropped, then `VertexWeights.Normalise` (top four, sum 1). A point with no weighted source stays unweighted.
- `FaceStyle.Carry` is generalised to (face, added faces), with groups looked up by set; extrude calls it unchanged. `EditResult.Made` is for an edit that has already carried its attributes.
- Tool `subdivide(levels, mesh)`. It returns the report plus `triangles` (whole model), `levels` and `created:{vertices:{first,count},faces:{first,count}}`. A range, not id lists, since level 3 on the gardener would list thousands of ids. A warning goes first when the model is past `SkinnedExport.MaxTriangles` (5000, past and never at). It is in `Program.cs` and in undo's tool list.
- Tests: `SubdivideTests` (unit, 19 cases): cube counts at levels 1 to 3, id rules, the limit point (Halstead's formula agrees from level 1 and level 4), all-hard cube keeps its shape, a hard edge carried to both halves, the crease and corner rules, an open box's rim by the boundary rule, colours and groups, weights (≤4, sum 1, a five-bone blend capped), unweighted stays unweighted, determinism and 2 = 1+1, refusals, an n-gon. `SubdivideToolTests` (regression, 10): the example, undo in one step, the 5000 warning (a 7x6 grid at level 3: 5376 triangles), bad input, and **the gardener path for T-44** (open `samples/gardener.carve`, subdivide Body and Hat, save, reopen, skinned export with no unweighted refusal, 2 clips).
- Cube scenario, after the T-42 cylinder: `cylinder-subdivided-smooth`, the hard-rimmed cylinder at levels 2; `cylinder-subdivided-flat-normals`, the same shown flat in the normals look; and `cylinder-subdivided-soft-rims`, a fresh smooth cylinder with no hard edge at levels 2. All three use direction 2 2 1, orthographic. Each frame has a positive check (cap shade unchanged from `cylinder-smooth`; red now steps across one old facet, where before subdividing it was one colour; just inside the far top rim is drawn when the rim is hard and is background when it is soft) and a check that a mirrored camera fails (side ends differ, red +x over +z) or a flipped one fails (cap or top brightest, green cap over side).
- `cairn task T-43 verify`: **VERIFY PASS** (server-unit, core-unit, scenario-cube, scenario-crate, server-regression 345 s).
- Hash guard, cube: baseline job J-20261110-191945-bc0974 (d1628bf, T-42 as built; no product change since) against J-20261110-193724-6807d8 (d8c6f58). All 29 baseline frames are byte-identical and none is missing; 3 frames are new. The hash files are in `artifacts/review/T-43/`.

**Left:**
1. Crate hash guard. No crate baseline job exists in the queue, so submit `scenario-crate` on `origin/milestone/M-18` and compare it with J-20261110-193725-70f53c by hash.
2. Break table: show each new scenario check failing (mutated sample points, mirrored in x / flipped in y, the subdivide call skipped), as T-42 did.
3. Frame review: the frames are copied to `C:\Users\andre\artifacts\carve\M-18\T-43\round-1`. The before frames are `cylinder-smooth` and `cylinder-flat-normals`; the after frames are the three subdivided ones. The agent has not opened any of them.
4. `### As built`, lint, finish.

**Question for the person (open, not landed):** Catmull-Clark quads are almost never planar, and `MeshReport` warns once per non-planar face. Subdividing the gardener at level 1 gives **552 warnings**: in the `subdivide` result, in `get_mesh` and `check`, and in the export result. The tool text says this is expected, but the flood buries every other warning, and T-44 will hit it. Options:
- (a) Leave it as it is.
- (b) `subdivide`'s own result folds the non-planar warnings into one line ("N faces not planar, as subdivided faces are"). The other tools stay as they are.
- (c) The report, `check` and the export fold non-planar warnings past some count into one line, for every tool.
- (d) A mesh-level "subdivided" flag that silences the planarity warning (this changes D-6/D-1).

Recommendation: (b) now, and (c) as its own task if T-44 shows the flood in `check` and export hurts.

**Surprising:** nothing broke. The first verify passed with the new scenario checks as written; their margins have not been measured.
