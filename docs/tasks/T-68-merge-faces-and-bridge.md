---
description: >-
  merge_faces dissolves connected faces into one, and bridge joins two open loops with a ring of quads
---
# T-68 — Merge faces and bridge

**Status:** Open

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-64

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent tidies up after a cut, merging the pieces it did not want back into one face, and joins two openings with a tunnel, so a hole can go through a wall.

## Scope

**Core: `FaceMerge`** (its own file). `merge_faces(faces)`.

- **What merges.** The listed faces must be live and connected, each sharing at least one edge with another listed face. They become one face: the union's outline, plus holes where the union encloses one (T-63).
- **Ids.** The lowest listed id survives, reshaped. The others are tombstoned (T-64).
- **Vertices.** Vertices left with no live face are tombstoned. Vertices still on the outline stay, including collinear ones (no vertex is dissolved from an outline).
- **What carries.**
  - The surviving face keeps its own colour and groups. A face of another colour among those merged is a warning naming it. It is not an error.
  - Hard marks on dissolved edges go.
- **Warnings.** A non-planar result is a warning, as `define_mesh`'s is.
- **Refused:**
  - fewer than two faces;
  - faces that are not connected;
  - mixed winding across a shared edge;
  - a result that is not one face, such as two faces touching at a vertex only.
- It reports `Deleted` (T-64).

**Core: `Bridge`** (its own file). `bridge(loops: [[v, ...], [v, ...]])`.

- **The loops.** Each loop is a closed cycle of vertex ids whose every edge is open (one face). Both loops have the same count.
- **The pairing.** Pairs are chosen by the least total length over every rotation of the second loop and both of its directions, so the order the agent lists them in does not matter. A tie goes to the first rotation found, in a stated order.
- **The faces.** One quad per pair of consecutive pairs, appended in the first loop's order. Each quad is wound so that its edge on each loop runs opposite to the existing face there, which closes the surface.
- **What carries.** New faces take the colour and groups of the face on the first loop's edge they come from, as `extrude`'s walls do. No vertex is created, so no weights are made.
- **Refused:**
  - a loop that is not a cycle of open edges;
  - unequal counts;
  - a vertex in both loops;
  - loops whose neighbouring faces wind such that no quad can agree with both.

**Server.** The tools `merge_faces` and `bridge`, each with its description and a worked example.

- `merge_faces`: on a cube that `loop_cut` has split, merging one side's pieces back. Until T-66 lands, the example's input is built with `define_mesh` directly.
- `bridge`: two unit squares 1 apart, given as open quads facing each other, bridged into an open-ended box.
- Each tool is one `undo` step.

**Tests.**

- `unit`, each with a break shown failing:
  - merge two quads, three in an L, and a ring of four round a square (which gives a holed face);
  - ids and orphans;
  - a colour mismatch warning;
  - each merge refusal;
  - bridge of two squares (pairing over rotations and reversal, winding, the result closed when they are the two holes of a slab's front and back);
  - bridge of two holes of T-63's plate front and back, giving a closed tube;
  - each bridge refusal.
- `regression`: both tools through the client, then `check`.

**Content.** `MergeBridgeStage.cs` in the Cuts scenario:

- a slab 2 x 2 x 0.2 defined with a square hole in its front face and a matching hole in its back face, the inner faces left out, bridged. From `[0.6, 0.35, 1]` shaded, and orthographic `[0, 0, 1]` showing the background through it.
- A plate of four quads merged into one, in the wireframe from `[0.6, 0.35, 1]`.

Ray-test pixel checks, each shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):**
  - the lowest id survives a merge;
  - vertices left with no face are removed and listed in `deleted`;
  - a non-planar merge warns;
  - bridge takes two vertex loops of equal count and pairs them by least total length.
- Runs beside T-65. Both add to `Program.cs`'s list and `CutsStages.cs`, so keep both sides. This task does not touch `MeshToolSupport`'s report body (T-65 adds `split` there) or `FaceSplit`. It uses `Deleted` as T-64 wrote it down.
- **Open rules: stop and ask.**
  - Bridging loops of unequal count.
  - Dissolving collinear outline vertices.
  - A merged colour other than the survivor's.
  - A bridge with twist or segments.
- Every existing frame stays byte-identical by hash, listed.
- Opus for the pairing and the winding rules. If one agent fills on merge, hand off before bridge with As built written.
- Evidence goes to the milestone worktree's `artifacts/review/T-68/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.
