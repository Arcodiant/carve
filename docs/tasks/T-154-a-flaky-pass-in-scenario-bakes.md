---
description: >-
  Find why one scenario-bakes run during M-24 failed and then passed on a retry, and make the suite repeatable or say why it is not
---
# T-154 — A flaky pass in scenario-bakes

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** scenario-bakes

## Goal

`scenario-bakes` passes or fails the same way on the same inputs, as every scenario run must (fixed clock, seed and
size; bakes byte-identical from run to run).

## Scope

- Find the run `cairn test report` counts as a flaky pass of `scenario-bakes` in M-24's window (2026-11-18 to
  2026-11-20; 18 runs, 1 flaky pass, 1 that could not run). Read its log and name the check that failed first.
- If it is a timing limit (the occlusion bake measured 7.4 to 8.8 s in Debug against M-24's 10 s budget, and
  T-124 moved `bake`'s worked example to 512 after the harness's 30 s answer window ran out under load), say so and
  put the fix to the person: a limit is theirs to move.
- If it is a pixel or a bake that differs between runs, that is a determinism bug: find it and fix it, with a test that
  would have caught it.
- If the log cannot be found, say so here and close it.

## Notes

- Filed from M-24's retrospective, from `cairn test report --since 2d` (2026-11-20). The report holds no more than
  the counts: 1035 passes' logs could not be read on the machine that ran the report.
- **Stop and ask** before moving any limit or tolerance.
