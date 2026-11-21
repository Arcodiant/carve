---
description: >-
  Review of M-28
---
# RV-76 — Review of M-28

**Status:** Open

**Reviews:** M-28

**Round:** 1

**Previous:** —

**Diff:** b00f5cc8693b458707631f243d3d7885ca4199a6 against origin/main

**Reviewer:** five reviewers, each claude-opus-5-5 (Claude Opus 5.5): the evaluation engine; the nodes, check rules and samples; the file format and graph model; the patina server; the scenarios and test configuration with a pass over the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the evaluation engine (claude-opus-5-5)

Reviewed on Claude Opus 5.5 (`claude-opus-5-5`). No tests run; every figure below is from the code or from arithmetic on it.

### Kept tile arrays are outside the bound, and `PeakBytes` and the cap tests do not count them
- **Where:** `src/Patina.Graphs/Evaluation/TileCache.cs:119-128` (`FreeLimit => Math.Max(4L << 20, Bound / 16)`), against `GraphEvaluator.cs:25-30` (the memory bound paragraph) and `:336-341` (`EvaluationRun`: "the most bytes held at once"); tests `SeparablePassesTests.cs:99, 113, 144`, `EvaluationTests.cs:141-143`.
- **Severity:** Minor
- **Rubric:** the memory bound; API contract against docs; tests that pass for the wrong reason.
- **What:** dropped arrays wait in `_free` and are not counted in `Bytes`, so `PeakBytes` leaves them out. The free list can hold up to 4 MiB however small the cap, plus a sixteenth of the bound (48 MiB for the strong minify's 768 MiB). The class doc says what is held stays at most the cap or the most pinned. `EvaluationTests` says "With no cache, one tile is held at a time" (cap 0, `PeakBytes` = one 64 KB tile), but up to 4 MiB of arrays can still be live then. `Distance_at_its_largest_range_holds_no_more_than_the_cap` uses a 1 MiB cap, and up to 5 MiB can be live. The blur and turn tests use 4 MiB caps, and up to twice the cap can be live. These tests prove only what the cache counts, not what it keeps reachable. At the default 64 MiB the gap is 6%, but the contract and the tests' wording promise the cap.

**Outcome:** fixed

**Fix:** the contract now says what is kept reachable. `PeakBytes` stays the tiles (under the bound, as the cap tests hold); the new `GraphEvaluator.LastHeldBytes` counts the tiles, the dropped arrays waiting (at most the larger of 4 MiB and a sixteenth of the bound) and nodes' scratch, and the docs of `TileCache`, `GraphEvaluator` and `EvaluationRun` say so; `Dropped_arrays_and_scratch_are_counted_in_what_is_held` holds it. Putting the waiting arrays and scratch under the bound was tried and took their room from the tiles: a blur at 1024 in tiles of 64 under 4 MiB made 12,640 tiles against 1,840, so that is left as a question for the person rather than landed. (claude-opus-5-5)

### A strong minify of a multi-port node pins every port of the whole image, past 2 GB at 8192
- **Where:** `src/Patina.Graphs/Evaluation/GraphEvaluator.cs:155-161` (`Gather` pins the whole `Entry`), `TileCache.cs:23` (`Bytes` sums every port), `TileCache.cs:45-49` (`Bound`); `tests/Patina.Server.Tests/TransformDistanceBoundedMemoryTests.cs:25` (the one minify tested).
- **Severity:** Minor
- **Rubric:** the memory bound at 8192.
- **What:** `transform` at tile 16 x 16 and scale 0.25 reads its whole input for every tile, and a read pins the whole entry: every output port of the source node at that position. The 2 GB test minifies `blend`, which has one colour port: 8192² x 3 x 4 = 768 MiB pinned. The same minify of `tiles.mask` pins all of `tiles`' ports (`mask`, `height` and `random` grey, plus colour `uv`: 6 floats a texel). That is 8192² x 6 x 4 = 1.5 GiB. `Bound` then becomes 1.5 GiB and the free list adds 96 MiB, on top of the export's 256 MB RGBA buffer and the runtime: past the 2 GB the milestone checks. A minify of a minify does the same: the outer one pins its whole input while each inner tile pins its own, about 1.5 GiB. "Holds that and no more" (`TileCache.cs:45-48`) holds only for a one-port source. Neither case is tested, and no sample hits either today (`floor-tiles` only turns by 90°).

**Outcome:** fixed

**Fix:** an entry keeps only the ports the evaluation reads; the others are filled into arrays released once the tile is made, and `BandTiles` counts only the ports read. `A_strong_minify_of_one_port_of_tiles_pins_that_port_alone` (unit, 512) and `A_strong_minify_of_the_tiles_mask_at_8192_peaks_at_2_GB_or_less` (regression) hold it. A minify of a minify is not changed: both whole images are read at once, the most-pinned case the bound already names. (claude-opus-5-5, as the person ruled 2026-11-21)

### `BandTiles` casts to `int` after the clamp, so a very large cap makes the band negative and `Evaluate` loops forever
- **Where:** `src/Patina.Graphs/Evaluation/GraphEvaluator.cs:316` (`band = Math.Min(band, (int)Math.Max(1, (cap / 4 * 3 - reading) / column))`), and `Order` at `:293-299`.
- **Severity:** Minor
- **Rubric:** cache correctness; API contract (the public `cacheBytes`).
- **What:** the quotient is a `long`, clamped to at least 1 and only then truncated to `int`. With `cacheBytes: long.MaxValue` (a natural way to ask for "no cap") and a blur in tiles of 128, the quotient is 35,184,372,088,830, which truncates to **-2** (worked out for reaches 2, 10 and 100 texels). `Math.Min(32, -2)` makes the band -2. `Order` then steps `b += -2` from 0, so `b < n` stays true for ever and the inner loop yields nothing: the call hangs, doing no work. The server never passes a cap, so no tool can reach this, but `GraphEvaluator.Evaluate` and `OutputImage` take the cap publicly. Clamp in `long` before the cast: `Math.Min(band, Math.Max(1, ...))`.

**Outcome:** fixed

**Fix:** clamped in `long` before the cast; `A_cap_of_long_MaxValue_evaluates` holds it, with the bytes of a default run. (claude-opus-5-5)

### Blur and distance's scratch buffers are large objects allocated per tile, outside both the bound and the array reuse
- **Where:** `src/Patina.Graphs/Nodes/BlurNode.cs:81, 105, 141, 159`; `DistanceNode.cs:70, 106`; against `TileCache.cs:116-118`.
- **Severity:** Minor
- **Rubric:** the memory bound at 8192.
- **What:** T-155 reused tile arrays because dropped 192 KB colour tiles piled up as large-object garbage. A blur row tile at 8192, smooth radius 0.05, allocates a buffer of (128 + 2 x 1,229) x 3 x 128 floats, about 4 MB, then one more array for each of its three passes, about 8 MB of LOH garbage a row tile. Each column tile allocates about the same. The 8192 run made 43,744 tiles. That is the same pattern about 40 times larger, and neither the cache's bound nor `PeakBytes` sees it. The blur's 2 GB test passes (1,226 MB in Debug), but As built says the peaks "move with the collector's timing". This garbage is what they move with, and it scales with the radius.

**Outcome:** fixed

**Fix:** `NodeEvaluation.Scratch(length)` hands out the evaluator's arrays, the last tile's kept for the next and counted in `LastHeldBytes`; blur's two passes ping-pong between two scratch arrays and distance's column buffer is scratch. A blur at 1024 in tiles of 64 under 4 MiB allocated 397 MiB before and 18 MiB after (`A_separable_blur_allocates_little_past_its_cache`); bytes unchanged (the two-pass tests against the one-pass node pass). (claude-opus-5-5)

### No unit test fails if the warm-up, the cold list or the band narrowing is removed
- **Where:** `src/Patina.Graphs/Evaluation/GraphEvaluator.cs:238-249` (`WarmRows`), `TileCache.cs:30, 67-71, 104` (cold list), `GraphEvaluator.cs:306-319` (`BandTiles`); `tests/Patina.Graphs.Tests/SeparablePassesTests.cs`.
- **Severity:** Minor
- **Rubric:** tests: does each risky part have a test that fails without it.
- **What:** T-155 says these three ended a 6,100 s thrash of blur at 8192. The unit tests check bytes (which order never changes) and `PeakBytes <= cap`. The cache enforces that cap whatever the order, so it holds with or without the three. The only test that would catch a return of the thrash is the Debug regression tier's 30-minute deadline on the blur export (`TransformDistanceBoundedMemoryTests.cs:37`). Nothing at unit tier bounds a separable node's `TilesEvaluated` against its minimum, the way `WarpAtTheLargestAmountMakesFewMoreTilesThanTheMinimum` does for warp (`DependentInputTests.cs:117`). A blur at 1024 in tiles of 64 under a few MiB would do it.

**Outcome:** fixed

**Fix:** `A_blur_under_a_tight_cap_makes_close_to_the_fewest_tiles` holds a blur at 1024 in tiles of 64 under 4 MiB at 2.1 times the fewest (made 1,840 of 1,024); without the warm-up it made 2,716, without the cold list 48,620, without the band narrowing 11,407, each run. (claude-opus-5-5)

### CLAUDE.md says transform is separable
- **Where:** `CLAUDE.md:72` ("`transform` and `distance` (T-138: ...; both separable since T-155)").
- **Severity:** Nit
- **Rubric:** misleading docs.
- **What:** only `BlurNode` and `DistanceNode` override `Separable` (grep `override SeparablePasses` over `src`). `transform` runs in one pass. Its fix in T-155 was the order and depth-first making, not separable passes.

**Outcome:** fixed

**Fix:** CLAUDE.md now says distance is separable since T-155 and transform runs in one pass, made depth first in T-155's bands. (claude-opus-5-5)

### `WarmRows`' comment says a column tile read away from its output tile makes only its own; the code tests only the column's number
- **Where:** `src/Patina.Graphs/Evaluation/GraphEvaluator.cs:236-237` against `:241`.
- **Severity:** Nit
- **Rubric:** misleading comments.
- **What:** `last = x >= bandStart && x < bandEnd ? bandEnd : x + 1` compares a deeper step's tile column with the *output's* band. A separable node under a transform, or read across a row pass, whose column happens to fall inside the band therefore warms row tiles to the band's right edge it may never read. That costs time only, since bytes are unchanged. The comment should say "a column outside the band's columns", not "read away from its output tile".

**Outcome:** fixed

**Fix:** the comment now says a column tile in a column outside the band's makes only its own, and that the test is the column's number alone, so a deeper step's tile inside the band may warm row tiles it does not read (time, never bytes). (claude-opus-5-5)

### `TileRead.Row` with several rectangles gives up on the first one that holds the start texel
- **Where:** `src/Patina.Graphs/Imaging/TileRead.cs:190-198`, against the class doc at `:11-12` ("readable when it lies in any of them").
- **Severity:** Nit
- **Rubric:** API contract against docs.
- **What:** when part 0 holds `(x, y)` but the run passes its edge, `Row` throws without trying `_rest`, even if a later, overlapping part holds the whole run in one tile. `Grey`, `Colour` and `Index` do try every part. No live caller hits this: warp asks for one rectangle, and `pair`'s two do not overlap. But T-153's contract, "a texel in any of them is readable", is looser than `Row` keeps.

**Outcome:** fixed

**Fix:** `Row` tries each rectangle in turn and returns the first that holds the whole run in one tile; `A_row_past_the_first_rectangle_is_read_from_a_later_one_that_holds_it` holds it. (claude-opus-5-5)

### Export collects before encoding but not between the roughness and metallic evaluations
- **Where:** `src/Patina.Server/ExportTool.cs:165-181`.
- **Severity:** Nit
- **Rubric:** the memory bound at 8192 (the forced GC in export).
- **What:** when roughness carries metallic, the second `Evaluate` starts while the first one's whole cache is unreachable but not collected: up to its `Bound` plus the free list. The forced collection at `:181` comes only after both. The comment's reason for collecting is that two peaks should never stack, and that applies here too. With grey roughness and metallic it is at most about 256 MiB more for two strong minifies, so it is unlikely to pass 2 GB, and no test exports roughness with metallic at 8192.

**Outcome:** fixed

**Fix:** export collects between the roughness and metallic evaluations as it does before encoding. No test holds it: a forced collection's effect on the peak working set is not one a test can pin without an 8192 roughness-with-metallic export, which none of the samples needs. (claude-opus-5-5)

## Findings: the nodes, check rules and samples (claude-opus-5-5)

Reviewed on claude-opus-5-5, from `git diff origin/main...HEAD` in `D:\Repos\carve\branches\milestone\M-28`. No tests were run: none of these findings needed one. Each is shown from the code, and the Worley one also from a small independent simulation.

### A PNG's metallic (roughness's B) is exempt from does-not-tile, not only from one-value
- **Where:** `src/Patina.Graphs/Checks/DoesNotTileRule.cs:20` (`set.Outputs.Where(o => !o.Derived && ...)`), with `CheckSet.cs:113-117`.
- **Severity:** Minor
- **Rubric:** the check rules against the person's rulings.
- **What is wrong:** T-141's ruling 2 exempts the roughness PNG's blue channel from `one-value` only. It also says "`does-not-tile` runs on every output". The code skips every `Derived` output in `does-not-tile` too. In PNG mode the roughness output is read from G alone (`channel: role == rough ? 1 : null`), so nothing ever looks at B for a seam. A metallic seam in a `files` check is never reported. The same metallic made by a graph is checked (graph outputs are never `Derived`). So the one rule gives different answers for the graph and for its exported PNGs. No test covers a seam in B.

**Outcome:** fixed (T-174, claude-opus-5-5): `does-not-tile` judges every output, the metallic read from a packed image's B too, as the person ruled (2026-11-21: the exemption was only ever from `one-value`). `A_seam_in_a_metallic_roughness_pngs_blue_channel_is_a_seam_in_metallic` fails without it.

### An all-metal graph cannot check clean: one-value fires on a constant metallic, and its fix is wrong
- **Where:** `src/Patina.Graphs/Checks/OneValueRule.cs:187-194`.
- **Severity:** Minor
- **Rubric:** check rules, edge cases (an all-metal image).
- **What is wrong:** A graph whose `metallic` output is 1 everywhere (bare steel) raises `one-value` with "a mask of one value masks nothing". The fix it gives is "if the one value is meant, leave the output out of the graph", but leaving metallic out makes the material non-metal. So the fix changes what the graph is. The same material checked from its PNGs is clean, because the B channel is `Derived` and exempt, so graph and PNG disagree again. The ruling names metallic among one-value's outputs, so firing is as ruled. The defect is the inconsistency with the PNG path and a fix that cannot be followed. Either the message for `metallic` should say something else, or a constant 0/1 metallic should be exempt in graph mode as it is in PNG mode. No test has an all-metal graph.

**Outcome:** fixed (T-174, claude-opus-5-5): by the person's ruling (2026-11-21), metallic is exempt from `one-value` from a graph or a PNG alike, and `metallic-not-binary` still judges its values; check's description says so. `A_graphs_constant_metallic_is_exempt_from_one_value_as_a_pngs_is` fails without it.

### No test tells the 99th percentile from the largest interior step
- **Where:** `tests/Patina.Graphs.Tests/CheckRulesTests.cs:172-262` (every does-not-tile case is `Image("mask", 32, 32, ...)`). The rule is at `src/Patina.Graphs/Checks/DoesNotTileRule.cs:60`.
- **Severity:** Minor
- **Rubric:** tests (a risky part without a test that would fail without it).
- **What is wrong:** T-141's ruling uses the 99th percentile by nearest rank. With 32 columns there are 31 interior boundaries, rank `ceil(0.99*31)-1 = 30`, which is the largest one. T-141's As built says so itself. A broken copy using `inside.Max()`, or an off-by-one in `rank`, passes every test in the file. The broken-copies table (lines 238-262) has "the wrap compared with the mean step" but no "with the max". The percentile and max differ only when there are 101 or more boundaries, for example 256 at the samples' size, where the top 2 interior steps are dropped. No case at that width checks that a lone interior outlier is ignored.

**Outcome:** fixed (T-174, claude-opus-5-5): a 256-wide ramp with two lone outlier steps (dropped: still a seam) and a third (kept: clean), in a theory and as three broken copies (the largest step, one rank too high, one rank too low). The rule is unchanged.

### warp still calls Math.Cos and Math.Sin, where transform and shape use the series
- **Where:** `src/Patina.Graphs/Nodes/WarpNode.cs:73-77`.
- **Severity:** Nit
- **Rubric:** determinism (hand-rolled maths that is exact).
- **What is wrong:** `Direction` uses `Math.Cos` and `Math.Sin` for a grey `by` at a non-zero `angle`. It is now the only transcendental call in `Nodes/` (grep `Math(F)?\.(Sin|Cos|Pow|Exp|Log|Tan|Atan)`). .NET leaves these to the platform's C runtime, so the last bit may differ between machines, while `TransformNode` (line 24-25) and `ShapeNode` (line 12-13) use `ShapeNode.SinCos` "so the result is the same on every machine". NodeType's rule (line 13) forbids only Pow and Exp, so this breaks no letter, and T-135's As built records it. No committed sample uses a grey `by` at an angle (cobblestones' warps use the default angle 0, where cos and sin are exact). So the risk is only in agents' graphs. Routing it through `TransformNode.SinCos(angle)` would also make 90 and 180 exact, where `Math.Cos(pi/2)` is 6e-17.

