---
description: >-
  A wide neighbourhood node runs as a cached row pass and a column pass, so blur and distance at 8192 meet 10 s within the memory cap, and a free rotation stops recomputing its source
---
# T-155 — Separable passes and rotated-read locality

**Status:** In Progress

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

### Research (Claude Opus 5.5, `claude-opus-5-5`, 2026-11-20)

Measured with a Release harness outside the repo (`%TEMP%\t155\measure`, T-153's console calling
`GraphEvaluator.Evaluate` with no packing; AMD Ryzen 7 5800X3D, tiles of 128, 64 MiB cap), on this branch before any
change, and a cache simulation (`%TEMP%\t155\sim.py`: the evaluator's LRU and pinning, tile counts only).

- **The source alone is past 10 s.** A default `fbm` through a two-stop `gradient_map` (colour) at 8192: **12.4 s**,
  8,192 tiles. T-138's colour fbm source took 16.8 s. So `blur` at 8192 on such an input cannot be 10 s in all; only
  the blur's own share (the run less its source alone) can. The same graph's blur, smooth 0.05, at 2048: 6.7 s (T-137
  had 10.5 s on its own input).
- **Under the 64 MiB cap the source of a wide colour blur must be made more than once.** A smooth blur at 0.05 and
  8192 reaches 1,229 texels each way, 10 tiles. Every output tile depends on a 21 x 21 square of source tiles, so
  whatever the order, a source band 21 tile rows high (or wide) is live at once: as the row pass's colour tiles that
  is 21 x 64 x 192 KB = 252 MB, four times the cap. No schedule computes the source once under 64 MiB; any that keeps
  the cap trades the shortfall for recomputation. Simulated with two passes cached as separate tiles: raster order
  makes the source 441 times over (1,806,336 fbm tiles for 4,096), column order 27.6 times. The best I found under
  the cap is bands of 14 tile columns walked downward, the row pass's tiles kept for the band: the source about 2.4
  times, about 30 s with this source.
- **Distance fits.** Its mask and its row pass are greyscale; at range 0.1 and 8192 a band is 15 x 64 x 64 KB =
  60 MiB. The pinned window drops from 196 tiles to about 30 once the row pass is its own tile, so the peak is under
  the cap at every range (range 0.25 recomputes the row pass, cheaply).
- **The rotation.** The turn's source is used by two or three consecutive output rows, and in raster order a whole
  row of output tiles (a slanted band of about 200 source positions, four node tiles each) passes between those uses,
  more than the cap holds, so it is evicted and made again: the 2.4 times. A locality-preserving order of output tiles
  (bands a few tiles wide walked downward, or a Hilbert curve) keeps a tile's later uses close to its first. The
  callers of `Evaluate` (`OutputImage`, `ExportTool`) place tiles by position, so the order may change.

**Decision asked of the person** (round-1): what the blur's 10 s means, and whether the cap may grow for a separable
node so the source is made once.

**The person's rulings (round-1, 2026-11-20):** Q1 (a): the blur's 10 s is its own share, the run less its source
alone. Q2 (A): keep the 64 MiB cap and recompute as little as it allows ("let's try for A").

### Hand-off

From the first agent (Claude Opus 5.5, `claude-opus-5-5`), at its call checkpoint. Work in progress, committed so the
next agent can carry on; `verify` has not been run.

**Done (uncommitted until this commit, built, Patina.Graphs tests run):**
- API in `Nodes/NodeType.cs`: `Separable(parameters, size)` returning `SeparablePasses(RowReach, ColumnReach)` or
  null, `EvaluateRows`, `EvaluateColumns`, `RowsPort = "rows"`. Design call: the passes are internal to the node
  (not hidden nodes at validation), the evaluator caching the row pass under a key of its own (`steps.Length + i`).
  Reason: a hidden node would need a port kind and a name in the graph, would show in faults and `get_graph`, and
  T-140's expansion would have to know it; internal passes keep the graph as written and the node's one-pass
  `Evaluate` as the reference the tests hold the passes to.
- `BlurNode` and `DistanceNode` opt in. Distance's row tile holds the signed u distance to the other side capped at
  the reach (+ inside, - outside, infinite when none), so it is the same in every tile.
- `Imaging/TileRead.CopyRow` (a long row in runs of `Row`).
- `Evaluation/GraphEvaluator.cs` rewritten: tiles made depth first (`Ensure`/`MakeTile`/`MakeRows`/`Finish`), each
  tile's inputs unpinned once it is made, so the pin is one path of reads; output tiles in bands of at most 16 tile
  columns walked downward (`Order`, `BandTiles`: narrowed so a band's row tiles fit three quarters of the cap).
- `Evaluation/TileCache.cs`: `PinnedBytes`, `PeakPinnedBytes`, `Bound = max(cap, most ever pinned)`; trims to the
  bound. This is what should bring the strong minify under 2 GB (only transform's direct input, the whole image, is
  pinned, and kept, rather than every node behind it): **not yet measured**.
- `EvaluationTests.TilesAreBoundedAndTheCacheStaysUnderItsCap`: its cap-0 line changed from 35 tiles held and 2,240
  made to 18 and 2,004 (both lower: depth-first pinning, and the bound). **This is a changed test target, so it goes
  to the person** before landing, though it is tighter, not looser.
- `SeparablePassesTests.cs` (new): blur (4 cases) and distance (4 cases) equal their one-pass copies (`blur_one`,
  `distance_one`, a wrapper not opting in) at tiles 16, 64, 256 and caps 0 and default: **all pass**. Blur's cap test
  passes. All 371 earlier Patina.Graphs tests pass.

**Failing / left:**
- `Distance_at_its_largest_range_holds_no_more_than_the_cap` fails: held 1,261,568 bytes against a 1 MiB cap, and
  takes about 2 minutes (too slow for `unit`). Cause, I think: `tiles` has four ports (mask, height, random, uv), held
  together, so each mask entry pinned is about 96 KB rather than 16 KB; and `BandTiles` counts only the row tiles, not
  the inputs a row tile pins. Fix the budget (count the input entries' bytes) or the test's cap/size, and make it fast.
- **Blur at 8192 thrashed** with a seven-eighths budget (360 of 4,096 tiles in 600 s): the band's row tiles plus the
  churn of source tiles just passed the cap and LRU fell over. Changed to three quarters; not measured since. Expect
  the source made about 2.6 to 3 times (bands of 11 to 13), so the blur's own share at 8192 is likely about 25 to
  30 s on the default fbm source (12.4 to 14.1 s alone): **probably not 10 s, which is a stop to report** with the
  figure, per the ruling's "try".
- Measure everything: `bash %TEMP%\t155\run-after.sh > %TEMP%\t155\after1.log` (blur 0.05 and 0.1, distance 0.1 and
  0.25, the colour fbm source, rot30, tile16 at scale 0.25, at 8192, Release; harness `%TEMP%\t155\measure`, pointed
  at this worktree). Run it with the tool's background option, not `&`. Then the cobblestones and planks at 8192
  (`Measure file <path> 8192 <limit>`) against T-153's figures (before figures need a run on `origin/milestone/M-28`).
- Rotation: the bands should cut its recompute; measure, then add the ratio test (a 30-degree turn at 2048 in tiles
  of 32 under a 4 MiB cap, so it is 64 x 64 tiles as at 8192).
- `TransformDistanceBoundedMemoryTests` (patina-regression): distance up to 8192 with the 10 s assert, blur at 8192,
  the strong minify at 8192, each at 2 GB or less.
- CLAUDE.md Layout lines (Patina.Graphs: separable passes, the bands; tests: `SeparablePassesTests`), As built, rebase
  onto `origin/milestone/M-28` (T-141 landed), `verify`, `finish`.

**Before figures so far** (this branch before code, Release harness): default fbm through gradient_map at 8192
12.4 s (14.1 s on a later run); blur smooth 0.05 at 2048 6.7 s, 960 tiles. **After so far:** blur at 2048 3.3 s,
1,312 tiles, peak 64 MiB, working set 179 MB (with the seven-eighths budget).

**The person's rulings since the first hand-off (2026-11-20):** the `EvaluationTests` cap-0 target of 18 tiles held
and 2,004 made (against 35 and 2,240 before) is approved. The two round-1 rulings stand as recorded: blur's 10 s at
8192 is blur's own share, the run less its source alone; the 64 MiB cap is kept, with as little recomputation as it
allows.

**The person's ruling since the second hand-off (2026-11-20):** `patina-regression` runs a Debug build, so its 8192
regression tests assert the 2 GB peak only. The 10 s checks (blur's own share, distance) are measured in Release, like
every other speed figure here, and written in As built.

### Hand-off (second agent, Claude Opus 5.5, `claude-opus-5-5`, at its call checkpoint)

Rebased onto `origin/milestone/M-28` (T-141 landed) cleanly, no conflicts. `verify` has not been run; the unit tests
have not been run since this agent's changes either (nothing heavy ran while measuring).

**Done this round (committed, built in Release through the harness only):**
- `BandTiles` now takes off what a row tile pins while it is made (the whole entries of its inputs across the row's
  reach, every port of a source such as `tiles`) before dividing by a column's row tiles.
- `Distance_at_its_largest_range_holds_no_more_than_the_cap` now reads a one-port greyscale `fbm` mask
  (`FbmDistance`), so the window held is distance's own: one pass pins 10 x 10 greyscale tiles of 64 (1.56 MiB) against
  the 1 MiB cap. **Not run yet**; check it passes and how long it takes.
- `A_30_degree_turn_makes_its_source_close_to_once` (new, 2048, tiles of 32, 4 MiB cap: 64 x 64 tiles as at 8192),
  its bound `MostTurnRemakes = 1.25`, which is **a placeholder, not measured**: run it, write the ratio reached,
  and set the bound just above it (a new target, so it is stated in As built, not loosened later).
- CLAUDE.md Layout: `Patina.Graphs` (depth-first making, bands, separable passes; blur and distance separable) and
  `tests/Patina.Graphs.Tests` (`SeparablePassesTests.cs`). The `tests/Patina.Server.Tests` line still needs the new
  regression tests once they exist.
- Harness: `%TEMP%\t155\measure` takes a seventh argument, the cap in KiB (`Measure <label> <size> <limit> <nodes>
  <output> <tile> <capKiB>`). `%TEMP%\t155\measure-base` is the same harness on `origin/milestone/M-28`'s `src`
  (extracted to `%TEMP%\t155\base`), for the before figures; `%TEMP%\t155\run-samples.sh` runs the cobblestones
  and planks at 8192 before and after, interleaved. `%TEMP%\t155\run32.sh` is the 2048 proxy below.

**Blur at 8192 still thrashes; the cause is found, not fixed.** With the three-quarters budget, blur smooth 0.05 at
8192 stopped at 400 s with 268 of 4,096 output tiles (about 6,100 s in all). A proxy with the same tile geometry runs
16 times faster: **2048 in tiles of 32 under a 4 MiB cap** (64 x 64 tiles, reach 9.6 tiles as at 8192); its source
alone is 1.1 s, the blur about 360 to 400 s. Counting makes per node (temporary, taken out again) showed a band of 10
columns and **21 row tiles made for every output tile, from the first band row to the last**: no row tile is ever
reused. The band's row tiles fit the cap (10 x 22 x 12 KB plus the churn is about 3.1 MB of 4 MiB), so the budget is
not the cause. The cause is the cold start: at a band's first row nothing is held, so each output tile makes its 21
row tiles a column at a time, each reading 21 source tiles, about 630 source tiles (some 10 MB) for one output tile.
That flushes the row tiles made for the tiles beside it, so the next band row misses again, and LRU never recovers
(the seven-eighths and three-quarters runs failed the same way).
**Suggested fix:** in the order loop, before a band's output row, make (`Ensure` and unpin) the separable output
step's row tiles the row needs that are not held, a row of row tiles at a time across the band (and, at the band's
start, the 2 x reach rows above as well, row by row), so a row tile's source row is read once for the whole band.
Then the column tiles find every row tile held. For a separable step deeper in the graph than the output node, the
same warm-up applies to the step whose column pass the output tile reaches; the general rule is to walk the row
pass's tiles in raster order within the band. Check it on the proxy first (expect the blur at about 3 to 4 times
its source), then at 8192.

**Left, in order:**
1. The warm-up above; the proxy, then `bash %TEMP%\t155\run-after.sh > %TEMP%\t155\after2.log` in the background
   (blur 0.05 and 0.1, distance 0.1 and 0.25, the colour fbm source, rot30, tile16 at scale 0.25, all at 8192). Blur's
   own share = the blur run less `src` (the default fbm through gradient_map, 12.4 to 14.1 s alone). If it is over 10 s,
   stop and bring the figure and the options to the person.
2. `bash %TEMP%\t155\run-samples.sh > %TEMP%\t155\samples.log` (before and after, cobblestones and planks at 8192;
   T-153's after figures for the cobblestones: base_colour 75.5 s, normal 129.2 s, roughness 57.4 s, occlusion
   54.9 s; T-153 has no planks figure at 8192 in Release, so the before run is the planks' baseline).
3. Run `SeparablePassesTests` (the distance cap test and the rotation ratio) and the whole Patina.Graphs suite.
4. `TransformDistanceBoundedMemoryTests`: distance raised to 8192, blur at 8192, the strong minify (tile 16 at scale
   0.25) at 8192, each at 2 GB or less. **A question for the person:** the suite `patina-regression` runs a Debug
   `patina` (`tests.json`), and the spec asks this test to assert distance's 10 s; Debug may well be past 10 s where
   Release meets it. Measure Debug first and ask before asserting a time a Debug build cannot meet.
5. CLAUDE.md's `tests/Patina.Server.Tests` line; As built (API, a worked opting-in node, every figure, the models);
   `verify`, commit, `cairn lint`, `finish`.

**Figures so far.** Before (this branch before code): default fbm through gradient_map at 8192 12.4 s, 14.1 s; blur
smooth 0.05 at 2048 6.7 s, 960 tiles. After: blur at 2048 3.3 s, 1,312 tiles, peak 64 MiB, working set 179 MB
(seven-eighths budget); blur at 8192 not finished (above). Proxy (2048, tiles of 32, 4 MiB): source 1.1 s, 8,192
tiles; blur stopped at 300 s with 3,091 of 4,096 output tiles.

### Hand-off (third agent, Claude Opus 5.5, `claude-opus-5-5`): stopped, two speed checks not met

Stopped and reported rather than finished: the person's ruled 10 s checks for blur's own share and for distance at
8192 are not met in Release, and the cause is now the passes' own arithmetic as well as the source's recompute.
`verify` has not been run; the Patina.Graphs suite (432 tests) passed before the turn test's count was fixed, and
`SeparablePassesTests` (11) pass after it.

**Done this round (committed):**
- **The blur thrash is fixed.** Two causes, both in the evaluator, neither changing a byte:
  - *Warm-up* (`GraphEvaluator.WarmRows`): before a column tile reads its row tiles, each row of them not held is made
    across the rest of the band, left to right, so a source row is read once for the band, not once per output tile.
  - *Cold tiles* (`TileCache`, the `cold` flag in `GraphEvaluator`): everything unpinned while a row pass is being made
    (its inputs, and their inputs in turn) goes on a cold list dropped before any other. The warm-up alone did not help:
    the row's sources, unpinned between its row tiles, sat in LRU among the row tiles and pushed them out (a proxy of
    512 in tiles of 8 under 256 KiB, the same 64 x 64 tile geometry, made 136 row tiles per output tile with the
    warm-up alone, 22 with cold sources from the row pass only, and 1 with everything under the row pass cold).
    A column pass made inside an outer row pass unpins its own row tiles warm again.
- `A_30_degree_turn_makes_its_source_close_to_once`: its count was wrong (it took the output node to make tiles, which
  it does not), fixed; **measured 1.16** (14,196 source tiles against 12,288 alone, at 2048 in tiles of 32 under
  4 MiB), so `MostTurnRemakes = 1.2`, a new target set from that run.
- `TransformDistanceBoundedMemoryTests`: distance raised to 8192, blur smooth 0.05 at 8192 and the strong minify
  (tile 16 x 16, scale 0.25) at 8192 added, each asserting the 2 GB peak only (the ruling above). Deadline 60 min
  as a placeholder: **not yet run in Debug**, so neither the Debug times nor the peaks are measured.
- The ruling since the second hand-off is in the Notes.

**Figures at 8192, Release, tiles of 128, 64 MiB cap (harness `%TEMP%\t155\measure`, this branch):**

| run | before | after |
|---|---|---|
| default fbm through gradient_map, alone (`src`) | 12.4 s, 14.1 s | 11.7 s, 8,192 tiles |
| blur smooth 0.05 on `src` | about an hour (T-137); about 6,100 s (thrash, this branch earlier) | **132.1 s**, 43,744 tiles, peak 64 MiB, working set 281 MB |
| blur smooth 0.05 on a flat `colour` (its own arithmetic) | — | **110.1 s**, 26,608 tiles, working set 499 MB |
| blur smooth 0.1 on `src` | — | stopped at 200 s, 2,476 of 4,096, about 331 s |
| `tiles` mask alone | — | 7.7 s, 4,096 tiles |
| distance 0.1 inside on the `tiles` mask | 474 s, pinned 84 MiB (T-138) | **25.3 s**, 18,448 tiles, peak 64 MiB, working set 193 MB |
| distance 0.25 both | — | 75.6 s, 31,872 tiles, peak 64 MiB |
| colour fbm (8 x 8, 4 octaves) alone (`csrc`) | 16.8 s, 16,384 tiles (T-138) | 14.2 s, 8,192 tiles |
| 30-degree turn of `csrc` | 46.7 s, 39,478 tiles (T-138) | **21.2 s**, 13,528 tiles: source made 1.15 times |
| tile 16, scale 0.25 of `csrc` | working set 4,348 MB (T-138) | 30.0 s, peak held 768 MiB, **working set 1,561 MB** |

Blur's own share (the run less its source alone) is **120.4 s** against 10 s; distance is **25.3 s** against 10 s
(17.6 s less its mask). The pinned windows are at the cap for both, and the turn and the minify meet their checks.

**Why 10 s is out of reach as built.** Blur on a flat source still takes 110 s: each 128-texel tile's running sum
runs the whole widened window, 128 + 2 x 1,229 = 2,586 texels, for every one of 128 rows, three box passes, in each of
the two passes; about 20 times the arithmetic of a whole-image pass. On a real source the band (10 tiles wide, reach
10 tiles each side) makes the source about 4.3 times over besides (35,456 source tiles for 8,192), which the research
found cannot fall below about 2.4 times under 64 MiB.

**Options for the person:**
1. **Strip passes** (an API change to T-155's own API): the row pass made over a whole band's tile row at once (one
   running sum across `band x 128 + 2r` texels, cut into row tiles) and the column pass over a tall strip of output
   tiles. That cuts the 20 times to about 3. Estimate: blur's arithmetic about 15 to 20 s, plus the source's recompute
   (about 3.3 x 11.7 s), so blur's own share about 50 s, distance about 12 to 15 s. Still over 10 s under the cap.
2. **Strip passes and a larger cap for separable nodes** (reverses round-1 Q2 (A)): with the source made once, blur's
   own share is about the strip arithmetic, perhaps 15 s; 10 s is still not sure.
3. **Accept the figures** above as the speed reached, and move the 10 s checks to a later task (with option 1 as its
   scope), landing what is here: the thrash fixed, the pinned windows at the cap, the turn and the minify met.
4. **Lower the bounds** (blur's radius, distance's range) at which the 10 s is asserted.

**Left once the person rules:** the samples at 8192 before and after (running at hand-off: `%TEMP%\t155\samples.log`;
T-153's cobblestones: base_colour 75.5 s, normal 129.2 s, roughness 57.4 s, occlusion 54.9 s); run
`TransformDistanceBoundedMemoryTests` in Debug and set its deadline from it; CLAUDE.md's `tests/Patina.Server.Tests`
line; As built; `verify`, `cairn lint`, `finish`.
