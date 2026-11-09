---
description: >-
  An agent sets per-vertex weights and renders the model in a given pose, skinned by Carve.Core
---
# T-32 — Explicit weights and posing

**Status:** Done

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-30, T-31

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent weights vertices by hand and sees the model bend, so skinning can be judged before there is any animation.

## Scope

- **Core:** per-mesh vertex weights, as D-6's *Weights* says; `Pose` (per-bone local rotation and translation, rest where not given); and linear blend skinning to posed positions and normals, in double, narrowed once.
- **Tool:** `set_weights(mesh, vertices | group, weights)`, with weights by bone name, so a hat is all `Head` in one call. The result says if influences were capped, and how many vertices are still unweighted.
- **Render:** a `pose` argument, drawn as a plain CPU-posed mesh through `MeshBridge`. Framing: confirm D-6's working assumption of rest-pose bounds, and record the outcome in D-6.
- **Sample:** two boxes on a two-bone chain, bent at the joint.
- **Tests:** `unit` for skinning against hand-worked values, the rest pose returning rest positions exactly, and normalisation; a frame of the bent chain whose pixels a flipped bend would fail.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

One agent, on Sonnet 5.5.

- **Core:** `VertexWeights` (`src/Carve.Core/VertexWeights.cs`) is immutable: vertex id to at most four `Influence(bone index, weight)`, normalised to sum 1; `Normalise` keeps the largest four, ties to the lower bone index, and says if it capped. It hangs on the mesh (`PolygonMesh.Weights`, `WithWeights`), carried through `Move` and `Extrude` (an extrude's new vertices start unweighted); a new `define_mesh` or `open` of a name starts without weights. *Superseded by T-35 for `open`:* `open` with a name carries the file mesh's weights across by bone name (`Model.WithMeshFrom`); `define_mesh` still starts without weights (RV-24 A7). `Pose` is per bone name an optional rotation and an optional translation, both absolute and local to the parent (as clip keys are), rest where not given. `Skinning` does linear blend skinning in double, narrowed once: `Positions`, `Normals` (blended rotation of given per-vertex normals, renormalised; the render does not use it, since the flat-shaded draw takes face normals from the posed triangles) and `Posed(model, pose)`, which gives a model of posed meshes (`PolygonMesh.WithVertices`, unchecked, since a blend need not keep faces planar). `Model.Combined` now joins meshes unchecked for the same reason.
- **Store:** `ModelStore.SetWeights` (one undo step) and the T-31 rule: `DefineSkeleton` is refused, under the lock, when any mesh has weights and the bone names or order differ (`Skeleton.SameNamesAs`); the same names with new rest transforms are accepted. Clips join that rule in T-33.
- **Tool:** `set_weights(vertices | group, weights, mesh?)`, weights by bone name (`{"Head":1}`), normalised; `group` is a face group and weights every vertex of its faces. Result `{mesh, set, capped, unweighted, weights}`: `capped` is how many of the vertices set lost influences to the cap, `unweighted` how many vertices of the mesh still have none. `define_skeleton` says it refuses a rename once weights exist.
- **Render:** `pose` argument (`bone: {rotation, translation}`); the pump shows the mesh again, posed, when a request's pose differs from the one shown. Framing is on the rest bounds (`IRenderScene.ShowMesh(mesh, frameOn)`), recorded in D-6's open question: it holds for the chain, and a pose that swings geometry past the rest bounds leaves the frame (the price of a still frame; not changed).
- **Sample:** `ChainPose.cs` in `scenario-crate`, run last: two 0.4 m boxes, `mesh` (replacing the crate under its name; weighted to `base` by vertex ids) and `upper` (to `joint` by group), bones `base` at the origin and `joint` at y 1; frames `chain-rest-z`, `chain-pose-empty-z`, `chain-bent-z`, `chain-bent-front`, `chain-rest-front`, `chain-rest-z-again`.
- **Pixel check:** head-on from +Z, +X is image right. After a quarter turn about +Z the arm must lie on the left: points on the +Z faces (z 0.2) projected from the mesh at x -0.4, -0.7, -0.9 must be the lit face colour (within 12 per channel of the lower box's pixel in the rest frame); x +0.4, +0.7 (a reversed bend, or a camera mirrored in X), where the upper box stood at rest, and above and below the arm must be background; the lower box is unchanged; every posed corner projects inside the image. An empty pose renders byte-identical to no pose.
- **Readings the spec left open:** pose values are absolute, not offsets from rest; a vertex with no weight stays where it is in a pose; `capped` counts vertices; an extrude's new vertices are unweighted rather than inheriting; weights are not saved by `save` (as colours are not; *superseded by T-35*, whose file keeps both, RV-24 A7); a pose is checked for unknown bones and unit rotations only, with the skeleton's 1e-3 tolerance. `EditToolTests.Tools_list_shows_each_schema` gained `pose` in render's argument list (a new argument, as T-29's ruling 2).

**Checked.** `core-unit`, `server-unit`, `server-regression`, `scenario-cube`, `scenario-crate` via `verify`. All 36 existing crate frames are byte-identical to T-30's `hashes-after.txt` (`artifacts/review/T-32/hashes-before-crate.txt`, `hashes-crate-after.txt` in M-16). The person approved the bent chain frames. Break table (`break-table.txt`): the bend flipped fails the scenario at the arm check and the unit tests; X mirrored fails the scenario; each other break (translation ignored, composition order, no normalising, cap keeping the smallest, tie to the higher bone, cap not reported, skeleton rule off or comparing nothing) fails a new unit test. Not run: `scenario-shed` and `scenario-export` (only `Model.Combined` for several meshes changed, which they do not use).