**Outcome:** fixed (T-174, claude-opus-5-5): warp's direction goes through `TransformNode.SinCos` (the series, exact at quarter turns). `PatinaGraphsIsEngineFreeTests.CallsNoPlatformTranscendental` scans the assembly's member references for `Math`/`MathF` Sin, Cos, Tan, Atan, Pow, Exp, Log and the like, and fails without it. No committed sample uses an angle, and every sample's bytes are as committed.

### worley's 3 x 3 search misses the true second-nearest point at the default jitter 1
- **Where:** `src/Patina.Graphs/Nodes/WorleyNode.cs:55-63`, with `jitter` defaulting to 1 at line 28.
- **Severity:** Nit
- **Rubric:** formulas against the node description.
- **What is wrong:** The description says `f1` is "the distance to the nearest point" and `edge` is "f2 - f1". With jitter 1 a point can sit anywhere in its cell. A texel near its cell's left edge can then have a point in cell `ix - 2` (outside the window) at just over 1 cell away, closer than every point inside the window. A simulation with uniform points (jitter 1, 16 x 16 cells, 40,000 samples, the 3 x 3 window against a 5 x 5 one) found f2 wrong in 0.0125% of samples and f1 never. So `edge` has rare one-texel jumps where the window shifts. This is minor in practice, and the description does say "searched in the 3 x 3 cells". But no test compares worley with a brute-force search, as distance's tests do.

