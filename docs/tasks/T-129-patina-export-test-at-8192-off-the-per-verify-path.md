---
description: >-
  T-115's 8192 bounded-memory export test (about 150 s) moves out of patina-regression into a heavy-tier suite, so it stops running on every patina verify
---
# T-129 — Patina export test at 8192 off the per-verify path

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

`patina-regression` runs on every verify that touches patina, and most of its time is one test: T-115's
`ExportBoundedMemoryTests`, which exports the planks at 8192 (about 150 s, 1565 MB peak). Move it to a heavy-tier
(T3) suite with triggers on what can change its result, so the per-verify suite is seconds again.

## Scope

- A new suite in `tests.json` for the 8192 test, in the heavy tier, with `triggers` on `src/Patina.Graphs/**` and
  `src/Patina.Server/ExportTool.cs` (see the cairn skill's `references/testing.md` for tiers and triggers).
- `patina-regression` keeps the stdio test and loses the 8192 one; its `est-seconds` back to what it measures.
- The memory bound (2 GB) and the recorded numbers are unchanged; no check is loosened.

## Notes

- Raised by M-27's retrospective test report (2026-11-18): `patina-regression` 1130 wall-seconds over its runs in
  the milestone's span, almost all the 8192 export.
- Interacts with T-130: a faster evaluator shortens the test but does not make it a per-verify test.
