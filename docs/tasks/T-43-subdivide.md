---
description: >-
  subdivide smooths a mesh with Catmull-Clark, keeping ids, colours, groups, hard edges and weights
---
# T-43 — Subdivide

**Status:** Done

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

### As built

Run on Opus 5.5 (two agents in turn: the first built and verified, the second landed the ruling, the guards and the review, and finished).

**The person's ruling (2026-11-10), as built:** non-planar warnings are folded into one line in `subdivide`'s own result only, after its other warnings: "N faces are not planar (expected after subdivide)" ("1 face is" for one). `get_mesh`, `check` and export stay as they are and still list each face; `select faces where non_planar` names them. If T-44 shows the flood getting in the way there, that is a task of its own. Test: `SubdivideToolTests.The_result_folds_not_planar_warnings_into_one_line_and_check_still_lists_each_face` (the gardener's Body at level 1: no per-face line, one folded line, last, its count equal to `select`'s and to `check`'s per-face lines). Shown failing with the fold undone: `Assert.DoesNotContain() Failure: Filter matched in collection`.

**Built:**
- Core: `MeshEdits.Subdivide(mesh, levels)` (1 to 3, `MaxSubdivideLevels`), one level in `Subdivision.Once`. Every new point is a stencil over the level's vertices, and the same stencil gives its position and its weights. Each level is narrowed to float and capped, so `levels 2` is exactly `levels 1` twice (tested).
- Ids (D-1): each vertex keeps its id at its vertex point. Appended per level: the face points (face order), then the edge points (edge-table order, lo then hi). Face `f`'s child 0 keeps id `f`; its children 1..n-1 are appended face by face. Child k is `[ck, ep(ck,ck+1), fp, ep(ck-1,ck)]`, wound as before.
- Readings, textbook Catmull-Clark (Hoppe/DeRose), landed and not asked: a sharp edge is one marked hard, or any edge `Shading` does not smooth across (open, 3+ faces, same-way winding). A sharp edge's point is its midpoint, and a hard mark goes to both halves. A vertex on 0 or 1 sharp edges takes the smooth rule (a dart), on 2 the crease rule `(a+b+6P)/8` (the boundary rule on an open rim), and on 3 or more it is a corner and stays. A vertex no face uses stays. Hard marks crease even while the mesh is flat. Weights: an unweighted source adds nothing, a bone whose blend is <= 0 is dropped, then `VertexWeights.Normalise` (top four, sum 1). A point with no weighted source stays unweighted.
- `FaceStyle.Carry` is generalised to (face, added faces), with groups looked up by set; extrude calls it unchanged. `EditResult.Made` is for an edit that has already carried its attributes.
- Tool `subdivide(levels, mesh)`: the report plus `triangles` (whole model), `levels` and `created:{vertices:{first,count},faces:{first,count}}`. It returns a range rather than id lists, since level 3 on the gardener would list thousands. When the model is past `SkinnedExport.MaxTriangles` (5000; past, never at), a warning goes first. It is in `Program.cs` and in undo's tool list.
- Tests: `SubdivideTests` (unit, 19 cases): cube counts at levels 1 to 3, id rules, the limit point (Halstead's formula agrees from level 1 and level 4), an all-hard cube keeps its shape, a hard edge carried to both halves, the crease and corner rules, an open box's rim by the boundary rule, colours and groups, weights (<= 4, sum 1, a five-bone blend capped), unweighted stays unweighted, determinism and 2 = 1+1, refusals, an n-gon. `SubdivideToolTests` (regression, 11): the example, undo in one step, the 5000 warning (a 7x6 grid at level 3: 5376 triangles), the folded warning, bad input, and the gardener path for T-44 (open `samples/gardener.carve`, subdivide Body and Hat, save, reopen, skinned export with no unweighted refusal, 2 clips).
- Cube scenario, after the T-42 cylinder: `cylinder-subdivided-smooth` (the hard-rimmed cylinder at levels 2), `cylinder-subdivided-flat-normals` (the same shown flat in the normals look) and `cylinder-subdivided-soft-rims` (a fresh smooth cylinder with no hard edge, at levels 2). All three use direction 2 2 1, orthographic. The person approved them against the before frames `cylinder-smooth` and `cylinder-flat-normals` (round-1, in the M-18/T-43 artifacts folder).

**New checks shown failing** (local runs of the cube scenario; the first check to fail; reverted after each). Mirrored and flipped are the frame's sample points in image space (x to 511-x, y to 511-y), applied to one frame's checks at a time:

| Mutation | First check that failed |
|---|---|
| `subdivided-smooth` points mirrored in x | the subdivided side at its two ends across the image: 149 and 151 |
| `subdivided-smooth` points flipped in y | the subdivided cap keeps one shade: (171,171,169) against (171,171,168) |
| `subdivided-flat-normals` points mirrored in x | red across one old facet, now four: 255 against 255 |
| `subdivided-flat-normals` points flipped in y | red across one old facet, now four: 188 against 188 |
| `subdivided-soft-rims` points mirrored in x | the soft side at its two ends across the image: 149 and 146 |
| `subdivided-soft-rims` points flipped in y | the soft top is the brightest part: 157 against 183 |
| first `subdivide` (hard rims) skipped | `subdivided-flat-normals`, red across one old facet: 228 against 228 |
| second `subdivide` (soft rims) skipped | `subdivided-soft-rims`, just inside the far top rim is not background: (181,181,179) |

The same mutations made on the camera instead (direction 1 2 2 mirrored across x = z, 2 -2 1 from below) leave three of the six frames passing. That is expected, not a weak check: the sixteen-sided cylinder is symmetric across x = z and top to bottom, so those cameras draw a mirrored or identical cylinder rather than a mirrored image.

**Hash guard:** cube, baseline job J-20261110-191945-bc0974 (d1628bf, T-42 as built) against J-20261110-193724-6807d8 (d8c6f58): all 29 baseline frames byte-identical, none missing, 3 new. Re-checked against the as-built verify job J-20261110-211240-18ca3f (4328fba), fetched by job id: the same 29 byte-identical, none missing, the same 3 new (`artifacts/review/T-43/cube-after-4328fba.sha256`). Crate: the queue only carried a pass for `origin/milestone/M-18` (5c2428d) with no job of its own, so both sides ran locally on one machine: 5c2428d in a temporary detached worktree, and this branch's tip. All 44 frames are byte-identical (`artifacts/review/T-43/crate-before.sha256`, `crate-after.sha256`).

**Not checked, and why:**
- `cylinder-subdivided-smooth` has no check of its own that subdivision happened. With its `subdivide` skipped, its checks still pass and the first failure is on the normals frame. A rim check was tried and dropped. The crease pulls the rim in at an old vertex only to about 0.975 of the radius, 2 to 3 pixels at the foreshortened far rim, so the probe sat on the anti-aliased edge. The render also frames the narrower subdivided model about 2.5% larger, so the fixed T-42 projection does not hold at the rim (interior samples are unaffected). Subdivision is proven by the two sibling frames.
- The margins of the subdivided checks are those of T-42's style: the flip on `subdivided-smooth` fails by one unit in blue.
- The agents did not open the frames themselves; the person reviewed them.