**Outcome:** fixed (T-174, claude-opus-5-5): after the 3 x 3 cells, worley searches each further ring that could still hold a point nearer than f2 (a bound from the texel's distance to its cell's edge and the jitter's margin), up to ring 4. `WorleyFindsTheTrueNearestTwoPointsAsASearchOfEveryCellDoes` compares with a search of every cell at jitter 1 by both metrics, and shows a 3 x 3 search misses some. The committed samples (cobblestones at jitter 0.85, painted-metal at 0.9) are byte for byte as committed: their tests pass unchanged.

### stripes' softness ramp is twice the width the description gives
- **Where:** `src/Patina.Graphs/Nodes/StripesNode.cs:31-37` against its description at lines 9-10.
- **Severity:** Nit
- **Rubric:** formulas against the node descriptions.
- **What is wrong:** The description says "`softness` 0..0.5 of a period, the smoothstep across each edge, centred on it". The code ramps from `half - softness` to `half + softness`, so the ramp is `2 * softness` of a period wide. At the top of the range (0.5) the ramp covers the whole period. A width-0.5 stripe then never reaches 1 or 0 (it peaks at 0.84 and bottoms at 0.16). The hand-worked test (`PatternNodesTests.cs:320-322`) is "worked from the ramp's formula", so it holds the code rather than the description. Either the description should say "softness each side of the edge", or the ramp should be `softness` wide. The fabric sample (softness 0.35, width 0.9) was approved by eye as it is, so changing the code would change its look.

**Outcome:** fixed in the words (T-174, claude-opus-5-5): the code is kept, so the fabric sample keeps its approved look; `StripesNode`'s description and the `nodes` catalogue now say softness is the ramp's reach each side of the edge, so the ramp is 2 x softness wide.

## Findings: the file format and graph model (claude-opus-5-5)

Reviewed on Claude Opus 5.5 (`claude-opus-5-5`). Diff: `git diff origin/main...HEAD` in `branches/milestone/M-28`.
I checked the claims below with F# scripts run against the worktree's built `src/Patina.Graphs/bin/Debug/net10.0/Patina.Graphs.dll`, which dates from the HEAD commit. The scripts and logs are in `%TEMP%\m28-probe\` (`probe.fsx`, `probe2.fsx`, `probe3.fsx` and their `.log` files).

### An outer node whose id collides with an expanded id replaces the nested node, with no fault
- **Where:** `src/Patina.Graphs/GraphValidator.cs:70` (`nodes[id] = node`), `:340` (`nodes[newId] = ...`), `:79-80` (`isExpanded ? GraphNames.IsValidPath(id) : GraphNames.IsValid(id)`).
- **Severity:** Major
- **Rubric:** 1 Correctness (validation hole); 2 Tests cover the Scope.
- **What is wrong:**
  - The reader accepts any string as a node id, so a file can hold an outer node `g/c` beside a `graph` node `g` whose nested file has a node `c`.
  - Nodes are walked in ordinal order, and `g` sorts before `g/c`. So `g` is expanded first and writes `nodes["g/c"]`, then the outer `g/c` overwrites it.
  - `a.Expanded` still holds `g/c`, so the id is checked with `IsValidPath`, which passes. The outer node's sources are also read as expanded (path) sources.
  - The result is a file that loads with **0 faults**, in which an outer node silently stands in for a node inside the nested graph. The probe gives `== collision: 0 faults`.
  - The same id without a matching `graph` node is faulted (`h/c`: "an id is [a-z][a-z0-9_]*"). Only the collision gets through.
  - The same hole exists one level down, inside a nested file.
- **Tests:** T-140's "renamed node clashing with a parent's id" (`tests/Patina.Graphs.Tests/NestedGraphTests.cs:129-130`) is a hand-written copy compared by pixels. Nothing tests an outer id that collides with an expanded one.
- **Fix:** fault an outer id that is not `IsValid` before expanding, or refuse a `nodes` key that is already present.

**Outcome:** —

### A `graph` node's path can be absolute, drive-relative or UNC, and is opened without question
- **Where:** `src/Patina.Graphs/GraphValidator.cs:45` (`Path.GetFullPath(Path.Combine(graph.Folder ?? ..., text))`), and `ParamSpec.FilePath`'s check in `src/Patina.Graphs/Nodes/NodeType.cs:215`, which accepts any non-empty text.
- **Severity:** Minor
- **Rubric:** 1 Correctness (the spec: "`path` to a `.texgraph`, relative to the using graph's file"); hostile file.
- **What is wrong:**
  - `Path.Combine` throws away `Folder` when the second argument is rooted:
    - An absolute path loads with 0 faults (probe "absolute path").
    - `ResolvePath(g, "//server/share/x")` gives `\\server\share\x.texgraph`.
    - A drive-relative `"C:"` resolves against the process's current directory on that drive, not the graph's folder. The probe faulted on `...\m28-probe\.texgraph`, which is the script's working directory, while `Folder` was `...\m28-probe\files`.
  - Each validation (open, get_graph, swatch, export, check) then calls `File.Exists` and `File.ReadAllText` on that path. A UNC path in a file someone is handed makes the server contact a remote SMB host, which can send the user's NTLM credentials to it.
  - `..` is used on purpose and tested (`NestedGraphTests.cs:293-294`), so the issue is only rooted paths.
- **Fix:** fault a `Path.IsPathRooted` path (drive-relative and UNC included). Nothing tests a rooted path.

**Outcome:** —

### Nested expansion has no bound but depth: fan-out makes validation exponential
- **Where:**
  - `src/Patina.Graphs/GraphValidator.cs:306` (`Analyse(set, ...)` once per `graph` node, with no cache of a file already read or analysed);
  - `:294` and `:309`, plus `GraphParameters.Analyse`/`NestedParameters` (`:383`), which re-read and re-analyse the nested file on each override, on `Of`, and on each drive.
- **Severity:** Minor
- **Rubric:** 1 Correctness (hostile file: a hang where a fault is wanted).
- **What is wrong:**
  - Depth is capped at 8, but each level may use the next level's file any number of times, and each use is read, parsed and analysed again.
  - Measured, with eight files each using the one below `fan` times:

    | fan | read and validate | faults |
    |---|---|---|
    | 2 | 0.27 s | 128 |
    | 3 | 3.4 s | 2187 |
    | 4 | 25.8 s | 16384 |

  - Growth is roughly 8× per step. A fan of 10, which is 9 small files, would not finish.
  - The cost is paid again by `Uses`, by `Validate` and by the evaluator, all from open.
  - Expansion is by design (inline, so evaluation is unchanged), but a hostile or careless file should fault, not hang. A cap on total expanded nodes, as a fault, would do it. So would a per-file parse cache plus a node-count limit.

**Outcome:** —

### A parameter whose value is a list of strings is written as a file that does not load
- **Where:** `src/Patina.Graphs/GraphParameters.cs:321-340` (`ParametersField.Value` reads only numbers in an array), against `TexGraphFile.Value` (`src/Patina.Graphs/TexGraphFile.cs`, the `Texts` case), which writes one.
- **Severity:** Minor
- **Rubric:** 1 Correctness (round trip; load error against fault).
- **What is wrong:**
  - Since T-171, `ParameterOverrides.Read` (`src/Patina.Server/ParameterOverrides.cs:69`) gives `ParamValue.OfTexts` for a string array. So `edit_graph add_parameter`/`set_parameters` with `value: ["#000000", "#ffffff"]` makes a `Texts` parameter value.
  - `Write` gives `"value": ["#000000", "#ffffff"]`, and `Read` of that text is a load error. The probe gives `line 5, column 17: parameters: 'p': value is a number, a string or an array of numbers`.
  - A number list in the same place loads and is faulted by kind. A node parameter's string list loads.
  - The file's own rule (TexGraphFile's summary) is that only shape errors are load errors and everything else is a `GraphFault`. A string list is valid JSON of the kind node parameters take, so it should load and be faulted ("is [...], but must be a number ..."), as a number list is.
  - Save refuses the faulted graph, so this does not reach disk through save. It does reach get_graph's text (`WriteShowingDriven`), which then does not read back.

