---
description: >-
  Patina.Graphs evaluates in square tiles, each node naming the input rectangle it needs, under a capped tile cache, with every existing output byte-identical
---
# T-133 — Tiled texture graph evaluation

**Status:** In Progress

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

### Research (T-133)

Written 2026-11-19 by the research agent (Claude Opus 5.5, `claude-opus-5-5`), read-only apart from this file. The
region-of-interest survey is from the agent's own knowledge of the engines, not fetched.

**How others name an input region.** All three ask each operation, for an output rectangle, which input rectangle it
needs, and evaluate on demand:
- VIPS: an operation's `generate(out_region)` computes a `VipsRect need` (the output rect for a pointwise op, grown by
  the mask for a convolution, the inverse-mapped bounding box for `affine`) and calls `vips_region_prepare(in, &need)`;
  tiles (typically 128 x 128, or thin strips) are cached per operation under an LRU cap.
- Nuke: `Iop::_request(x, y, r, t, channels, count)` passes a bounding box up the tree before `engine()` fills rows; a
  transform requests its inverse-mapped box, a blur its box grown by the radius.
- Halide: bounds inference by interval arithmetic gives each stage's required region for an output tile; `compute_at`
  and `store_at` decide what is held. Darktable's `modify_roi_in(roi_out -> roi_in)` is the same idea.

All are conservative: one bounding box per input, so a minifying transform asks for a large region. That is accepted
here too (see Risks).

**Decisions proposed.**
1. *Canonical tiles.* The evaluator only ever asks a node to fill a canonical tile, `[tx*T, tx*T+T) x [ty*T, ty*T+T)`,
   inside the image, `T = min(tileSize, size)`, both powers of two so tiles divide the image exactly. Writes never
   wrap; only reads do. This is narrower than the Scope's "an output rectangle which may run past the image": every
   texel is computed under its image coordinates, so a node that does not wrap its own `x` (`noise` and `gradient` use
   `x` unwrapped today) still gives the full-size value. Input rectangles may run past the image anywhere.
2. *Tile size* `GraphEvaluator.DefaultTileSize = 128` (a grey tile 64 KB, a colour one 192 KB; 4096 tiles at 8192, one
   tile at sizes up to 128). 64 would keep colour tiles off the LOH; 256 cuts the overhead of a big-radius neighbourhood
   (T-137's blur at radius 0.05 of 8192 = 410 texels reads `(T + 2r) / T` times its rows in a first pass: 7.4x at 128,
   13.8x at 64, 4.2x at 256). Bytes do not depend on it, so it can move later without a byte changing.
3. *Cache* keyed `(node index, tx, ty)`, holding every output port's tile of that node (a node fills all its ports in
   one call). Cap `GraphEvaluator.DefaultCacheBytes = 64 MiB`, per `Evaluate` call (dropped at the end, as bands are
   now; sharing tiles across the four outputs is a later T-130 optimisation). LRU by a step counter, no clock. The cap
   bounds *unpinned* tiles: held bytes stay at most `max(cap, pinned)`. 64 MiB and not more because the planks at 8192
   already peak at about 1.57 GB of the 2 GB bound (measured below); and 64 MiB gives full reuse in the planks (the
   normal chain re-reads about two tile rows of `slope` and `bevel`, 2 x 64 x 2 x 64 KB = 16 MB, re-touched within 64
   steps with about 37 MB of other tiles inserted in between).
4. *Pinning, so no tile is evicted half-used.* For each output tile, every tile that step needs is pinned before any is
   computed (those cached at the step's start included), and unpinned after the output tile is handed out. Eviction (on
   every insertion: drop LRU unpinned tiles until held <= cap) only drops unpinned tiles. A dropped tile asked for again
   is recomputed and comes out the same, since a node's tile is a pure function of its inputs' full-size values.
