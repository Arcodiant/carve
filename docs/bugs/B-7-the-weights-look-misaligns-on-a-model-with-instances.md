---
description: >-
  The weights look misaligns on a model with instances
---
# B-7 — The weights look misaligns on a model with instances

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

`render` in the weights look of a whole model whose part tree has instances (or placed parts) draws the heat out of step with the geometry. Found by T-94 (M-9) on 2026-11-18. No M-9 sample is both rigged and instanced, so no committed frame shows it. Filed off-milestone by the person's ruling.

## Cause

`WeightHeat.Of` builds its per-vertex heat from `Model.Meshes`, one entry per mesh, while the geometry drawn comes from `Model.Combined`, which has one occurrence per drawn part: each instance is a copy of its source, placed at its world transform (T-91). The heat and the drawn vertices then disagree in count and order. T-94's subtree path (`render part=`) builds each occurrence's heat itself; the whole-model path does not.

## Fix

## Verified by