**Outcome:** —

### `Write(graph, TexGraphFormat.Version1)` silently drops the parameters and the values they drive
- **Where:** `src/Patina.Graphs/TexGraphFile.cs:51` (the driven set is built from `graph.Parameters` whatever the format), `:57` (fields come from the format).
- **Severity:** Nit
- **Rubric:** 1 Correctness (round trip).
- **What is wrong:**
  - With `Version1`, `parameters` is not written, because the format has no such field. Each driven node parameter is still left out of its node, because the driven set ignores the format.
  - The probe gives a version 1 file with `"c": { "type": "colour" }`: the parameter and the node's own `#123456` are both gone, with no error.
  - Only a test calls it (`ExposedParametersTests.cs:61`, with no parameters), but it is public. It should throw when the graph uses something the format cannot hold, or at least keep the node values.

**Outcome:** —

### No test has a nested file whose size differs from the outer graph's
- **Where:** `tests/Patina.Graphs.Tests/NestedGraphTests.cs:29` and `:48` (inner and outer are both `"size": 64`). Every other nested fixture in the file is also the same size as its outer graph.
- **Severity:** Nit
- **Rubric:** 2 Tests cover the Scope ("the outer graph's seed and size rule").
- **What is wrong:**
  - The seed ruling is tested: inner 0, outer 5, with the hand-written copy at 5.
  - The size ruling holds by construction, since only nodes are taken. But no test would fail if a nested size started to leak into a node that reads the graph's size.
  - A fixture with an inner size of 32 under an outer 64, compared to the hand-written copy, would cover it.