5. *Determinism whatever the tile size.* Holds because (a) a canonical tile's texels are computed under their image
   coordinates from inputs holding exactly the full-size values; (b) every read wraps by `size`, as rows do now;
   (c) evaluation is single-threaded in a fixed order (output tiles in raster order, top row first, left to right; nodes
   inputs first). Eviction changes how often a tile is computed, never its value. The run's stats are deterministic too.
6. *Memory bound.* Pinned in one step = each node's needs cone for one output tile: one tile a node in a pointwise
   chain, at most 3 x 3 for a radius below T, at most 3 x 3 for a rotated tile's bounding box (a turned T square's box
   is up to 1.41 T across, touching 2 or 3 tiles an axis: four is typical, nine the worst). Plus the cap, plus the
   caller's RGBA image. At 8192 the planks hold about 64 MB + a few MB, where bands hold about 30 MB now: the same order.

**Types and signatures** (image types in `Patina.Graphs.Imaging`, which references nothing in `Evaluation`; the cache
and the evaluator in `Patina.Graphs.Evaluation`: D-7's boundary):

```csharp
// Imaging/TexelRect.cs: logical texels; X, Y any int (they wrap when read), Width, Height >= 1.
public readonly record struct TexelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;   public int Bottom => Y + Height;   // exclusive
    public TexelRect Inflate(int r) => new(X - r, Y - r, Width + 2 * r, Height + 2 * r);
    public TexelRect Inflate(int rx, int ry);
    public static TexelRect Bounding(float minX, float minY, float maxX, float maxY, int margin); // floor, ceil, margin
}

// Imaging/Tile.cs: replaces Band. One canonical tile of one port; coordinates are image coordinates inside Rect,
// anything else throws.
public sealed class Tile
{
    public Tile(PortKind kind, TexelRect rect);
    public PortKind Kind { get; }  public TexelRect Rect { get; }  public int Channels { get; }
    public float[] Data { get; }   public long Bytes { get; }           // row-major, Channels floats a texel
    public Span<float> Row(int y);                                      // Rect.Width * Channels floats from Rect.X
    public float Grey(int x, int y);  public (float R, float G, float B) Colour(int x, int y); // grey spreads to grey
    public void SetGrey(int x, int y, float v);  public void SetColour(int x, int y, float r, float g, float b);
    public static int Wrap(int v, int n);                               // moved from Band, same body
}

// Imaging/TileRead.cs: what a node reads an input through, the tiles covering the rectangle it asked for.
public sealed class TileRead
{
    public TileRead(PortKind kind, int size, int tileSize, TexelRect asked, Tile[] tiles); // tiles row-major over the cover
    public PortKind Kind { get; }  public TexelRect Asked { get; }  public int Size { get; }
    public float Grey(int x, int y);                         // wraps; throws when (x, y) wraps outside Asked
    public (float R, float G, float B) Colour(int x, int y);  // wraps; a grey input spreads to grey
    public ReadOnlySpan<float> Row(int y, int x, int count); // texels x..x+count-1 of row y; must lie in one tile
}
```

`TileRead`'s read rule: `ax = Wrap(x - Asked.X, size)` must be `< Asked.Width`, else it throws an
`ArgumentOutOfRangeException` naming the coordinates and the rectangle asked for (the evaluator catches nothing, so an
under-declaring node fails its test instead of giving a quiet wrong value); the texel is at image column
`Wrap(Asked.X + ax, size)`, in cover column `floor((Asked.X + ax) / T) - floor(Asked.X / T)`; the same for y. An axis
asked `size` or more across is first clamped to `[0, size)`, so any read on it is allowed. When `Asked` lies inside one
tile (every pointwise read) it keeps that tile directly, so a Debug-build read costs what `Band.Grey` costs now.

`Nodes/NodeType.cs`: `Radius` goes, replaced by

```csharp
/// <summary>The rectangle of input <paramref name="port"/> that filling <paramref name="output"/> reads; by default
/// <paramref name="output"/> itself (a pointwise node).</summary>
public virtual TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) => output;
```

`NodeEvaluation` (in `GraphEvaluator.cs`, as now): `NodeId`, `Size`, `Seed`, `Parameters`, `TexelRect Rect` (the
canonical tile to fill, replacing `Y0` and `Rows`), `TileRead? Input(string port)`, `Tile Output(string port = "out")`.

`GraphEvaluator`:

```csharp
public const int DefaultTileSize = 128;
public const long DefaultCacheBytes = 64L << 20;
public void Evaluate(OutputRole role, int size, Action<Tile> onTile, int? tileSize = null, long? cacheBytes = null);
public EvaluationRun? LastRun { get; private set; }
public sealed record EvaluationRun(int TileSize, int MostTiles, long PeakBytes, long CapBytes, int TilesEvaluated);
```

`onTile` gets the output node's source tile for each canonical tile. `OutputImage.Make` and `Rgba8(…, int? tileSize =
null, long? cacheBytes = null)` pack it row by row with `OutputPacking.PackRow(role, tile, y, rgba.AsSpan((y * size +
tile.Rect.X) * 4, tile.Rect.Width * 4))` (`PackRow` takes a `Tile`, `x` running over `Rect`). `BandRowsFor` and
`BandTexels` go. `ExportTool` (line ~135) packs the same way and checks for cancellation once a tile.

**The evaluator's algorithm** (one `Evaluate` call):
1. Check size and role as now; `T = min(tileSize ?? DefaultTileSize, size)`, a power of two (else throw); `n = size / T`.
   The needed nodes, inputs first, as now, numbered `0..k-1` to key the cache with ints.
2. For each output tile `(tx, ty)`, `ty` outer, `tx` inner:
   - *Needs, backwards:* `needs[source of the output node] = {(tx, ty)}`. For each node in reverse order, for each tile
     of its needs in insertion order, for each connected port: `r = InputRect(port, tileRect, parameters, size)`; clamp
     each axis `size` or more across to `[0, size)`; add every tile covering `r` (indices `floor(r.X / T)` to
     `floor((r.Right - 1) / T)`, each `Wrap(·, n)`, deduplicated) to `needs[source]`.
   - *Pin* every needed tile already cached, and touch it.
   - *Forwards:* for each node in order (not the output node), for each needed tile not cached: a `TileRead` per
     connected port over the source's pinned tiles, a new `Tile` per output port, `Evaluate`, insert pinned (evicting
     LRU unpinned tiles past the cap), `TilesEvaluated++`; `MostTiles` and `PeakBytes` taken after each insertion.
   - `onTile(the (inNode, inPort) tile at (tx, ty))`, then unpin the step's tiles.
3. `LastRun = new(T, mostTiles, peakBytes, cap, tilesEvaluated)`; drop the cache.

**Worked nodes** (for the As built section, against the API above):
- *Pointwise* (`levels`): no `InputRect`. `for (var y = e.Rect.Y; y < e.Rect.Bottom; y++) { var from = input.Row(y,
  e.Rect.X, e.Rect.Width); var to = output.Row(y); for (var i = 0; i < to.Length; i++) to[i] = Map(from[i], …); }`.
  `colour`, `noise` and `gradient` run `x` from `e.Rect.X` to `e.Rect.Right - 1` instead of `0` to `Size - 1`; `blend`
  the same with `Grey` and `Colour` reads; `TestNodes`' `hash` and `split` likewise.
- *Widened* (`height_to_normal`): `InputRect(…) => output.Inflate(1)`; the body is today's with the `x` loop over
  `e.Rect`; `h.Grey(x - 1, y)` at `x = 0` reads column `size - 1`, wrapping. `TestNodes.BlurNode`: `Inflate(R)`.
- *Transformed* (a new `TestNodes.RotateNode`, `rotate`: its input turned by `degrees` about the image centre and moved
  by half the image, nearest texel, `float` and `MathF.Sin`/`Cos` of a parameter worked once): `InputRect` maps the
  output rectangle's four corners through the inverse map (the same function `Evaluate` uses) and returns
  `TexelRect.Bounding(min, max, margin: 2)`; `Evaluate` maps each texel centre `(x + 0.5, y + 0.5)`, floors it and
  reads `input.Grey(sx, sy)`, wrapping. The margin covers `float` rounding between corners and centres; were it short
  the read would throw. Its truth does not need the evaluator: `out(x, y) = HashValue(Wrap(sx), Wrap(sy))`, worked
  directly in the test.
- *Reading a wrapped texel:* read at the logical coordinate meant (`x - 1`, an `sx` past the edge, a negative one);
  `TileRead` wraps it by `size`. Never wrap into a tile by hand, never index an input's `Data`.

**What a node must not assume** (for As built): the order tiles arrive in (raster today, not promised); that it is
called once a tile (an evicted tile is asked for again and must come out the same); any state between calls (a
`NodeType` is one shared instance); the tile's size (16 to 256 in tests, the whole image below the tile size; never
`Rect.Width == Size`); that an output tile starts zeroed (write every texel of every port); any texel outside the
rectangle it asked for (reading one throws). A node works texels from image coordinates and `e.Size`, never from the
position inside its tile.

**Commits, in order:**
1. `TexelRect`, `Tile` and `TileRead` in `Imaging/`, with unit tests (`TileReadTests.cs`): the cover of a rectangle
   past each edge and corner, wider than the image, negative; wrapped reads equal the tile's texel; a read outside
   throws; `Row` across two tiles throws. Nothing else uses them yet.
2. The switch, in one commit since the build breaks otherwise: `NodeType.InputRect` for `Radius`, `NodeEvaluation.Rect`,
   the evaluator and `TileCache` (internal, `Evaluation/TileCache.cs`), the seven nodes and the three test nodes ported,
   `OutputPacking.PackRow`, `OutputImage`, `ExportTool`, `Band.cs` deleted; the tests' start lines moved to `tileSize`
   (`FirstNodesTests.Run`'s callback, `EvaluationTests.Rgba`, `PlanksGraphTests` line ~65). patina-unit must pass with
   every committed PNG and every hand-worked value unchanged. **Measure 8192 here**, before the new tests.
3. New tests: tile-size and cap invariance (the planks at 256, and a graph with every node type plus `blur`, at tile
   sizes 16, 64 and 256 and caps 0 and the default, all equal to tile = size); `EvaluationTests`' band tests rewritten
   as `ARadiusTwoNodeWrapsAtTileEdgesAsTheWholeImageDoes` (tile sizes 16, 32, 64) and
   `TilesAreBoundedAndTheCacheStaysUnderItsCap` (exact `EvaluationRun`s worked by hand for a flat graph and the
   blurred one); `AFarReadingNodeStaysUnderTheCap` (`RotateNode` at 2048, 30 degrees, cap 8 MB: `PeakBytes <=
   CapBytes`, bytes equal to the direct formula and equal at tile sizes 64 and 256); `AnUnderDeclaringNodeThrows` (a
   test node asking for its output rectangle but reading `x + 1`); with the default cap the planks evaluate each tile
   once (`TilesEvaluated` = tiles x nodes).
4. Docs: CLAUDE.md's `src/Patina.Graphs` and `tests/Patina.Graphs.Tests` Layout rows and the rule ("deterministic and
   tiled"); `export`'s description (line 39, "in bands"; its "about 2.5 minutes" is stale: 196 s here) and its comment
   (line 72, "between bands"); `swatch`'s description does not speak of bands (searched; `SwatchImages.LabelBand` is
   a label strip, unrelated); `ExportBoundedMemoryTests`' summary ("holds bands"); the As built section with both
   times. Then scenario-patina's and scenario-planks' frames by hash against `main`'s.

**Estimate at 8192.** The per-texel arithmetic and reads are unchanged in kind. Bookkeeping: 4096 tile positions x 17
node tiles over the four outputs, about 70 k node calls and their need sets: well under a second, even in Debug. Saved:
`slope` and `bevel` are no longer computed for 34 rows a 32-row band (about 6 % of two cheap nodes). Allocation: 192 KB
colour tiles land on the LOH as today's 3 MB colour bands do, the same bytes in all. Expected: the same time within
+-5 % (about 186 to 206 s against the 196.0 s median), so no stop expected. The risk is Debug's uninlined accessors: if `TileRead.Grey` and
`Colour` come out slower than `Band`'s, `blend` and `height_to_normal` (the per-texel readers) show it first; hence the
one-tile fast path, and the measurement right after commit 2.

**Risks for the tasks that build on it** (none a stop for this task; the person may want to know now):
- *Warp (T-135)* reads up to `amount` 0.25 of the image away. Its honest `InputRect` is the output widened by
  `ceil(amount * size) + 1`: at 8192, 34 x 34 tiles, about 220 MB pinned for one colour output tile, over the 64 MB
  cap, and the cache cannot hold the band reuse would need (about 400 MB), so the input is recomputed many times over.
  Correct, but slow at 8192 with a large amount. Remedies, not decided here: small amounts in the samples; a
  data-dependent request (warp reads `by` first, then asks for `in`'s true box, which turns the needs pass into a
  VIPS-style recursive pull); or a larger cap for such a step.
- *Transform (T-138)*: a minification (`tile` 16, `scale` 0.25) asks for a box of up to 16 x 16 tiles for one output
  tile, and touches the whole input in each output tile row; under a 64 MB cap a colour input at 8192 (768 MB) is
  recomputed row after row. The same remedies. Rotation and offset are cheap (2 or 3 tiles an axis).
- *Blur (T-137)* at a large radius pays `(T + 2r) / T` in its first pass; a larger tile helps and changes no byte.
- *Nested graphs (T-140)* expand at validation; the evaluator never sees them.

**How the before time was measured.** `dotnet build tests/Patina.Server.Tests -c Debug`, then three runs, one after
another, of `dotnet test tests/Patina.Server.Tests -c Debug --no-build --filter
"FullyQualifiedName~ExportBoundedMemoryTests"` (T-115's test: the real `patina` process opens the planks and exports
all four outputs at 8192, timed around the `export` call, PNG encoding included, with the process's peak working set),
reading the first line of `%TEMP%\patina-export-8192.txt` after each; then two more runs the same way. Machine: AMD
Ryzen 7 5800X3D, Windows 10 Pro. Take the after time the same way on the same machine.

**Before** (`main`'s band evaluator at `task/T-133`'s start, Debug, 8192, four outputs, five runs): 196.0 s, 287.2 s,
205.8 s, 182.7 s, 191.5 s; **median 196.0 s**; peak working set 1567 to 1571 MB in every run. The 287 s run is an
outlier (the agent was only editing this file at the time, so something else on the machine was busy). Even without
it the runs spread by +-6 %, so the after time must be five runs too, compared by median: one run cannot tell a 10 %
slowdown from noise. Runs 1 to 3 overlapped the agent's file reads and edits; runs 4 and 5 overlapped nothing.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`), following the research's plan.

