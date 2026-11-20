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

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`), one agent.

**The API** (`Nodes/NodeType.cs`, `Evaluation/GraphEvaluator.cs`; for T-137, T-138, T-140 and M-29's mesh nodes):

```csharp
// The inputs whose values decide port's rectangles. Default empty: the static InputRect, as before.
public virtual IReadOnlyList<string> DependsOn(string port, NodeParameters parameters) => [];
// Asked, for a port with DependsOn, each time the node's tile is made: the rectangles (one or a few) of port it reads.
public virtual IReadOnlyList<TexelRect> InputRects(string port, InputRequest request) => [InputRect(...)];
// InputRequest: NodeId, Size, Seed, Parameters, Output (the tile about to be filled), and
// Input(dependency): TileRead? holding that input's InputRect; naming an input not in DependsOn throws.
```

- The evaluator works back the static inputs as before. A dependent input is left out of that pass. When the node's
  tile is about to be made, its other inputs exist already. It calls `InputRects` and makes those rectangles' tiles
  (and what they need) one depth deeper, by the same plan-pin-make pass. They stay pinned with the rest until the
  output tile is handed over. A node naming itself, a port it lacks, or a port that is itself dependent in `DependsOn`
  throws `InvalidOperationException` when evaluated. No chains, so the depth is at most one per dependent node.
- `TileRead` takes several rectangles (`TileRead(kind, size, tileSize, parts)`, `Parts`). A texel is readable when
  it lies, wrapped, in any of them; a read outside all throws as before. `Row` must lie in one tile of one part.
- Worked example, `TestNodes.PairNode` (`pair`): where `gate` is above 0.5 it reads its own texel of `in`, else the
  texel half the image to the right. So it asks for one rectangle or two:
  ```csharp
  public override IReadOnlyList<string> DependsOn(string port, NodeParameters p) => port == "in" ? ["gate"] : [];
  public override IReadOnlyList<TexelRect> InputRects(string port, InputRequest request)
  {
      var o = request.Output;
      if (port != "in") return [o];
      var gate = request.Input("gate")!;              // holds gate's InputRect: the tile itself
      for (var y = o.Y; y < o.Bottom; y++)
          for (var x = o.X; x < o.Right; x++)
              if (gate.Grey(x, y) <= 0.5f) return [o, new TexelRect(o.X + request.Size / 2, o.Y, o.Width, o.Height)];
      return [o];
  }
  // Evaluate reads in.Grey(x, y) or in.Grey(x + e.Size / 2, y), exactly where InputRects said.
  ```
  Work the rectangles with the same function `Evaluate` uses (warp's `Source`), so the rectangle is what is read to
  the last bit. A dropped tile is asked again and must get the same answer.
- **`warp`** depends on `by` for `in`. It asks for one rectangle: the bounding box, in unwrapped coordinates, of the
  2 x 2 bilinear footprint of every texel of the tile, from `Source(x, y, …)`, which `Evaluate` also uses. Offsets
  are at most a quarter of the image, so the box never needs to span the image. A box past an edge is wrapped by the
  read, so it never needed a second rectangle. `InputRect("in")` stays as the bound (the whole amount and 2).
  `Evaluate` and `InputRects` read `by` a row at a time (`TileRead.Row`), not per texel; the values are the same.
  `NodeCatalogue`'s warp line had no "slow" warning, so it is unchanged.

**Tests** (`DependentInputTests.cs`, all `unit`; the nodes are in `TestNodes.Dependent`, so other type lists are
unchanged):
- `pair` is checked by hand at tile sizes 16, 64 and 256, under caps 0 and the default (two rectangles every tile).
- `pair_short` under-declares its rectangle and throws "is not in the rectangle asked for".
- A self-dependent port is refused.
- `warp` equals `warp_static` byte for byte at amounts 0.05 and 0.25, at tile sizes 16, 64 and 256 under caps 0 and
  the default. `warp_static` is the built-in warp without the opt-in.
- Warp at 0.25, 1024, tiles of 128 under a 1 MiB cap makes at most **2 times** the minimum of 192 tiles. It made 314,
  1.64 times. Measured the same way, the static warp makes 2,481, 12.9 times.
- Every committed `samples/graphs/*/*.png` is unchanged: all 201 Patina.Graphs tests pass, the cobblestones' and the
  planks' PNG equality among them.

**Time at 8192** (Release, AMD Ryzen 7 5800X3D, Windows 10 Pro, tiles of 128, 64 MiB cap, peak held 64 MiB in each).
This is T-135's machine and build configuration. The harness is a console outside the repo
(`%TEMP%\t153-measure`), calling `GraphEvaluator.Evaluate` with no packing. The warp graph reproduces T-135's tile
counts exactly (12,800 and 3,648).

| | T-135 | before, re-run here | after |
|---|---|---|---|
| warp 0.05 | 27.3 s, 12,800 tiles | 24.0, 35.2, 26.5, 32.7 s | 26.3, 30.3, 22.5 s, 12,352 tiles |
| warp 0.25 | did not finish, about 55 min (est.) | not run | 26.9 s, 12,352 tiles |
| warp 0.25 at 4096 | 8.4 s, 3,648 | 7.3 s | 8.1 s, 3,104 (minimum 3,072) |
| cobblestones base_colour | 134.4 s, 123,456 | 140.7 s | 75.5 s, 95,589 |
| normal | 202.2 s, 161,672 | 246.7 s | 129.2 s, 133,783 |
| roughness | 156.4 s, 90,688 | 123.8 s | 57.4 s, 62,821 |
| occlusion | 142.6 s, 78,400 | 131.5 s | 54.9 s, 50,533 |

- Warp at 0.25 now costs what 0.05 does.
- The cobblestones are 36 to 63 % faster than T-135's figures.
- Back-to-back runs of one build vary by about 30 % on this machine. The "before" re-runs overlapped this agent's
  builds; the last "after" set ran with nothing else of mine running.
- The cobblestones' figures are from the final code. Of the warp 0.05 figures, the first "after" (33.5 s) came
  before the row reads and is left out.

**Not checked.** No frames: nothing visible changed, and the PNG equality tests hold every committed output. I did
not run `ExportBoundedMemoryTests` by hand; `patina-regression` in verify runs it.
