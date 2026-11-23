---
description: >-
  Texture graphs evaluate an output in strips of tile rows, one thread and one tile cache a strip, so an 8192 export uses every core and its bytes stay the same
---
# M-40 — Parallel graph evaluation

**Status:** Planned

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

## Required tasks

Not planned yet.

## Retrospective
