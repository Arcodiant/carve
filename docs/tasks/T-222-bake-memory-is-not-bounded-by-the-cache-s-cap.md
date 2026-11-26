---
description: >-
  Bake memory is not bounded by the cache's cap
---
# T-222 — Bake memory is not bounded by the cache's cap

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

The memory graph bakes hold is bounded by the cache's 512 MB cap, across the undo history too, and the cap counts what
is held.

## Scope

## Notes

- **From RV-82** (the review of M-29, 2026-11-25), the finding "Bake memory is held outside the 512 MB cap: every resolved model in the undo history keeps its own copies of its bakes": every `Resolve` copies each slot's PNG into the
  resolved model, those copies stay alive for every model in the 100-step undo history and are not counted, and the
  byte count includes decoded bytes the cache never holds. Nothing drawn is wrong.
- **Filed off-milestone by the person's choice (2026-11-25).** Declined: fixing it in M-29 if small, and fixing it
  in M-29 whatever it takes.
- No test evicts from the cache today (M-29's retrospective, "Never verified").
