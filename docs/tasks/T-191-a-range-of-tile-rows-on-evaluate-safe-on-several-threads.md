---
description: >-
  GraphEvaluator.Evaluate takes a range of tile rows and may be called from several threads at once; any split of an output into strips gives the bytes of the whole, and node types are held safe to call together
---
# T-191 — A range of tile rows on Evaluate, safe on several threads

**Status:** Open

**Pri:** P1

**Milestone:** M-40

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

`GraphEvaluator.Evaluate` can make any range of an output's tile rows, and several calls may run at once on one
evaluator. Tests hold that every split of an output into strips gives the bytes of the whole. Nothing runs in
parallel in the product yet: T-192 builds the driver on this.

## Scope

- **Read first:** `src/Patina.Graphs/Evaluation/GraphEvaluator.cs` and `TileCache.cs` as they are on `main` when this
  starts. M-29 and M-33 land before this milestone, so read this scope against what they left.
- **The row range.** Both `Evaluate` overloads gain `int rowStart = 0, int? rowEnd = null` after `cacheBytes`: the
  output's canonical tile rows `rowStart` up to but not including `rowEnd` (default: every row).
  - The bands are walked as today, over those rows only. `onTile` is called once for each canonical tile in the range
    and for no other. Tiles of inputs outside the range are still made where a tile inside reads them.
  - Refused with `ArgumentOutOfRangeException` naming the parameter: `rowStart` under 0, `rowEnd` over the output's
    tile rows, `rowStart >= rowEnd`.
  - The planning probe's copy (`artifacts/m40-probe/Patina.Graphs/Evaluation/GraphEvaluator.cs` under the container's
    `branches/main`, ignored by git, so read it by its absolute path) makes this change in four lines.
- **Safe on several threads.**
  - `Evaluate` returns its `EvaluationRun` (it returns nothing today), and the record gains `HeldBytes` (what
    `LastHeldBytes` reports) and `PinnedBytes` (the most bytes of tiles pinned at once, `TileCache.PeakPinnedBytes`).
    A call keeps all its working state in itself. `LastRun` and `LastHeldBytes` stay, set when a call ends, and their
    summaries say that with calls running at once they are the last to finish, so a caller of several reads the
    returned run.
  - **Every node type is audited** for state that two calls could share: each file in `src/Patina.Graphs/Nodes/`,
    each mesh node M-29 added in `src/Carve.Graphs`, and the test node types. A static table that is only read is
    fine. A cache filled on first use, a field written in `Evaluate`, `EvaluateRows`, `EvaluateColumns`, `InputRect`
    or `InputRects`, or a bake made lazily on first read is not: make it safe (made before evaluation, or behind a
    lock that cannot change a value) and say which in the As built. The planning read found none in
    `Patina.Graphs/Nodes/`; M-29's nodes were not on `main` to read.
  - `NodeType`'s summary gains the contract: its methods are called from several threads at once and hold no state
    that changes.
- **The promise, reworded.** `GraphEvaluator`'s summary: the Determinism paragraph no longer says single-threaded. It
  says a tile's texels depend on neither the tile size, the cap, the rows asked for, nor the thread, and that one call
  walks its rows in a fixed order. `CLAUDE.md`'s architecture rule for `Patina.Graphs` gains: an output's bytes do not
  depend on how its rows are split or on the thread that makes them, and a node type is safe to call from several
  threads at once. Its Layout row's `Evaluation/` part names the row range.
- **Tests, in `tests/Patina.Graphs.Tests`** (`unit`), a new `RowRangeTests.cs`:
  - **Any split gives the bytes of the whole.** For each committed sample graph, every output, at 256 in tiles of 32
    (8 tile rows): the rows one at a time, in two halves, in three uneven strips (1, 5, 2), and bottom strip first, each
    packed into one image and compared byte for byte with a whole evaluation and with the committed PNG. The same at
    cap 0 and at the default cap.
  - The same splits for the test node types that read far (`rotate`), dependent inputs (`pair`, `warp_static` and
    `warp`), the separable nodes at their largest radius and range (`blur`, `distance`), a `transform` turned 30
    degrees and one minified, and a named port (`tiles.mask`).
  - **Each tile once.** For each split, the rectangles `onTile` was given are exactly the canonical tiles of the
    range, each once.
  - **On real threads.** Eight strips of one evaluator, each on a thread of its own, started together behind a
    barrier, 20 rounds, for the cobblestones and painted-metal at 256 in tiles of 32: every round equals the whole.
    Run it for the mesh nodes too, in M-29's `graphs-unit` suite if `Carve.Graphs` has one (add that suite to this
    file's `**Verify:**` line in the same commit).
  - **The refusals** above, and that `Evaluate` returns the run it also leaves in `LastRun`.
  - **A table of broken copies**, each failing a test and named in the As built: the range ignored (every row made), a
    strip's first row's reads clamped to the range (reads above it not made), `onTile` called for a tile outside the
    range, and a node type that keeps a field between calls (a test node type written for this, in a registry of its
    own) caught by the threads test.
- Every existing test passes unchanged. No committed PNG changes.
- Nothing a user or an agent can reach changes, so `README.md` is not touched. Say so in the As built.

## Notes

- **Design-heavy, and it decides an API T-192, T-193 and T-194 build on.**
- **Decided at planning by the person (2026-11-23):** the evaluator may be parallel, in strips of tile rows; the
  promise becomes the same bytes at any split and on any machine, held by tests at fixed splits.
- **Decided by the orchestrator, a stop if it proves wrong:** `Evaluate` returns the run and `EvaluationRun` gains the
  two fields; the range is in tile rows, not texel rows; the parameter names.
- **Goes back to the person, not into the code:** a split whose bytes differ from the whole (do not adjust the test);
  a node type that cannot be made safe without changing a value it gives; any existing test that needs its target
  changed.
- This task does not start a thread in `src/`. The threads are in its tests.
- **Write down in `### As built` for T-192:** the exact signatures; `EvaluationRun`'s fields; what the audit found in
  each node file, M-29's by name; how long the threads test takes.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.
