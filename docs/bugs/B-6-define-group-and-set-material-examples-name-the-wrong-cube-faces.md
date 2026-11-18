---
description: >-
  define_group and set_material examples name the wrong cube faces
---
# B-6 — define_group and set_material examples name the wrong cube faces

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

Two worked examples on `define_mesh`'s unit cube name faces that are not the
ones their text describes, so an agent learning from them targets the wrong
faces:

- `define_group` (`src/Carve.Server/FaceStyleTools.cs`) names "the four sides"
  as `[0,1,2,3]`, which are the -Z, +Z, bottom (-Y) and top (+Y) faces. The four
  sides are `[0,1,4,5]`.
- `set_material` (`src/Carve.Server/MaterialTools.cs`) textures "its top and
  front, faces 3 and 2". Face 2 (`[0,1,5,4]`, all at y = 0) is the bottom; the
  front (+Z) is face 1. Its list of the faces left without UVs (0, 1, 4 and 5)
  is wrong for the same reason.

## Cause

The examples' face ids were written without working out the cube's faces from
`CubeExample`'s coordinates.

## Fix

Correct both examples (and the expected `check` warning in `set_material`'s),
and any test that parses them. Found by RV-57; the README (T-119) already uses
the correct ids.

## Verified by
