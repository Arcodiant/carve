---
description: >-
  What threads do not fix in 8192 evaluation is probed and the worthwhile part built: work shared between a graph's outputs, the tiles the cobblestones remake, and the speed of one thread
---
# T-130 — Faster texture graph evaluation at 8192

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

After M-40, an output is evaluated on several threads. Three costs are left that threads do not touch. Find out
which of them is worth removing, and remove it, with every output's bytes unchanged.

## Scope

- **Do not start before M-40 has landed.** Start with a probe on `main` as it then is, and write what it finds into
  this file's Notes before anything is built. T-195's As built has the figures to begin from.
- **Outputs share no work.** Each output of a graph is evaluated from nothing, though they read the same nodes: the
  planks' four outputs, the cobblestones' five. Measure what share of an export's tiles two or more outputs both
  make, and what evaluating them together would cost in memory under M-40's budget.
- **The cobblestones remake their tiles.** At 8192 on one thread 29% of the tiles made were made again after being
  dropped (339,787 against 242,415 under a 1 GiB cache, 451 s against 242 s; M-40's *Why*). Under strips of 64 MiB
  they still are. Find which reads cause it (the `warp`'s scattered reads are the likely ones) and whether an order
  or a cache rule removes it inside the budget.
- **One thread's own speed.** Profile a strip. T-110's As built named bounds-checked per-texel reads; T-133 replaced
  bands with tiles, so measure again before assuming it.
- Whatever is built: every output byte-identical (the committed PNGs, `patina-unit`'s determinism and strip tests),
  the 2 GB bound and M-40's budget held, and the Release and Debug figures measured before and after.

## Notes

- **Re-scoped at M-40's planning (2026-11-23, the person's ruling):** this task was "the planks at 8192 in seconds an
  output" by faster work on one thread. M-40 reaches that by strips (planning measured the planks' four outputs at
  4.6 s on 8 strips against 38 s on one thread). It now holds what M-40 left open and does not fix: shared outputs,
  the thrash and per-thread speed. It stays off-milestone.
- Raised by M-27 (T-115's As built and the milestone retrospective, 2026-11-18). Held for M-28 on 2026-11-19 until
  T-133 landed; kept out of M-33 by the person on 2026-11-22.
- `Patina.Graphs` stays engine-free and deterministic: no `System.Random`, clock or `Math.Pow` (T-110, T-112). Node
  types stay safe to call from several threads at once (T-191).
- **Release figures, from M-33's planning** (2026-11-22, `main` at `d45ed52`, 16 cores, .NET 10.0.15): the planks'
  four outputs at 8192 evaluate in 19.6 s on one thread. The 154.7 s T-115 measured was a Debug build with the old
  encoder.
- Evaluating outputs together changes what one call holds and the order tiles are made in. It is a design change:
  put the probe's findings and a recommendation to the person before building it.
- [T-159](T-159-strip-passes-for-separable-nodes.md) owns blur's and distance's own cost; do not take it in here.