**Outcome:** —

## Findings: the patina server (claude-opus-5-5)

Reviewed on Claude Opus 5.5 (`claude-opus-5-5`). Diff: `git diff origin/main...HEAD` in `branches/milestone/M-28`, limited to `src/Patina.Server/` and `tests/Patina.Server.Tests/`. No tests were run: every finding below rests on the code as quoted.

### A save to another folder breaks the graph's graph nodes, after the save is reported as a success

- **Where:** `src/Patina.Server/FileTools.cs:125-129` (save), `src/Patina.Server/GraphStore.cs:20` and `:47-51` (`Folder`, `Saved`).
- **Severity:** Major
- **Rubric:** 1, correctness (and 2, tests cover the Scope)
- **What is wrong:**
  - Save validates the graph against the store's current `Folder`. It then writes the file, and only then calls `store.Saved(target)`. `Saved` puts the graph back into the target's folder (`Placed`), so `graph` nodes now resolve against the new folder.
  - The worked case: a never-saved graph using `bricks.texgraph` from the tool folder, then `save {"path":"materials/wall.texgraph"}`. Save answers `saved ...: no faults`. But the file now names `materials/bricks.texgraph`, which does not exist.
  - The open graph is faulted from then on. swatch, export and check refuse it, and `open` of the file just saved reports the fault. Save's description says "a saved graph always evaluates" (line 104), and here it does not.
  - Nothing resolves the nested paths against the target folder before writing. The path is not rewritten either, and save does not refuse.
  - `push_snapshot`/`pop_snapshot` share the cause: `Pop` re-places a snapshot into the current folder.
  - `NestedGraphToolTests` saves only into the folder it already resolves against (lines 64 and 79), so no test would catch this.