**Rulings since the research (2026-11-19, by the person):**
- *Accepted:* the evaluator fills only canonical tiles inside the image; only a node's reads wrap, and the rectangles a
  node asks of its inputs may still run past the image. This narrows the Scope's "an output rectangle which may run
  past the image and wrap".
- The cost of a large warp or a strong minify at 8192 (heavy recomputation under one bounding box per input and the
  64 MiB cap) is left to T-135 and T-138 to measure and raise. T-133 adds no data-dependent request.

**What was built.** `Imaging/TexelRect.cs`, `Tile.cs`, `TileRead.cs` and `PortKind.cs` (moved out of the deleted
`Band.cs`); `Evaluation/TileCache.cs` (internal) and the tiled `GraphEvaluator`; `NodeType.InputRect` for `Radius`;
`OutputPacking.PackRow` over a `Tile` and `PackTile`; `OutputImage.Make`/`Rgba8(…, tileSize, cacheBytes)`;
`ExportTool` packs tile by tile and checks for cancellation once a tile. The research's commits 1 and 2 went in as one
(the types and the switch), since `PortKind` moved out of `Band.cs`; the new tests are a commit of their own. Every
committed `samples/graphs/*/*.png` is unchanged. Test start lines moved: `FirstNodesTests.Run`'s callback and its
`CheckSeam` line (`Band.Wrap` is `Tile.Wrap`), `EvaluationTests.Rgba` and its packing helper, `PlanksGraphTests`' tiled
test. The test nodes `rotate` and `peek` are in a registry of their own, `TestNodes.Reaching`, so the type list
`GraphFaultTests` holds is unchanged.

