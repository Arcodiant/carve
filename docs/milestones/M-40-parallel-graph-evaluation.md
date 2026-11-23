---
description: >-
  Texture graphs evaluate an output in strips of tile rows, one thread and one tile cache a strip, so an 8192 export uses every core and its bytes stay the same
---
# M-40 — Parallel graph evaluation

**Status:** Planned

**Depends on:** M-29, M-33

**One-liner:** *Texture graphs evaluate an output in strips of tile rows, one thread and one tile cache a strip, so an 8192 export uses every core and its bytes stay the same*

## What this milestone delivers

`Patina.Graphs` evaluates on one thread. At 8192 that is minutes for a sample such as the cobblestones, in a Release
build. This milestone splits an output into horizontal strips of tile rows and evaluates each strip on its own thread,
with its own tile cache. No texel's arithmetic changes, so every output's bytes are what they are today.

- `GraphEvaluator.Evaluate` takes a range of tile rows, and a caller above it runs the strips in parallel and gathers
  the tiles.
- `export`, `swatch`, `check` and the sample tool use it, as does Carve wherever it evaluates a graph.
- A cache budget across the strips, chosen so an 8192 export stays inside the 2 GB bound
  (`ExportBoundedMemoryTests`).
- `export`'s description and the 8192 figures measured again.

Content: every sample graph's committed PNGs, unchanged byte for byte, and the samples timed at 8192 before and after.

## Estimated size

About **5 tasks**:

1. The row range on `Evaluate`, with tests that any split into strips gives the bytes of the whole.
2. The parallel driver: how many strips, the cache each gets, and the peak memory measured at 8192.
3. `export`, `swatch`, `check` and the sample tool moved onto it.
4. Carve's graph evaluation (M-29) moved onto it, if M-29 has landed.
5. The numbers: every sample at 8192, Release and Debug, and the descriptions that quote a time.

## Why

Measured on 2026-11-22 against M-28's evaluator at `57b562e` (a Release build, 16 logical cores, .NET 10.0.15), with a
four-line patch to a copy of the evaluator that limits `Evaluate` to a range of tile rows. Every variant gave the same
bytes as one thread did. No PNG is encoded in these figures.

| | One thread, as today | 16 strips, 64 MiB each | Speedup | Tiles made |
|---|---|---|---|---|
| planks, 2048 | 1.6 s | 0.36 s | 4.3x | +22% |
| bricks, 2048 | 4.8 s | 0.88 s | 5.4x | +11% |
| cobblestones, 2048 | 24.0 s | 4.3 s | 5.6x | +41% |
| painted-metal, 2048 | 25.5 s | 7.7 s | 3.3x | +113% |
| planks, 8192 | 26.6 s | 4.9 s | 5.4x | +5% |
| cobblestones, 8192 | 451 s | 46 s | 9.8x | +1% |

- A strip remakes the tiles its neighbours also need, so thin strips waste more: at 2048 a strip of 16 is one tile row.
  More strips than cores was slower (64 strips at 8192: planks 4.3x, cobblestones 8.5x).
- A node that reads far from its tile gains least: painted-metal's blur made twice the tiles.
- The cache thrashes on the cobblestones at 8192. One thread with a 1 GiB cache took 242 s and made 29% fewer tiles;
  16 strips with 256 MiB each took 38 s (11.8x). At 2048, and on the planks at 8192, a larger cache changed little.
- 16 MiB a strip was about as fast as 64 MiB on the planks, bricks and cobblestones at 2048 and the planks at 8192, and
  slower on painted-metal (2.8x) and the cobblestones at 8192 (8.1x).
- One evaluator per output, each on its own thread and otherwise unchanged, gave 1.5 to 2.7x at 2048 and 1.9x at 8192.
  The slowest output sets its time.
- Each figure is one run. The one-thread times at 2048 were about twice those of a run an hour earlier, and the
  cobblestones at 8192 took 350 s in that run; that was not explained. Speedups are within one run.
- Peak memory was not measured per variant. Sixteen caches of 64 MiB are 1 GiB beside the 256 MB image, and sixteen
  of 256 MiB could reach 4 GB.