**Outcome:** fixed

**Fix:** (T-176, claude-opus-5-5) save judges the graph in the target's folder (`graph with { Folder = <target's folder> }`), so a graph node not found from there is a fault and the save is refused, naming that folder; nothing is written and the open graph is unchanged. Paths are not rewritten: that is save's existing rule (a saved graph evaluates) applied where the file goes. pop_snapshot now reports the restored graph as it is placed in the current folder, so a snapshot pushed before a save into another folder comes back with its faults listed, not as it was when pushed. Both descriptions say so. Tests: `NestedGraphToolTests.Save_into_another_folder_is_judged_there_and_refused_when_a_graph_node_would_not_resolve` and `Pop_snapshot_after_a_save_into_another_folder_lists_the_faults_the_snapshot_has_there`.

### A sheet of more than 15 outputs throws instead of drawing or refusing

- **Where:** `src/Patina.Server/SwatchRendering.cs:185`, `src/Patina.Server/SwatchImages.cs:186` (`extents[row]`) and `:126-148` (`LabelAtlas.Size` 1024, `Rows` 32), `src/Patina.Server/SwatchTool.cs:190,212`.
- **Severity:** Minor
- **Rubric:** 1, correctness (labels past the atlas)
- **What is wrong:**
  - `SwatchSheet.Labels` gives 1 + 2n + 1 labels for n columns. The frame thread measures and draws only `row < LabelAtlas.Rows` (32), so `Extents` holds at most 32 entries.
  - With 16 or more outputs, `Compose` calls `Label(32, ...)`, and `extents[32]` throws `ArgumentOutOfRangeException`. The agent gets the SDK's generic tool error, not a refusal naming the limit.
  - Nothing caps the outputs: six roles plus any number of named outputs, and named outputs are the masks M-30 will layer with. The atlas comment ("15 columns' two labels") knows the limit, but no code path enforces it or falls back.
  - No test covers a sheet of more than 7 columns.

**Outcome:** fixed

**Fix:** (T-176, claude-opus-5-5) as the person ruled: the label atlas grows a page at a time. Page 0 (rows 0 to 31) is drawn in the sphere's frame as before; each further page of 32 rows is drawn in a frame of its own, and the pages stacked are the atlas, so `Compose` reads row r at r x 32 unchanged (`LabelAtlas.Pages`). A sheet of 15 outputs or fewer draws exactly what it drew. Tests: `SwatchTests.A_sheet_of_more_than_15_outputs_reads_its_labels_from_a_second_atlas_page`, and scenario-patina's `WideSheetStage` (18 columns drawn through the engine, the second page's labels found).

### export refuses `metallic` even when `roughness` is named too, against its own description

- **Where:** `src/Patina.Server/ExportTool.cs:125-126` (and the description, line 50).
- **Severity:** Minor
- **Rubric:** 1, correctness (refusal wording names the right thing)
- **What is wrong:**
  - The description lists "metallic named alone" among the refusals. The loop refuses any `metallic` in `outputs`, so `{"outputs":["roughness","metallic"]}` is refused with "so name roughness to write it", although roughness was named.
  - Either the refusal should fire only when roughness is absent (the description's rule), or the description and message should say that metallic is never named.

**Outcome:** fixed

**Fix:** (T-176, claude-opus-5-5) the description's rule. metallic is refused only when roughness is not named too; named beside it, metallic is written in roughness.png. Test: `MetalPlateTests` (both orders write the same roughness.png and only it).

### check on files reads the blue channel of any roughness PNG as metallic

- **Where:** `src/Patina.Graphs/Checks/CheckSet.cs:114-118` (as `CheckTool.TryFiles` reaches it, `src/Patina.Server/CheckTool.cs:223`), `src/Patina.Server/CheckTool.cs:131-137`.
- **Severity:** Minor
- **Rubric:** 1, correctness (the metallic-in-blue reading)
- **What is wrong:**
  - A roughness file with no metallic file always gets a derived `metallic` from B. That is right for export's packed image.
  - A plain greyscale roughness PNG (R = G = B, as most other tools write one) has B equal to its roughness. It is then judged as a half-metal: `metallic-not-binary` fires, and `base-colour-range` holds base-colour texels under rough areas to the metal floor of 120. Nothing tells R = G = B apart from a packed image.
  - Even for an export with no metallic, the report adds a `metallic` entry to `stats.outputs` that the agent never gave, plus a "metal texels: 0%" note.
  - The description does say "read as export writes it", but the report names a problem the file does not have. The only way out is giving a separate metallic file.
  - `CheckToolTests` never exercises this reading through the tool; only `Patina.Graphs.Tests` touches `OfPngs`.

**Outcome:** fixed (T-174, claude-opus-5-5), by the person's ruling (2026-11-21, replacing T-141's): role `roughness` is roughness only; role `metallic_roughness` is the packed glTF image, its G the roughness and its B the metallic, and is how check's description and schema name export's `roughness.png`. Giving both is an error. `Only_a_metallic_roughness_file_has_its_blue_channel_read_as_metallic` (through the tool) and two `CheckRulesTests` hold it.

### swatch, export and check word a refused override's value wrongly

- **Where:** `src/Patina.Server/ParameterOverrides.cs:41`
- **Severity:** Nit
- **Rubric:** 1, correctness (fault wording)
- **What is wrong:**
  - `WhyNot(...).Replace(" is ", ": is ")` replaces every " is ", not just the first. `swatch {"parameters":{"mortar":true}}` therefore returns `swatch: parameters['mortar']: is true or false; a value: is a number, a string, or an array of numbers or of strings`.
  - A mixed array gives `parameters['mortar']: an array: is all numbers or all strings`. T-171's As built gives the intended words.
  - No test covers either override path. The tested `edit_graph` path does not go through the `Replace`.

**Outcome:** fixed

**Fix:** (T-176, claude-opus-5-5) the colon goes only before a leading " is ", giving `parameters['mortar']: is true or false; a value is a number, ...` and `parameters['mortar']: an array is all numbers or all strings`. Test: `ExposedParameterToolTests.An_override_value_patina_cannot_read_is_worded_with_one_colon` (swatch, export and check).

### check on files reports one `stats.size` for files of different sizes

- **Where:** `src/Patina.Server/CheckTool.cs:93`
- **Severity:** Nit
- **Rubric:** 1, correctness (report shape)
- **What is wrong:**
  - `pixels` is the first file's width, so `stats.size` is wrong for every other file when the files differ in size (and for a non-square PNG). The code knows this case happens: it writes a "different sizes" note at line 130.
  - The agreed shape has one `size`. With files, either use the size shared by all of them, or name each file's size in its stats.

**Outcome:** fixed (T-174, claude-opus-5-5): `stats.size` is given when every output has one square size; otherwise it is left out and each output's stats name its `width` and `height`, as the description says. `Files_of_different_sizes_name_each_ones_size_and_files_of_one_size_give_stats_size`.

### check's "not a PNG" error has no test

- **Where:** `src/Patina.Server/CheckTool.cs:211-220`, `tests/Patina.Server.Tests/CheckToolTests.cs:59-68`
- **Severity:** Nit
- **Rubric:** 2, tests cover the Scope
- **What is wrong:**
  - The description lists "a file that is missing or not a PNG" among check's errors. The tests cover missing, twice-named, size and unknown-argument errors, but not an unreadable file.
  - So the `catch (InvalidOperationException or IOException or UnauthorizedAccessException)` is never shown to catch what StbImageSharp throws on a corrupt or non-image file.
  - `ImageResult.FromMemory` also accepts JPEG, BMP and others, so "not a PNG" is not what is checked.

**Outcome:** fixed (T-174, claude-opus-5-5): a file must start with the PNG signature (StbImageSharp reads JPEG and BMP too), and `A_file_that_is_not_a_png_or_is_a_broken_one_is_an_error` covers text, a BMP header and a PNG cut short (the last showing the catch holds what StbImageSharp throws).

# M-28 review, part E: the scenarios and test configuration, and the whole diff

Reviewer: claude-opus-5-5. Read in `D:\Repos\carve\branches\milestone\M-28` (`git diff origin/main...HEAD`); no code changed, no tests run.

## Findings: the scenarios and test configuration (claude-opus-5-5)

### The metallic break cannot fail
- **Where:** `scenarios/Carve.Scenario.Patina/SwatchStage.cs:400` (with `MetallicReads`, a few lines below `BricksAsync`).
- **Severity:** Minor
- **Rubric:** Breaks: each one actually exercised and caught.
- **What is wrong:** The break is `checks.Check(!MetallicReads(lit[0], lit[0]), ...)`. `MetallicReads(d, m)` is `d.Median - m.Median >= MetallicStep && m.Clipped > d.Clipped`. With the same tuple on both sides that is `0 >= 40 && x > x`, so it is false for every frame and the break passes whatever patina draws. It shows only that the rule's arithmetic is not vacuous. It never shows the rule catching a wrong frame, such as metallic dropped from the material. Self-comparison (`lit[1]` against `lit[1]`) would be constant-false in the same way. A meaningful break must judge one frame against a different, wrong prediction, for example the two frames swapped, `MetallicReads(lit[1], lit[0])`.

**Outcome:** fixed

### The painted-metal sphere threshold was fitted to the frame it judges
- **Where:** `scenarios/Carve.Scenario.Patina/LibrarySphere.cs:21-25` (`MetalDrop = 0.34`, `MetalMin = 0.62`). Used in `LibraryStage.cs` `SampleAsync` (`least = ... MetalMin`) and in the break "painted-metal sphere predicted without metallic".
- **Severity:** Minor
- **Rubric:** Weak or circular predictions.
- **What is wrong:** T-142's As built gives the figures on this same sample's frames: r 0.661 with metallic in the prediction, 0.571 without. `MetalMin` 0.62 was then set between the two. So the check, and the break that says metallic is "weighed in", both rest on a line drawn through the output being checked. The margin is 0.04 on one side and 0.05 on the other. `MetalDrop` 0.34 is also patina's own measured drop (T-134), not something derived independently. The other samples' rule (0.6 against 0.896 to 0.994, with the mirrored texels at -0.31 to 0.05) separates clearly. This one does not, so the painted-metal check and its metal break show little beyond "this frame is the one measured". T-142 discloses the thin margin, but it is not ruled as acceptable.

**Outcome:** no change needed: the person accepted `MetalMin` 0.62 and `CorrelationMin` 0.6 after T-142 (2026-11-20, in T-142's Notes, "Decided after landing"), with the thin margin disclosed. No sharper measure was looked for.

### Two scenario frames are captured with no pixel judged, and one has changed unexplained
- **Where:** `scenarios/Carve.Scenario.Patina/SwatchTiling.cs:113-118` (`swatch-sheet-grid-tiling-3`, judged only by `sheet.Width > N`). `NestedGraphStage.cs:36,39` (`nested-old-wall-sheet`, `nested-grime-sheet`, "for the person to judge").
- **Severity:** Minor
- **Rubric:** Weak predictions.
- **What is wrong:** The comment at SwatchTiling.cs:112 says "The sheet's sphere cell wears the tiling". Nothing checks this: the sphere cell is never compared with `frames[(0f, 3)]`, as `LibraryStage.CheckSheet` and the plate's sheet compare theirs. So a sheet that ignored the tiling would pass. T-172's As built records that this exact frame, `frames/swatch-sheet-grid-tiling-3.png`, differs from T-140's job J-20261120-085227-39ade3. It puts the change down to "work landed after that job", but the reason it gives (the cube rows) explains only the tsv. A frame that changed with nothing judging it is the case a check would have explained. The nested stage's two sheets are checked nowhere in the `base` suite. The library stage checks old-wall and grime only at tiling 3, in another suite.

**Outcome:** fixed

### CLAUDE.md's Patina scenario row leaves out the split, the nested stage and the cube
- **Where:** `CLAUDE.md:88` (the `scenarios/Carve.Scenario.Patina` row).
- **Severity:** Minor
- **Rubric:** CLAUDE.md's Layout and rules against the code.
- **What is wrong:** The row still says "suite `scenario-patina`" and "run-scenario.ps1 patina -Server patina". It does not mention the following:
  - `Patina:Stages` or `PatinaStages.For`. Nor the three suites `scenario-patina` (`base`), `scenario-patina-library-1` and `scenario-patina-library-2` (tests.json:623-687, T-172). A reader running the row's command by hand now gets all stages, not what any suite runs.
  - `NestedGraphStage.cs` (T-140), which is a stage in `PatinaStages.All`.
  - `SwatchCubeCheck.cs` and the cube view's checks and breaks (T-157), which run inside `SwatchTiling.RunAsync`.

  The `src/Patina.Server` row (`CLAUDE.md:73`) also describes swatch as "flat, tiled 3 x 3 and the sphere", with no `cube` (`SwatchTool.Views` has it).

**Outcome:** fixed

### CLAUDE.md's Patina.SampleGraphs row omits the variants and half the samples
- **Where:** `CLAUDE.md:96`; `tools/Patina.SampleGraphs/Program.cs:12-15,36-40`.
- **Severity:** Nit
- **Rubric:** CLAUDE.md's Layout against the code.
- **What is wrong:** The row says the tool writes `samples/graphs/<name>/<role>.png`. It names the tests that keep planks, bricks, fabric and floor-tiles. The code also writes `samples/graphs/<name>/variants/<variant>/` (the bricks' `grey`, through `GraphParameters.WithOverrides`), and names each file by `OutputRoles.FileOf`, so metallic gets none of its own. The row is also silent on cobblestones, painted-metal, old-wall and grime, and on the tests that keep them: `CobblestonesGraphTests`, `PaintedMetalTests`, `NestedSamplesTests`, and `ExposedParametersTests` for the variant.

**Outcome:** fixed

## Findings: the whole diff (claude-opus-5-5)

### CLAUDE.md says edit_graph's description is left to T-171; T-170 took it back and the test holds it
- **Where:** `CLAUDE.md:73` ("every description is held at 3,686 or less by `NodeToolTests`, edit_graph left to T-171"). Against it: `tests/Patina.Server.Tests/NodeToolTests.cs:22-31`, and `docs/tasks/T-170-tool-descriptions-an-agent-can-read-whole.md:47,82`.
- **Severity:** Minor
- **Rubric:** Tracker docs and CLAUDE.md contradicting the code; a leftover from the hand-offs.
- **What is wrong:** `Every_description_is_under_the_limit` checks every tool from `tools/list`, edit_graph included. T-170's Notes say "edit_graph falls to T-170" after T-171 closed, and its As built trimmed edit_graph from 4,950 characters. The CLAUDE.md clause is the first agent's state, from before that hand-off.

**Outcome:** fixed

### T-155's line in the changelog, the milestone and its own description claims the 10 s it did not meet
- **Where:** `docs/changelog/UNRELEASED.md` (the T-155 line); `docs/milestones/M-28-a-node-library.md:171`; `docs/tasks/T-155-separable-passes-and-rotated-read-locality.md:3`.
- **Severity:** Minor
- **Rubric:** Tracker docs that contradict the code they describe.
- **What is wrong:** All three say "so blur and distance at 8192 meet 10 s within the (memory) cap". T-155's own Notes (lines 67-69) and the milestone retrospective (line 232, "The 10 s targets for blur and distance at 8192 were not met"; blur 120 s) say otherwise, and T-159 (off-milestone) now owns the target. The changelog is what ships. In the same way, the T-172 changelog line repeats the goal's "by smaller textures ..., otherwise by splitting" rather than what was done (the split).

**Outcome:** fixed

### T-172's Scope says the committed swatch.png sheets are compared byte for byte; nothing compares them
- **Where:** `docs/tasks/T-172-scenario-patina-back-under-its-time-limit.md` Scope ("the committed `samples/graphs/*/*.png` and `swatch.png` sheets stay 256 and are still compared byte for byte"). Against it: `scenarios/Carve.Scenario.Patina/LibraryStage.cs:139` (`.Where(f => f != "swatch.png")`), and T-142's Scope and As built ("not compared by the scenario").
- **Severity:** Nit
- **Rubric:** Tracker docs that contradict the code.
- **What is wrong:** No scenario or test reads `samples/graphs/*/swatch.png`; a grep of `tests`, `scenarios` and `tools` finds only the exclusion. The claim is T-172's and is wrong, though nothing is lost by it.

**Outcome:** fixed

## Summary

Round 1 of M-28's review, after the rebase onto `origin/main` (f0841ff, M-24 and release 0.16.0) and the refactor pass. The diff (181 files, about 14,000 lines) was split by area, since members overlap in the same subsystems, with one reviewer also reading the whole diff for what falls between the parts. 36 findings: 2 Major, 20 Minor, 14 Nit. Reviewers B and D ran no tests; C proved its findings with scripts against the built library; A and E argued from the code. What each reviewer did not read is in its report to the orchestrator; the whole-diff pass checked tool and node lists, thresholds and parameter schemas, not interfaces changed from both sides across the engine, format and server.
