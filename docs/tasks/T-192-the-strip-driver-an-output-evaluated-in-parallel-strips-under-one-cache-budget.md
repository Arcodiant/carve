---
description: >-
  Patina.Graphs evaluates an output in strips of tile rows, a thread and a tile cache each, under one 512 MiB budget, on fewer strips when one tile pins more than its share, with the same bytes at every strip count
---
# T-192 — The strip driver: an output evaluated in parallel strips under one cache budget

**Status:** Open

**Pri:** P1

**Milestone:** M-40

**Batch:** — (standalone)

**Depends on:** T-191

**Verify:** patina-unit

## Goal

`Patina.Graphs` has a driver that evaluates one output in horizontal strips of tile rows, each strip on a thread of
its own with a tile cache of its own, all under one budget. Its bytes are those of one thread at every strip count,
no graph runs slower than it does on one thread, and what it holds is bounded by construction. `OutputImage` can use
it. No tool calls it yet: T-193 and T-194 move the callers.

## Scope

- **Read T-191's `### As built` first**, for `Evaluate`'s signature and `EvaluationRun`'s fields.
- **A new file, `src/Patina.Graphs/Evaluation/StripEvaluation.cs`:**
  - `public sealed record StripOptions(int? Strips = null, long? BudgetBytes = null, int? TileSize = null)`.
  - `public static class StripEvaluation` with `DefaultBudgetBytes` (512 MiB), `LeastStripBytes` (64 MiB,
    `GraphEvaluator.DefaultCacheBytes`), `LeastSize` (512), and
    `EvaluationRun Evaluate(GraphEvaluator evaluator, string output, int size, Action<Tile> onTile, StripOptions? options = null, CancellationToken cancellation = default)`.
  - `EvaluationRun` gains `Strips` (1 from `GraphEvaluator.Evaluate`).
- **How many strips, decided at planning:**
  - `Strips` not named: one strip for a `size` under 512; otherwise the least of `Environment.ProcessorCount`, the
    budget divided by 64 MiB, and the output's tile rows, and never under one. A public pure function gives this
    number from the size, the tile size, the budget and a processor count, so a test can name each.
  - `Strips` named: the least of it and the tile rows. This is for tests and for T-195's one-thread figures.
  - Strip `s` of `n` holds tile rows `rows * s / n` up to `rows * (s + 1) / n`.
- **The budget, decided at planning:**
  - A strip's cache cap is the budget divided by the strips.
  - **One strip is today's evaluator, cap and all:** with one strip and no `BudgetBytes` named the cap is
    `GraphEvaluator.DefaultCacheBytes`, and the cache keeps its rule of today (it trims to the cap or to the most it
    ever pinned, whichever is more).
  - **The pin rule.** Strip 0 starts alone, on the calling thread. After its first output tile the driver reads what
    that tile pinned at once (`p`). If `p` is more than a strip's cap, the strips become the most that fit,
    `budget / p` and at least one, and the cap becomes the budget divided by them. Strip 0 is told which row to stop
    before and goes on with the cache it has; only then are the other strips started. So a strong minify, whose every
    tile pins its whole input (772 MiB at 8192), runs on one strip exactly as it does today, and makes its input once.
    This needs an internal way for a running `Evaluate` to report after its first output tile and be given its last
    row; add it to `GraphEvaluator` as an internal member, not a public one.
  - **The gate.** With more than one strip, only one strip at a time may hold pinned tiles past its cap. A strip
    takes a shared gate before its pinned bytes pass its cap and gives it back once they are under it again, and a
    cache under the gate trims to its cap, not to the most it pinned. A strip waiting for the gate holds no more
    than its cap. So the tiles held are at most the budget and one strip's excess over its cap, whatever a later
    tile reads (a `warp` reads where its offsets point).
  - Each cache keeps its own waiting arrays and scratch as today (`TileCache.FreeLimit`); `EvaluationRun.HeldBytes`
    counts them.
- **Threads.** Strip 0 runs on the calling thread and each other strip on a `Thread` of its own (background, named
  `patina-strip-<n>`), joined before `Evaluate` returns, whatever happened. Not the thread pool: an export of minutes
  must not take the host's pool threads (`ExportToolTests.Other_tools_answer_while_an_export_runs`).
- **`onTile`** is called from several threads at once, once for each canonical tile of the output. The summary says
  so, and that a callback writing only its tile's texels (the packers, `CheckSet`'s copy) needs no lock.
- **Failure and cancellation.** `cancellation` is checked before each output tile in every strip. The first exception
  from any strip, a node's or `onTile`'s, stops the other strips before their next output tile and is thrown again
  from `Evaluate` as itself, with its own type and stack (not an `AggregateException`); later ones are dropped. A
  cancelled call throws `OperationCanceledException`.
- **The run it returns:** `Strips` as run; `TilesEvaluated`, `MostTiles`, `PeakBytes` and `HeldBytes` summed over the
  strips (the sum of the peaks, an upper bound on what was held at once); `PinnedBytes` the largest strip's;
  `CapBytes` the budget.
- **`OutputImage`** (`Rgba8`, `FileRgba8`, `Make`, both overloads of each): a last optional parameter
  `StripOptions? strips = null`. With it null they evaluate on one thread through `GraphEvaluator.Evaluate` exactly
  as today, so no caller changes in this task. With it given they go through `StripEvaluation`; a named `tileSize` or
  `cacheBytes` beside it is refused, since `StripOptions` carries both.