The benchmarks, the patch and the results are kept in `artifacts/eval-bench/` (ignored): `results.tsv` for the
outputs in parallel, and `strips/` for the strips (`evaluator-row-range.patch`, `samples-2048.tsv`,
`planks-8192.tsv`, `cobblestones-8192.tsv`).

## Open at planning

Every item here is answered under *Decided at planning* below. Shared work between outputs and the cobblestones'
thrash are not fixed by this milestone; they moved to T-130.

- **Whether the evaluator may be parallel at all.** T-130's Notes and T-133 both make a parallel evaluator a stop and
  ask, as a change to determinism, and `GraphEvaluator`'s own summary promises one thread in a fixed order. Strips
  keep each texel's arithmetic and gave the same bytes in every run here, but the promise changes. The person's.
- **What becomes of [T-130](../tasks/T-130-faster-texture-graph-evaluation-at-8192.md).** It is this milestone's goal
  by another route (faster work on one thread), was held for T-133, which has landed, and still names `main`'s band
  evaluator. It could join this milestone, come first, or be closed into it.
- **The cache budget.** A fixed cap a strip, or one budget divided by the strips, and whether the 2 GB bound stays.
  The cobblestones want more cache at 8192 than the bound allows sixteen strips.
- **How many strips.** The core count makes the split depend on the machine. The bytes do not depend on it, but the
  time and memory do, and a test that holds either would need a fixed count.
- **Whether outputs share work.** Each output of a graph is evaluated from nothing, though they read the same nodes.
  Not looked into; it may be worth more than threads for a graph with many outputs.
- **The thrash on the cobblestones at 8192**, whether or not anything is parallel: 29% of the tiles made on one thread
  are remade.
- **Order against [M-33](M-33-fast-png-encoding.md).** They touch different code and either can land first. In a
  Release build the encode is the larger part of the planks' export and evaluation the larger part of the
  cobblestones'.

**Decided at planning (2026-11-23).**

By the person, each put with a recommendation:

- **The evaluator may be parallel, in strips of tile rows.** The promise changes from one thread in a fixed order to
  the same bytes at any split and on any machine, held by tests at fixed splits. A node type is safe to call from
  several threads at once; this becomes an architecture rule in `CLAUDE.md` (T-191).
- **T-130 is re-scoped, off-milestone, to what threads do not fix:** work shared between a graph's outputs, the tiles
  the cobblestones remake, and one thread's own speed. It starts with a probe after this milestone lands. Its file
  was rewritten in this plan. Declined: closing it, and bringing shared outputs or one-thread speed-ups into M-40.
- **M-33 lands first.** Both milestones change `ExportTool.cs`, the sentence in `export`'s description that quotes
  the 8192 time, the README's "What is slow or bounded", T-115's record and `patina-regression`'s `est-seconds`.
  M-33 was started on 2026-11-22.
- **M-40 depends on M-29,** so that carve's graph bakes are moved in this milestone (T-194). The recommendation was
  to drop the carve task and leave a note in M-29; the person chose the dependency.
- **One budget of 512 MiB of tile cache for an output, divided among its strips, never under 64 MiB a strip,** so at
  most 8 strips. **The 2 GB bound stays** on all six 8192 tests. Declined: 1 GiB with the bound kept (measured within
  0.1 to 0.4 GB of it before the encoder), and 1 GiB with the bound raised to 3 GB.
- **`System.GC.ConserveMemory=9` in patina and in carve,** in each server's project file. Without it the collector
  leaves the working set far above what is held (below). Declined: patina only, and no runtime setting.
- **Strips by cores, capped:** the least of the logical cores, the budget over 64 MiB and the tile rows; one strip
  for a size under 512, exactly as today. A graph whose one tile pins more than its share gets fewer strips, down to
  one. Tests name a fixed count through a parameter. No tool gains an argument. Declined: always 8, and a `threads`
  argument on `export`.

By the orchestrator, recorded in the tasks' Notes, each a stop if it proves wrong:

