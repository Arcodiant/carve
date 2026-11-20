---
description: >-
  A wide neighbourhood node runs as a cached row pass and a column pass, so blur and distance at 8192 meet 10 s within the memory cap, and a free rotation stops recomputing its source
---
# T-155 — Separable passes and rotated-read locality

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-137, T-138

**Verify:** patina-unit, patina-regression

## Goal

`blur` and `distance` cost about linearly in their radius, not with its square, and stay under the tile cache's cap at
8192. `transform` at a free rotation no longer recomputes its source several times over. Every output stays
byte-identical, so the speed checks T-137 and T-138 moved here can be met.

## Scope

- **Separable passes** (`Evaluation/`, `Nodes/NodeType.cs`, extending T-133's and T-153's API): a node can declare that
  it runs as two passes, a row pass widened only in x and a column pass, widened only in y, over the row pass's cached
  tiles. The intermediate is a tile kind of its own in the cache, evicted and recomputed like any other, and a
  recomputed tile has the same bytes. Nodes that do not opt in are unchanged. Whether the passes are internal to the
  node or expanded into hidden nodes at validation is this task's design call; write down why.
- **`blur`** (T-137) and **`distance`** (T-138) opt in, with outputs bit-identical to what they give now at every tile
  size and cap (their tests and the committed PNGs hold it). `blur`'s fixed-point running sums and `distance`'s exact
  Euclidean transform carry over as they are.
- **Pinned windows under the cap**: T-138 measured `distance` at range 0.1 and 8192 pinning 84 MiB against the 64 MiB
  cap. After this task, the most held stays at or under the cap for blur and distance at their largest radius and range.
- **Rotated reads** (the person's ruling, 2026-11-20: fix the recompute, rather than accept it or bound the overhead):
  T-138 measured a 30-degree turn of a colour `fbm` at 8192 at 46.7 s and 39,478 tiles, against 16.8 s and 16,384 for
  the source alone, so the source was made about 2.4 times. Find the cause (tile order, the cache's policy, the
  bounding box of a rotated tile) and fix it in the evaluator, so the turn makes its source close to once. State the
  ratio reached in a test.
- **The speed checks**, moved here from T-137 and T-138 (Release, on the machine T-135 and T-153 measured on; see their
  As built for the harness):
  - `blur` `smooth` at radius 0.05 on a colour input, at 2048 and 8192: 10 s or less at 8192.
  - `distance` at range 0.1 at 8192: 10 s or less.
  - the 30-degree turn of a colour `fbm` at 8192: write the time and the ratio of tiles made to the source's alone.
  - the cobblestones and the planks at 8192: no more than 10% slower than T-153's figures.
  - Write every figure, before and after, here.
- A `regression` test for each of blur and distance at 8192 with the peak working set at 2 GB or less, beside T-138's
  `TransformDistanceBoundedMemoryTests`. T-138 lands its distance memory test at 2048 (the person, 2026-11-20: at
  8192 it took about an hour in Debug); this task raises it to 8192 and asserts the 10 s there.
- **Transform's strong minify under the memory bound** (the person, 2026-11-20): T-138 measured `transform` at tile 16
  and scale 0.25 at 8192 with a working set of 4,348 MB, over the 2 GB bound. Bring it under 2 GB, and add a
  `regression` test that holds it there at 8192. Write the figure before and after.

## Notes

- **Decided (2026-11-20, by the person):** T-137 measured `blur` smooth at radius 0.05 at 8192 at about an hour, and
  T-138 measured `distance` at range 0.1 at 474 s, pinning past the cap. Each 128-texel output tile read a (128 + 2r)²
  window. The person chose a separable two-pass evaluation in M-28, over accepting the cost or lowering the bounds.
  T-137 and T-138 land first with their correctness and their samples; their 10 s checks are met here.
- Design-heavy: it changes the node API that T-140 and M-29's mesh nodes build on. Opus; a research step first is the
  agent's call.
- **Stop and ask:** any committed output that changes; a parallel evaluator; a speed check above that cannot be met.
- T-142's library stage and T-143's acceptance evaluate the samples at large sizes, so T-142 waits on this task.
