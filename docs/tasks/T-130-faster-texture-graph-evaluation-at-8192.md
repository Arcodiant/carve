---
description: >-
  Patina.Graphs evaluates the planks at 8192 in seconds per output, not the 155 s T-115 measured for four, keeping its bytes and memory bound
---
# T-130 — Faster texture graph evaluation at 8192

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

T-115 measured `export` of the planks at 8192, all four outputs, at 154.7 s (Debug build), against planning's "tens
of seconds". PNG encoding is about 10 s an output, so most of it is evaluation in `Patina.Graphs`. One 8192 call can
outlast a client's call timeout. Make evaluation fast enough that `export`'s description can say seconds again.

## Scope

- Profile evaluation at 8192 first and write what dominates into this file's Notes.
- Likely: nodes work on `Band.Row(y)` spans rather than bounds-checked per-texel `Band` methods (T-110's As built
  names this); avoid re-evaluating shared inputs per output where the band model allows.
- Every output stays byte-identical (the planks PNGs, `patina-unit`'s determinism tests); the memory bound holds.
- Measure Release and Debug, before and after, and update `export`'s description and T-115's numbers.

## Notes

- Raised by M-27 (T-115's As built and the milestone retrospective, 2026-11-18).
- `Patina.Graphs` stays engine-free and deterministic: no `System.Random`, clock or `Math.Pow` (T-110, T-112).
- A parallel evaluator is a design change to determinism; stop and ask before one.
- **Release figures, from M-33's planning** (2026-11-22, `main` at `d45ed52`, 16 cores, .NET 10.0.15): the planks'
  four outputs at 8192 evaluate in 19.6 s, and the old encoder took 24.2 s more. The 154.7 s above was a Debug build.
  M-33 replaces the encoder, so after it evaluation is nearly all of an 8192 export. The person kept this task out of
  M-33. Evaluation at 8192 is M-40's, and what becomes of this task is open there.
- **Held for M-28** (2026-11-19, the person's ruling at M-28's planning): T-133 replaces the band evaluator with tiled
  region-of-interest evaluation. Do not start this task until T-133 has landed, then re-scope it against the tiled
  evaluator and T-133's 8192 measurements.
