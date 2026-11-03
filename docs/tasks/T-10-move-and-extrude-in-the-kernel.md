---
description: >-
  Carve.Core moves vertices, edges and faces and extrudes or insets faces, keeping ids and refusing a bad edit by element
---
# T-10 — Move and extrude in the kernel

**Status:** In Progress

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** T-8

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

`Carve.Core` has the two edit operations the crate is built with. Each either returns a valid mesh and what it created, or refuses with every problem named, and the mesh it was given is left as it was.

## Scope

In `src/Carve.Core`, engine-free, on T-8's `MeshTopology` and `Edit` entry point. Name the static class (for example `MeshEdits`):

- **`EditResult`**: the new `PolygonMesh` or a list of `MeshError`, plus what was created: new vertex ids and new face ids, and for extrude a per-face record (`face`, the cap's id, which is the same, the wall face ids in loop order and the new vertex ids in loop order).
- **`Move(mesh, vertices, edges, faces, by)`**: the union of the listed vertices, the two ends of each listed edge and every vertex of each listed face moves once by `by`. **Errors**:
  - an unknown vertex or face id;
  - an edge `[a, b]` that is not an edge of the mesh (`TryGetEdge`);
  - nothing listed;
  - `by` zero or non-finite.

  A face bent out of plane is legal, and comes back as the usual warning.
- **`Extrude(mesh, faces, distance, inset = 0)`**: each listed face separately (no region extrude).
  - **Geometry**: a new vertex for each corner, offset inward in the face's plane by `inset`, measured perpendicular to each edge (a mitred offset, so a rectangle insets evenly and a concave face too), then along the face's Newell normal by `distance` (negative recesses).
  - **Ids**: the face's loop is replaced by the new ring, so the cap **keeps the face's id**. One quad wall per edge is appended, `[old k, old k+1, new k+1, new k]`, so walls wind outward for both signs of `distance`. New vertices are appended in loop order, then walls, face by face in the order listed.
  - **Errors**:
    - an unknown id;
    - a face listed twice;
    - `distance` and `inset` both zero;
    - a negative or non-finite inset;
    - **an inset that collapses or inverts the face**: any offset edge reversing direction, or the inset loop self-intersecting. The error names the face and says the inset is too large for it.

    Every result also passes `Create`'s validation, which may refuse it by element.
- Errors list every problem, like `define_mesh`: elements are `faces[n]`, `vertices[n]` and `edges[a-b]`, with argument positions where it helps, e.g. `faces[1] (the 2nd listed)`.

Tests in `tests/Carve.Core.Tests`, a new class per operation:

- Move a vertex, an edge and a face of the cube: the right vertices move, and ids and face loops are unchanged.
- Moving one top corner up gives a non-planar warning.
- Extrude the cube's top by 1: 12 vertices, 10 faces, closed, no warnings, the top keeps id 3 and its walls wind outward (normals checked).
- Recess by -0.2 with inset 0.2: closed, and the cap's normal is unchanged.
- Inset only, distance 0, on all six faces: closed, 54 faces. This is half of the planning probe's crate.
- An inset on the concave L-block cap: valid, every offset edge parallel to its original.
- An inset of 0.6 on a unit face is refused naming that face.
- Each move error and each extrude error named above.
- **The crate as a test**: the planning probe's call sequence (below) through `Move` and `Extrude` gives a closed mesh with 72 vertices, 70 faces and no warnings, with bounds `(-0.02, 0, 0)` to `(1.02, 0.8, 1)`.
- **Show each new check failing** on a broken implementation (such as walls wound inward, or the inset check removed) and say so in the closing report.

No scenario: T-11 puts these on screen through tools, and T-12 films the crate.

## Notes

- **Decided by the person:** an extruded face keeps its id as the cap, and walls are new. One face at a time. A distance of 0 with an inset is a plain inset. A bad edit is an error with the mesh unchanged. A non-planar result is legal with a warning.
- **The crate's sequence**, as probed at planning (`artifacts/m2-probe/crate_probe.py`, a stand-in, not this code), starting from the `define_mesh` example cube (faces 0 -Z, 1 +Z, 2 -Y, 3 +Y, 4 -X, 5 +X):
  1. `move faces [3] by (0, -0.2, 0)`.
  2. `extrude faces [0..5] distance 0 inset 0.12`.
  3. `extrude faces [0..5] distance -0.08`.
  4. For each X face: `extrude [f] distance 0 inset 0.05`. Then move its top and bottom edges toward each other by 0.17 each. The +X side does this with two edge moves, and the -X side with vertex moves. Then `extrude [f] distance 0.1`.

  That makes 11 edit calls after the seed, 12 in all.
- **Open rules: stop and ask.** The inset's meaning on a face with a reflex corner beyond the mitre (cap it, or refuse it). Any new tolerance. A behaviour for an extrude of faces that share an edge other than "each separately".
- Files owned: new files in `src/Carve.Core` and `tests/Carve.Core.Tests`. Do not change `MeshTopology`'s public shape without saying so in the closing report.
- **Design-heavy, and decides what others build on**: `EditResult` is the shape T-11 returns to agents. Put its public names in the closing report.
- Leave the worktree before `finish`.
