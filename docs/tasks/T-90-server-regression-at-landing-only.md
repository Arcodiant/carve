---
description: >-
  A planning rule leaves server-regression out of Verify lines, open items follow it, and the suite estimates are measured again
---
# T-90 — Server regression at landing only

**Status:** Done

**Pri:** P2

**Milestone:** M-20

**Batch:** — (standalone)

**Depends on:** T-88, T-89

**Verify:** server-unit

## Goal

`server-regression` runs before work lands on `main`, as cairn's tiers intend, rather than at every task verify.

## Scope

- Add a rule to CLAUDE.md's *Build, test and verify*: a task or bug's `**Verify:**` names `server-regression`
  only when it changes something the suite alone covers, which is the stdio transport, extension loading, the
  debug extension or rendering. Tool behaviour is covered by `server-unit`, and the milestone's landing on `main`
  runs `server-regression` anyway.
- Bring the open items not yet started into line: drop `server-regression` from their `**Verify:**` where the rule
  says so, and leave items already in progress to their owners. Use `cairn list`, not grep, to find them.
- Measure both server suites again through `cairn test run` and set `est-seconds` in `tests.json` from the
  results.
- Record the before and after in the M-20 retrospective: `cairn test report` slot-seconds per week, and the
  per-run time of each server suite.

## Notes

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5).

- **Rule:** a paragraph in CLAUDE.md's *Build, test and verify*: `**Verify:**` names `server-regression` only for changes to the stdio transport, extension loading, the debug extension or rendering; tool behaviour is covered by `server-unit`; the milestone's landing on `main` runs `server-regression` anyway. The Layout row for `tests/Carve.Server.Tests` now says unit tests run on `ToolHarness` and the regression tests cover stdio, extension loading, the debug extension, rendering and `ToolFamiliesThroughProcessTests`.
- **Open items** (`cairn list task --status Open`, `cairn list bug --status Open`): T-71, T-73, T-86, B-4, B-5. None has a `**Verify:**` line naming `server-regression` (T-71, T-86, B-4 and B-5 have no Verify line; T-73 mentions the suite only as `tests.json` inputs to edit, not as its verify), so no item changed and none was kept by doubt. Items In Progress were left alone.
- **Measured** (`cairn test run <suite> --fresh`, to measure): server-unit PASS in 24.0 s; server-regression PASS in 337.8 s. `est-seconds` set to 24 (was 12) and 338 (was 260).
- **Before** (M-20 opening section): about 17,300 slot-seconds a week, 524 cases in about 7 minutes, est-seconds 260. **After** (this run): server-regression 228 tests per T-89 (about 5 m 38 s here, 337.8 s), server-unit about 751 tests in 24 s.
- **`cairn test report --since 28d`** (4.0 weeks, so it does not yet reflect the change): server-regression 61 runs/wk, 96,194.7 slot-s in the window, 24,048.7 slot-s/wk, carried 20%, 2 flaky; server-unit 56.75 runs/wk, 2,889.3 slot-s, 722.3 slot-s/wk, carried 27%, 0 flaky. These are the before-the-rule figures; the weekly figure falls only as tasks stop running server-regression in verify.

- **Correction (2026-11-17, RV-59):** the survey above missed four items. M-21's T-94, T-103, T-104 and T-108 were Open and named `server-regression` in `**Verify:**` (M-21's plan, 40d3794, was in history at T-90's commit). T-94, T-103 and T-108 keep it, since they render. T-104 changes `GlbExport.cs` output read back through the engine's loader, which the amended rule (CLAUDE.md, *Build, test and verify*) also names, so it keeps `server-regression` too (3282eab had dropped it). Why the query missed them cannot be told from the record: `cairn list task --status Open` returns all four today, each with Status Open, so the query itself does not hide milestone members; the survey must have read a shorter list than the query gives. The next sweep should read every Open item's `**Verify:**` from the files, not from a list copied by hand.