**The node API** (for every node task in M-28 and M-29's mesh nodes):

```csharp
public abstract class NodeType
{
    // The rectangle of input `port` that filling `output` reads, in logical texels; it may run past the image on any
    // side. Default: `output` itself (a pointwise node).
    public virtual TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) => output;
    // Fills every texel of e.Rect in every output port.
    public abstract void Evaluate(NodeEvaluation e);
}
// e.NodeId, e.Size, e.Seed, e.Parameters; e.Rect (the tile to fill, inside the image);
// e.Input(port): TileRead? (null for an optional input left unconnected); e.Output(port = "out"): Tile.
```

- *Pointwise* (`levels`): no `InputRect`.
  ```csharp
  for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
  {
      var from = input.Row(y, e.Rect.X, e.Rect.Width);   // a pointwise read always lies in one tile
      var to = output.Row(y);
      for (var i = 0; i < to.Length; i++) to[i] = Map(from[i], lo, hi, olo, ohi, gamma);
  }
  ```
  `colour`, `noise`, `gradient`, `blend`, `hash` and `split` loop `x` over `e.Rect.X` to `e.Rect.Right - 1` (an output
  row's index is `x - e.Rect.X`) and `y` over `e.Rect.Y` to `e.Rect.Bottom - 1`.
- *Widened* (`height_to_normal`, radius 1; `TestNodes.BlurNode`, radius 2):
  ```csharp
  public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) => output.Inflate(1);
  // in Evaluate, for each x, y of e.Rect:
  var n = Normal(h.Grey(x - 1, y), h.Grey(x + 1, y), h.Grey(x, y - 1), h.Grey(x, y + 1), e.Size, strength);
  output.SetColour(x, y, n.X, n.Y, n.Z);
  ```
- *Transformed* (`TestNodes.RotateNode`, `rotate`: its input turned by `degrees` about the centre and moved by half the
  image, nearest texel). `InputRect` maps the output rectangle's four corners through the same inverse map `Evaluate`
  uses and returns `TexelRect.Bounding(minX, minY, maxX, maxY, margin: 2)`; `Evaluate` maps each texel centre
  `(x + 0.5f, y + 0.5f)`, floors it and reads `input.Grey(sx, sy)`. The margin covers `float` rounding between corners
  and centres; were it short, the read would throw rather than give a wrong value.

**Reading a wrapped texel.** Read at the logical coordinate meant: `x - 1` at `x = 0`, an `sx` past the right edge, a
negative one. `TileRead` wraps it by the image's size and finds the tile. A texel is readable when, wrapped, it lies in
the rectangle asked for (`Wrap(x - Asked.X, size) < Asked.Width`, the same for y); any other read throws an
`ArgumentOutOfRangeException` naming the texel and the rectangle. An axis asked the image's size or more across is the
whole axis. Never wrap into a tile by hand and never index an input's `Data`; `TileRead.Row(y, x, count)` is for a run
inside one tile (it throws across two).

**Tile size and cap.** `GraphEvaluator.DefaultTileSize = 128` (a greyscale tile 64 KB, a colour one 192 KB; 4096 tiles
at 8192; one tile at sizes to 128) and `DefaultCacheBytes = 64 MiB`, per `Evaluate` call. Tiles a step needs are pinned;
past the cap, unpinned tiles are dropped least recently unpinned first. `EvaluationRun(TileSize, MostTiles, PeakBytes,
CapBytes, TilesEvaluated)` is in `LastRun`. Neither the tile size nor the cap changes a byte: tests hold it at tile
sizes 16, 64 and 256 under caps 0 and the default, for the planks and for a graph with every node type.

**What a node must not assume:** the order tiles arrive in (raster today, not promised); that it is called once a tile
(a dropped tile is asked for again and must come out the same); any state between calls (a `NodeType` is one shared
instance); the tile's size (16 to 256 in the tests, the whole image below the tile size: never `Rect.Width == Size`);
that an output tile starts zeroed (write every texel of every port); any texel outside the rectangle it asked for. A
node works texels from image coordinates and `e.Size`, never from its place inside the tile.

**Time at 8192** (the planks, all four outputs, Debug, `ExportBoundedMemoryTests` five runs one after another, AMD Ryzen
7 5800X3D, Windows 10 Pro): before (bands) 196.0 s median; after (tiles) 187.6 s, 181.1 s, 195.0 s, 188.8 s, 167.8 s (measured on commit `ab42c20`, before the new tests); **median 187.6 s** (4 % faster than before), peak
working set 1566 to 1567 MB in every run, as before. The runs overlapped only this agent's light file edits.

**Frames.** scenario-patina's and scenario-planks' frames compared by hash with `main`'s: not yet compared (see the Hand-off).

### Hand-off

Written by Claude Opus 5.5 (`claude-opus-5-5`) at the call checkpoint. "As built" above is a draft: correct it as the
rest lands.

Done: the switch (commit `ab42c20`: types, evaluator, cache, the seven nodes and the test nodes ported; patina-unit
passed on it, every committed PNG unchanged); the 8192 time (above: median 187.6 s against 196.0 s, no stop); the new
tests and the docs (CLAUDE.md's rule and its two Layout rows, `ExportBoundedMemoryTests`' summary), in the commit after.

Left, in order:
1. `AFarReadingNodeStaysUnderTheCap` fails: `held 8454144 bytes, past the cap of 8388608`, one 64 KB tile over.
   `TileCache.AddPinned` takes `PeakBytes` after adding the new tile and before `Trim`, and the tile is allocated
   before anything is dropped, so the cache really does hold cap + one tile for a moment. Fix it in the evaluator, not
   the test: before allocating a node's output tiles, drop unpinned tiles until `Bytes + incoming <= Cap` (a
   `TileCache.MakeRoom(long incoming)`), then add. `TilesAreBoundedAndTheCacheStaysUnderItsCap`'s worked runs should
   still hold (check the cap-0 cases: held is then exactly the pinned tiles). Every other patina-unit test passed.
2. `export`'s description (`src/Patina.Server/ExportTool.cs` line 39): "in bands" to "in tiles", and "about 2.5
   minutes" to the measured time (about 3 minutes, 187.6 s here). Check whether a test quotes that text.
3. scenario-patina's and scenario-planks' frames by hash against `main`'s, fetched by job id.
4. `cairn task T-133 verify`, then fold this Hand-off into As built, commit, `cairn lint`, `cairn task T-133 finish`.

Not done: the research's commit 1 was not split from commit 2 (`PortKind` moved out of `Band.cs`; As built says so).
Surprising: a stray empty folder `D:\Users\andre\AppData\Local\Temp` was made on D: by a mistyped path (the
script in it was moved to `%TEMP%`); a safety check refused its removal, so it is left for the person to delete.