- **Tests, a new `tests/Patina.Graphs.Tests/StripEvaluationTests.cs`** (`unit`):
  - **The same bytes at every strip count.** Every committed sample, every output, at 256 in tiles of 32 with 1, 2,
    3, 5 and 8 strips named, each equal to `GraphEvaluator.Evaluate`'s image and to the committed PNG. Two samples at
    512 and 1024 with no count named, equal to one strip. The far-reading, dependent and separable test graphs of
    T-191's `RowRangeTests` with 3 and 8 strips.
  - **Each tile once** across the strips, and `OutputImage` with `strips` equal to `OutputImage` without.
  - **The strip count's table:** 256 gives 1; 512 on 16 processors gives 4; 8192 on 16 gives 8; 8192 on 4 gives 4;
    8192 with a 128 MiB budget gives 2; a budget under 64 MiB gives 1; one processor gives 1.
  - **The pin rule:** a minify whose tile pins more than its share (small tiles and a small budget, so it is quick)
    runs on fewer strips than asked, down to one, with the same bytes, and on one strip makes exactly the tiles a
    plain `Evaluate` makes.
  - **The gate:** a test node type that reads the whole image for one tile in a lower strip and one texel elsewhere,
    in a registry of its own: the bytes are the same, `HeldBytes` stays under the budget and one excess and the
    waiting arrays, and the run ends (a test timeout catches a deadlock).
  - **Failure:** `onTile` throwing in a middle strip, a node throwing, and a cancelled token each come out as their
    own type; the other strips deliver no more than one further tile each; no `patina-strip-` thread is alive
    afterwards.
  - **A table of broken copies**, each failing a test and named in the As built: strips overlapping by a row, a strip
    left out, the pin rule off (the minify makes its input once a strip), the gate off (held passes the bound), an
    exception swallowed, a strip started before strip 0's first tile.
- **Measured at 8192, by a second agent once the tests pass** (see Notes). A Release build, the harness in
  `artifacts/M-40/T-192/` under the container's `branches/main` (outside the repo's tracked files; no product flag),
  one process a run so each peak is its own, with `DOTNET_GCConserveMemory=9` and without:
  - the five graphs of `TransformDistanceBoundedMemoryTests` and `warped.texgraph`, on one strip and on the default;
  - the planks, the cobblestones and painted-metal on the default (their one-thread times are T-195's);
  - each run's time, strips, tiles made, `HeldBytes` and peak working set, in a table in the As built beside
    planning's (`artifacts/m40-probe/*.tsv`).
  - `artifacts/m40-probe/probe/Program.cs` is planning's harness and `README.txt` beside it says how it was run. It
    drives a patched copy with `Parallel.For`; this one calls `StripEvaluation`.
- `CLAUDE.md`'s Layout row for `src/Patina.Graphs`: the `Evaluation/` part names the driver, its budget and its two
  rules. `README.md` is not touched (nothing an agent can reach changes); say so in the As built.

## Notes

- **Design-heavy, and it decides an API T-193 and T-194 build on.** Plan two agents: one builds the driver and its
  tests, one measures. A long measurement costs a hand-off, so the first writes down how to run the harness.
- **Decided at planning by the person (2026-11-23):** one budget of 512 MiB for an output, divided among its strips,
  never under 64 MiB a strip, so at most 8; strips by cores, capped by the budget and the tile rows, one strip under
  512; a graph whose tile pins more than its share gets fewer strips, down to one; the 2 GB bound stays; no tool gains
  an argument.
- **Decided by the orchestrator, a stop if it proves wrong:** the names; strip 0 measuring the pins and going on;
  the gate; dedicated threads; one strip keeping today's 64 MiB cap; `OutputImage`'s opt-in parameter.
- **What planning measured** (2026-11-22 and 23, `artifacts/m40-probe/`, Release, a patched copy, no encoder):
  - 8 strips of 64 MiB under `ConserveMemory=9`: planks 4.6 s and 1,262 MiB; cobblestones 60 s and 1,208 MiB;
    painted-metal 97 s and 1,209 MiB; blur 44 s and 1,175 MiB; distance 5.5 s; the turn 3.6 s; warp at 0.25 4.4 s
    and 884 MiB. One thread: planks 38 s, blur 125 s, distance 20 s, the turn 17 s, warp 24 s, the minify 19 s.
  - A share under 64 MiB thrashes: blur at 16 MiB a strip took 246 s, and painted-metal at 32 MiB was stopped
    unfinished after 24 minutes.
  - The minify on two plain strips held 1,544 MiB and took 21 s against 19 s: the pin rule's reason.
  - Without `ConserveMemory` the blur at 8 strips peaked at 2,687 MiB while holding 604 MiB. The setting is T-193's
    and T-194's; here it is an environment variable of the measuring run.
- **Goes back to the person, not into the code:** bytes that differ at any strip count; a graph more than 10% slower
  on the default than on one strip; `HeldBytes` past the budget and one excess; a peak working set over 1.5 GB with
  `ConserveMemory=9` (the encoder needs the rest of the 2 GB); any wish to change the budget, the floor or the
  strip rule.
- [T-159](T-159-strip-passes-for-separable-nodes.md) (strip passes for separable nodes) changes the same evaluator
  and is not part of this milestone. If it has landed, read this scope against it.
- **Write down in `### As built` for T-193 and T-194:** the signatures; the measured table; how `onTile` must behave;
  what a caller passes to get the default.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.