- `Evaluate` takes `rowStart` and `rowEnd` in tile rows and returns its `EvaluationRun` (T-191).
- The driver is `StripEvaluation` with `StripOptions`. Strip 0 starts alone on the calling thread, the driver reads
  what its first output tile pinned, settles the strip count, and only then starts the others, each on a thread of
  its own, not the pool. With several strips, only one at a time may hold pinned tiles past its cap (a shared gate),
  so the tiles held are at most the budget and one strip's excess. One strip keeps today's 64 MiB cap and cache
  rule. The first exception stops the other strips and comes out as itself (T-192).
- `OutputImage` takes the driver by an optional parameter, so no caller changes until its own task (T-192).
- The one-thread Debug figures are not measured for every sample; the Debug before is T-189's planks (T-195).
- Nothing here draws a new frame, so no layout was sketched for approval and no frame review is planned. Each task
  that could move a frame holds every hash against a run of its base commit.

**The split, for the record:**

```
an 8192 output: 64 tile rows of 128 texels, 8 strips, a 512 MiB budget

 row  0 +--------------------------------+  strip 0  calling thread   cache 64 MiB   starts alone; after its first
        |                                |                                           tile the driver reads what it
 row  8 +--------------------------------+  strip 1  patina-strip-1   cache 64 MiB   pinned and settles the count,
        |                                |                                           then starts strips 1 to 7
 row 16 +--------------------------------+  strip 2  patina-strip-2   cache 64 MiB
        |              ...               |    ...
 row 56 +--------------------------------+  strip 7  patina-strip-7   cache 64 MiB
        |                                |
 row 64 +--------------------------------+

 each strip: its own bands of 32 tile columns, walked down its own rows, as one thread does today.
 a strip remakes the input tiles its neighbours also need (a blur reads about 10 tile rows past each edge).
 onTile is called from every strip's thread, once a tile, into the caller's one image.
 a strong minify (one tile pins 772 MiB, more than the budget): one strip, rows 0 to 64, as today.
```

**Probed at planning** (2026-11-22 and 23, `main` at `8f9f945` with the row range patched into a copy, Release, 16
logical cores, 64 GB, .NET 10.0.15, one run each, no PNG encoded; harness and tables in `artifacts/m40-probe/`,
ignored). Every variant gave the bytes of one thread.

| At 8192 | One thread, 64 MiB | 8 strips x 64 MiB, `ConserveMemory=9` | Peak working set | 16 strips x 64 MiB, default collector |
|---|---|---|---|---|
| planks | 38.1 s | 4.6 s | 1,262 MiB | 7.9 s, 2,234 MiB |
| cobblestones | 451 s (2026-11-22) | 59.7 s | 1,208 MiB | 49.5 to 66.9 s, 2,436 to 2,533 MiB |
| painted-metal | not measured | 97.2 s | 1,209 MiB | 125.2 s, 2,985 MiB |
| blur, smooth, radius 0.05 | 124.9 s | 44.2 s | 1,175 MiB | 63.5 to 65.1 s, 3,843 to 4,046 MiB |
| distance, range 0.1 | 19.8 s | 5.5 s | 883 MiB | 10.2 s, 1,664 MiB |
| a 30-degree turn | 17.2 s | 3.6 s | 871 MiB | 3.6 s, 1,421 MiB |
| warp, amount 0.25 | 23.7 s | 4.4 s | 884 MiB | not measured |
| strong minify | 18.8 s | not run: the pin rule gives it one strip | 1,167 MiB on one thread | not run |

- **A strip needs 64 MiB.** The blur at 16 strips of 16 MiB took 246 s, twice one thread, making 1.58 million tiles
  against 61 thousand. Painted-metal at 16 strips of 32 MiB was stopped by hand, unfinished, after 24 minutes.
- **The strong minify pins its whole input in every strip:** 772 MiB held on one thread, 1,544 MiB on two strips
  (1,906 MiB peak), and 21.4 s against 18.8 s. Hence the pin rule.
- **The collector.** With 16 strips of 64 MiB the blur held 1,209 MiB and peaked at 4,046 MiB; under
  `ConserveMemory=9` it peaked at 1,880 MiB in the same time, and the cobblestones at 1,835 MiB against 2,533 MiB. A
  1.75 GB heap limit did the same but slowed the blur by a fifth. Non-concurrent collection and a 64 MB gen 0 did
  nothing. At 8 strips the blur peaked at 2,687 MiB by default and 1,175 MiB with the setting. The project-file form
  (`RuntimeHostConfigurationOption`) was built and run: it writes the value into `runtimeconfig.json` and gave
  1,146 MiB.
