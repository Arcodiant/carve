---
description: >-
  Patina.Graphs evaluates in square tiles, each node naming the input rectangle it needs, under a capped tile cache, with every existing output byte-identical
---
# T-133 — Tiled texture graph evaluation

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, patina-regression, scenario-patina, scenario-planks

## Goal

`Patina.Graphs` evaluates an output in square tiles rather than full-width row bands. Each node says which rectangle
of each input it needs for an output rectangle, so a node that reads far from the texel it writes (a rotation, a
scale, a warp) costs a few input tiles, not the whole image. Every graph that evaluates today gives the same bytes,
and the memory bound holds at 8192.

## Scope

- **Research first** (a read-only Opus agent, its findings written into this file's Notes before code): the region-of-
  interest model (how VIPS, Halide or Nuke's row/tile engines name a node's input region), the tile size, the cache and
  its cap, and how the band evaluator's guarantees carry over: the memory bound, determinism whatever the tile size,
  the logical-row wrap. It proposes the node API below in detail and estimates the time at 8192 for the planks.
- **The node API** (`Nodes/NodeType.cs`, `Evaluation/`), replacing `Radius` and the row-band `NodeEvaluation`:
  - a node is asked for an output rectangle (x0, y0, width, height in logical texels, which may run past the image and
    wrap, as rows do now) and says, per input port, the rectangle it needs: the same for a pointwise node, widened for
    a neighbourhood node, a bounding box for a transform;
  - it fills its output rectangle from input tiles that cover what it asked for;
  - `Band` gives way to a rectangle-shaped image piece with the same channel layout (one or three `float`s, colours held
    sRGB-encoded, a normal's components raw) and the same wrapping reads;
  - a node can read an input texel anywhere inside what it asked for, with the coordinates wrapping, so a transform
    or warp written in T-138 and T-135 needs no help from the evaluator.
- **The evaluator** works the rectangles back through the graph from each output tile, evaluates inputs first, and
  keeps each node's tiles in a cache with a memory cap. Neighbouring output tiles reuse what they share. Single-
  threaded, in a fixed order, as now.
- **The seven node types are ported** (`colour`, `output`, `noise`, `gradient`, `levels`, `blend`,
  `height_to_normal`), their meaning unchanged.
- **Byte identity, held by tests:**
  - every committed `samples/graphs/*/*.png` is equal to its graph, as `PlanksGraphTests` holds now;
  - an output's bytes do not depend on the tile size or the cache cap: a test evaluates the planks, and a test graph
    with every node type, at tile sizes 16, 64 and 256 and two caps, and requires equal bytes;
  - `FirstNodesTests` and the determinism tests pass unchanged, apart from their start lines where they call the
    evaluator.
- **Memory and time:**
  - `ExportBoundedMemoryTests` (T-115: the planks at 8192, peak working set 2 GB or less) passes;
  - a new unit test evaluates a graph whose node asks for a far-away rectangle (a test node in `TestNodes.cs` reading a
    rotated rectangle) at 2048 and holds the most bytes the cache had at once under the cap;
  - `EvaluationRun` (or its successor) reports the tile size, the most tiles held, and the peak bytes, as `LastRun` does
    now;
  - measure the planks at 8192 (all four outputs, Debug, on the same machine before and after) and write both times here.
- `export`'s and `swatch`'s descriptions are corrected where they speak of bands. scenario-patina's and scenario-planks'
  frames stay byte-identical to `main`'s (by hash, from runs on one runner).
- CLAUDE.md's Layout row for `src/Patina.Graphs`, and its architecture rule ("deterministic and banded"), say tiles,
  not bands.

## Notes

- **Decided at planning (2026-11-19, by the person):** tiled region-of-interest evaluation, built first, so `transform`
  (T-138) can rotate and scale freely and warp (T-135) can read far from a texel within the memory bound. The person
  proposed tiles (a rotated tile needs at worst four source tiles); reducing the graph to a per-texel function was
  weighed and left as a possible later optimisation, since a neighbourhood node over another multiplies the work.
- Design-heavy, and it decides the API every other M-28 task and M-29's mesh nodes are written against: Opus, a research
  agent then the implementation; plan for a hand-off.
- **Write down for the tasks that depend on it** (in an As built section): the node API, with a worked pointwise node,
  a widened one and a transformed one; how a node reads a wrapped texel; the tile size and the cap, and what a node must
  not assume about the order tiles arrive in.
- Every input texel must be exactly the value the node would read at full size, so an output never depends on how it
  was tiled. A design in which it would (resampling at tile edges, a cache that evicts a tile half-used) is a stop.
- `Patina.Graphs` stays engine-free and deterministic: no `System.Random`, no clock, no `Math.Pow`; evaluation is kept
  apart from the image types by a namespace boundary (D-7).
- **Stop and ask:** a parallel evaluator (T-130's Notes: a change to determinism); a planks time at 8192 more than 10%
  slower than before; any committed output or frame that changes.
- **T-130** (faster evaluation at 8192, off-milestone) touches the same code. It is held until this task lands, and is
  then re-scoped against the tiled evaluator.
- Alone in wave 1: every node task in M-28 depends on this one.
