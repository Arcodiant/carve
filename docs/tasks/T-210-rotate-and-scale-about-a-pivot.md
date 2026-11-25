---
description: >-
  rotate turns vertices, edges or faces about an axis by an angle in degrees and scale scales them per axis, both about a pivot (default the selection's bounds centre), keeping every id as move does
---
# T-210 — Rotate and scale about a pivot

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit

## Goal

An agent turns and scales part of a mesh about a pivot, picking vertices, edges and faces as `move` does, with every
id kept.

## Scope

- **Core, `MeshEdits.Rotate` and `MeshEdits.Scale`** (in `src/Carve.Core/MeshEdits.cs`, beside `Move`). They take the
  same union of vertices, edges and faces as `Move`, with each vertex counted once, and write positions only, through
  `mesh.Edit(movedVertices:)`. That keeps ids, face loops, UVs, seams, groups and weights exactly as `move` keeps them.
  - **The pivot** is a point, or by default the centre of the axis-aligned bounds of the vertices being moved. It is
    in the mesh's own space, which is the part's space in a part tree.
  - **rotate:** `axis` is `"x" | "y" | "z"` or a non-zero `[x, y, z]`, normalised. `angle` is in degrees, by the right
    hand: a positive angle turns counter-clockwise seen from the axis's tip looking at the pivot. Work it in `double`.
    **A multiple of 90° about x, y or z is exact**: sin and cos come from a table, so 90° about Z puts (1, 0, 0) on
    (0, 1, 0) bit for bit. The wheel (T-215) depends on that.
  - **scale:** `by` is one number, or `[sx, sy, sz]`, each > 0. A factor ≤ 0 is refused, and the message names
    `mirror` for flipping (decided at planning).
- **Refusals** follow `Move`'s rules, every problem named by element, the mesh unchanged: nothing listed, an unknown
  id, a pair that is not an edge, a zero or non-finite axis, an angle that is not finite or is a multiple of 360, every
  factor 1 ("nothing would change"), a factor ≤ 0, a non-finite pivot, and a result that folds a face (the bow-tie
  check `move` already has).
- **Server, `src/Carve.Server/RotateTool.cs` and `ScaleTool.cs`** (new), tools `rotate` and `scale`. They have move's
  arguments, with `by` replaced as above, plus `pivot` and `mesh`. They return move's report, `moved` included, and
  carry `PartSpaceText`. Register both in `CarveServer.AddTools` after `move`.
- **Owns `MeshToolSupport`'s new argument readers**: `Axis` (a letter or a vector) and the optional `Pivot`. T-212 uses
  them.
- **Descriptions**: each at most 3,686 characters, with one worked example on define_mesh's unit cube, run as written
  by a test. For example, the top face turned 45° about y with the default pivot, and the cube scaled by
  `[2, 1, 1]` about `[0, 0, 0]`.
- **Tests** (`tests/Carve.Core.Tests/RotateScaleTests.cs`, `tests/Carve.Server.Tests/RotateScaleToolTests.cs`):
  - by hand on the cube, and on a non-axis-aligned, concave input (an L-block turned about `[1, 1, 0]` by 30°; the
    M-5 lesson);
  - the default pivot, and quarter turns exact;
  - rotate by θ then −θ back within 1e-6; scale then 1/scale back within 1e-6;
  - ids and every layer unchanged, a morph-targeted mesh included;
  - the non-planar warning fold as move gives it;
  - every refusal, and the worked examples.
- **`README.md`**: both tools, with a worked call each.

## Notes

- **Decided at planning (2026-11-25, by the person):** axis and angle in degrees; scale > 0 only, a negative factor
  refused naming mirror. Recommended by me: the pivot defaults to the selection's bounds centre (not the origin).
- **Open, ask before dispatch:** a morph target's deltas. Recommended: they are kept as they are, as `move` keeps
  them, and not turned with the vertex. Turning them is the other answer. Do not land either without the answer.
- **Design-heavy:** the `axis` and `pivot` argument forms are a public API, which T-212 reuses and later tools will
  copy. Write them into `### As built`.
- Compare in float where the store holds float (B-1). Parse numbers culture-invariant.
- New tools: re-take the tools/list digests for the added entries only, and ask if an existing entry would change.