- **8 strips were as fast as 16 on this machine** for the planks, the blur and distance, and a little slower for the
  cobblestones.
- **Small sizes** (three runs, the best): at 256, two strips against one ran 0.58x (bricks) to 1.70x (old-wall), on
  runs of 13 to 420 ms. At 512 with four strips, 1.17x to 3.00x. At 1024 with eight, 1.40x (painted-metal) to 5.21x.
- **Not checked:** another machine or core count; the PNG encoder's share of the peak (the tasks' own 8192 tests hold
  it); what `ConserveMemory` does to rendering in either server; anything of M-29's code, which was not on `main`;
  painted-metal on one thread at 8192; the driver itself, which does not exist yet (the probe ran a patched copy
  under `Parallel.For`, with no pin rule and no gate).

**Order:**

| Wave | Task | Model | Depends on | Owns (no sibling in its wave touches) |
|---|---|---|---|---|
| 1 | T-191 the row range, safe on threads | Opus | — | `GraphEvaluator.cs`, `TileCache.cs`, the node audit, `CLAUDE.md`'s architecture rule |
| 2 | T-192 the strip driver | Opus (two agents: build, measure) | T-191 | the new `StripEvaluation.cs`, `OutputImage.cs`, the evaluator's internals |
| 3 | T-193 patina's tools | Sonnet | T-192 | `src/Patina.Server`, `Checks/CheckSet.cs`, `tools/Patina.SampleGraphs`, patina's README section |
| 3 | T-194 carve's bakes | Opus or Sonnet, by T-163's As built | T-192 | `src/Carve.Graphs`, `src/Carve.Server`, carve's README section |
| 4 | T-195 the numbers | Sonnet (expect a hand-off) | T-193, T-194 | `ExportTool.Text`'s time sentence, the README's figures, T-115's record, `tests.json`'s `est-seconds` |

The milestone is started only once M-29 and M-33 are on `main`; every task reads its scope against what they left.
Content comes with its mechanic: each task holds every committed sample PNG byte for byte, T-192 lands with its
measured table at 8192, and T-193 and T-194 put the driver behind the tools an agent calls on a plain `patina` and
`carve`. T-193 and T-194 share `README.md` and `CLAUDE.md` only by lines added in different sections, settled by
keeping both sides. Nothing here adds sound, motion or a new frame, so there is no listening, real-time or frame
approval pass; the person's part is the stops each task names.

## Required tasks

- [ ] [T-191](../tasks/T-191-a-range-of-tile-rows-on-evaluate-safe-on-several-threads.md) — GraphEvaluator.Evaluate takes a range of tile rows and may be called from several threads at once; any split of an output into strips gives the bytes of the whole, and node types are held safe to call together
- [ ] [T-192](../tasks/T-192-the-strip-driver-an-output-evaluated-in-parallel-strips-under-one-cache-budget.md) — Patina.Graphs evaluates an output in strips of tile rows, a thread and a tile cache each, under one 512 MiB budget, on fewer strips when one tile pins more than its share, with the same bytes at every strip count
- [ ] [T-193](../tasks/T-193-patina-s-export-swatch-check-and-the-sample-tool-evaluate-in-strips.md) — patina's export, swatch, check and the sample tool evaluate in parallel strips, with System.GC.ConserveMemory set so an 8192 export stays under 2 GB, and every committed PNG and frame unchanged
- [ ] [T-194](../tasks/T-194-carve-s-graph-bakes-evaluate-in-strips.md) — carve bakes a graph material's slots in parallel strips, with its mesh nodes safe on several threads, baked times and the 5 s warning still right, and every frame unchanged
- [ ] [T-195](../tasks/T-195-the-8192-numbers-after-strips.md) — export's description, the README and T-115's record carry every sample's 8192 time and peak memory measured after strips, in Release and Debug, beside the one-thread figures

## Retrospective
