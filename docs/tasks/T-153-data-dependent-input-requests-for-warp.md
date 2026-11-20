---
description: >-
  A node can ask for an input's rectangle after reading another input, so warp at a large amount reads the few tiles its offsets reach rather than a box widened by the whole amount
---
# T-153 — Data-dependent input requests for warp

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-135

**Verify:** patina-unit, patina-regression

## Goal

A node can name the rectangle it needs of one input after it has read another, so `warp` asks for the source texels
its `by` offsets actually reach, not the output tile widened by the whole `amount` on every side. At 8192 and amount
0.25, a warp costs about what amount 0.05 costs now, and every output stays byte-identical.

## Scope

- **The API** (`Nodes/NodeType.cs`, `Evaluation/`), extending T-133's `InputRect`: a node may declare that one input's
  rectangle depends on another input's values. The evaluator evaluates the inputs it depends on first (with the usual
  static rectangle), hands the node a read of them, and the node returns the rectangle, or a small set of rectangles,
  it needs of the dependent input. Nodes that do not opt in are unchanged.
- **`warp`** opts in: its `in` rectangle is the bounding box of the output tile's texels moved by their actual offsets
  from `by` (plus the bilinear footprint), wrapping. If the moved texels wrap across the image, it may return more than
  one rectangle rather than one box spanning the image.
- **The guarantees carry over** from T-133's `### As built`: determinism whatever the tile size and cap; an evicted tile
  recomputes to the same bytes; pinned tiles are never evicted; reads outside what was asked still throw; the memory
  bound at 8192.
- **Tests:**
  - every committed `samples/graphs/*/*.png` still equals its graph (the cobblestones use warp);
  - warp's outputs are equal across tile sizes 16, 64 and 256 and two caps, at amounts 0.05 and 0.25;
  - a unit test holds that warp at amount 0.25 evaluates no more than a small multiple of the minimum tiles (state the
    multiple, measured);
  - a test node that under-declares its dependent rectangle throws, as `peek` does now.
- **Measure** at 8192, in the same build and on the same machine as T-135's figures (Release, see T-135's As built):
  warp at amount 0.05 and 0.25, and the cobblestones' four outputs. Write the before and after figures here.
- Warp's description in `NodeCatalogue` loses any "slow at a large amount" warning that T-135 gave it, if the
  measurement removes the reason.

## Notes

- **Decided (2026-11-20, by the person):** T-135 measured warp at amount 0.25 and 8192 at about 55 minutes, against
  27 s at 0.05. The cause is that a static `InputRect` must cover the largest possible offset, a 4,224-texel square
  (about 1,089 source tiles) per 128-texel output tile, more than the 1,024 grey tiles the 64 MiB cache holds. The
  warp field is smooth, so a tile really reads about 1 to 4 source tiles. The person chose to build the
  data-dependent request in M-28, over filing it off-milestone, capping the amount or raising the cache cap.
- Design-heavy: it changes T-133's node API, which T-137, T-138 and T-140 and M-29's mesh nodes build on. Opus.
- `transform` (T-138) is not asked to opt in: a rotation's box is exact, and a strong minify really reads a wide area.
  If T-138 finds otherwise, that is its call to raise.
- **Stop and ask:** any committed output that changes; a cobblestones time at 8192 more than 10% slower than T-135's;
  a parallel evaluator.
- Runs beside T-134, which owns the output roles and `TexGraphFile`. Lines added to the evaluator by both are kept on a
  rebase.
