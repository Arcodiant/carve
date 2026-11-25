---
description: >-
  move, rotate and scale take symmetric x, y or z: each vertex's mirror partner, found by position, follows mirrored, vertices on the plane stay on it, and an unpaired vertex is refused by id
---
# T-212 — Symmetric editing

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** T-210, T-211

**Verify:** core-unit, server-unit

## Goal

An agent edits one side of a symmetric mesh and the other side follows, call by call, with no mode to remember.

## Scope

- **Core, `src/Carve.Core/Symmetry.cs`** (new), on T-211's `MirrorPlane` with `at` 0, in the mesh's own space.
  - **Pairing:** a vertex's partner is the live vertex at its reflection within 1e-5 m. A vertex on the plane is its
    own partner. A vertex with no partner, or with two candidates, is refused, and every such id is named.
  - **The + side leads:** the selection is closed under partners. A vertex on the + side of the plane takes the edit,
    T(v). A vertex on the − side takes R(T(R(v))), its partner's edit reflected. A vertex on the plane takes T(v) with
    its axis coordinate kept on the plane. Listing a − side vertex counts as listing its partner. So listing both
    sides gives the same answer as listing one.
  - `MeshEdits.Move`, `Rotate` and `Scale` take an optional `MirrorPlane? symmetric`. Leaving it out keeps every
    existing result byte for byte as it is.
- **Server:** `move`, `rotate` and `scale` take `symmetric: "x" | "y" | "z"`, and `moved` lists partners too. Each
  description gains the rule in two or three sentences and one worked example, run as written. Move's could be the
  cube's +x face moved 0.2 along +x with `symmetric: "x"` after a centre-on-origin `primitive box`, so both side faces
  move out. Each description stays at or under 3,686; move's is the tight one.
- **extrude is not covered** (decided at planning): extrude, then mirror. The descriptions say so.
- **Tests** (`tests/Carve.Core.Tests/SymmetryTests.cs`, `tests/Carve.Server.Tests/SymmetricEditTests.cs`):
  - the barrel case: a 4-band cylinder's ring 1 scaled in x and z with `symmetric: "y"`, so ring 3 follows and ring 2
    stays on the plane;
  - a tilted rotate on a symmetric L pair;
  - listing one side and listing both give equal meshes;
  - an unpaired vertex refused by id, with an ambiguous pair refused too;
  - no `symmetric`, so results unchanged;
  - ids and layers kept;
  - the worked examples.
- **`README.md`**: the `symmetric` argument, with a worked call.

## Notes

- **Decided at planning (2026-11-25, by the person):** a per-call argument on move, rotate and scale, not a mode the
  server holds. The plane goes through the mesh's own origin, and pairing is by position. Recommended by me: an
  unpaired vertex is refused, not skipped.
- **Mine, not the person's: the + side leads** (above). Any other tie-break for a selection on both sides goes back to
  the person.
- Move's tools/list entry changes, which is expected. Re-take the digests for the `symmetric` sentences and schema
  only, as T-93 did, and say so in `### As built`.
- **Design-heavy** in its rule, not its size: T-215's barrel and T-216's planted fault rest on it.
