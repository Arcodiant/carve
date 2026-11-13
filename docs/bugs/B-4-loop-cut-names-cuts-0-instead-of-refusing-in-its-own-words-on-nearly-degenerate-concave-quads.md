---
description: >-
  loop_cut names cuts[0] instead of refusing in its own words on nearly degenerate concave quads; found by RV-34
---
# B-4 — loop_cut names cuts[0] instead of refusing in its own words on nearly degenerate concave quads

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

On a concave quad, a `loop_cut` whose cut really crosses a side edge near the reflex arm's corner, or anywhere along a thin arm, is refused by `FaceSplit` as `cuts[0]: the segment 4-5 runs outside faces[0]`. That names vertex ids the agent's mesh does not have. The person's ruling of 2026-11-13 (RV-32) is that `loop_cut` refuses such a cut in its own facts-only words (`crosses its edge [1, 2]`), naming the fraction, the face and the edge, and never `cuts[k]`. No cut is ever made wrongly, and nothing is skipped silently; only the refusal's wording is wrong.

Failing inputs (RV-34's probe, `%TEMP%\rv34\probe`, Core at 06e0779; each was refused in `loop_cut`'s words at 30ae748), axis-aligned at z = 0, entry edge (0, 1):

- `(0,0) (4,0) (1,1) (0,4)` at 0.9996 and 0.9997, and in all of 300 random rotations at 0.9996;
- `(0,0) (10,0) (1,0.01) (0,10)`: 258 of 1999 fractions, e.g. 0.9;
- `(0,0) (10,0) (1,0.002) (0,10)`: 1276 of 1999, including 0.5;
- `(0,0) (10,0) (1,0.5) (0,10)`: 0.9975 to 0.9997.

## Cause

`LoopCut.cs:158` (from 38a3d87, RV-33's fix) skips the crossing test for any edge that either end of the cut lies within the tolerance of, not only the entry and exit edges. On a concave quad, an end of the cut can be within the tolerance of the side edge the cut really crosses, so that crossing is never tested.

## Fix

Suggested by RV-34: skip only the entry and exit edges, by vertex id, as `FaceSplit.Pieces` does, and keep the tolerance for those two. Test on the darts above, cut near the reflex arm, and keep RV-33's rotated rectangles and subdivided cubes passing. Also correct `LoopCut`'s remarks (`:25-27`) and T-66's As built, which describe the intended behaviour. Filed rather than fixed in M-5, by the person's ruling of 2026-11-13.

## Verified by
