---
description: >-
  Re-review of M-17
---
# RV-30 — Re-review of M-17

**Status:** Answered

**Reviews:** M-17

**Round:** 2

**Previous:** [RV-29](RV-29-review-of-m-17.md)

**Diff:** f713200661e8787efcf408feb588d3cfa4e428e0 against origin/main

**Reviewer:** claude-opus-5-5 (one reviewer, the RV-29 fix range)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the RV-29 fixes (claude-opus-5-5)

No findings.

Checked, finding by finding, against the fix range `3ac2fb4..HEAD` (fdf17f2, 3bc670e, 3a15bc4, 3a8f45e, f4cec67, 5a377ae, 8e89b67 with its revert 0e0aa94 and re-revert 859ff2a, ec42db3), with `dotnet test tests/Carve.Server.Tests --filter RenderPump` run (20 passed). Every RV-29 outcome holds:

- **Render #1 / whole #1, the stuck pump (fdf17f2).**
  - A stale bone shows the mesh shaded without setting `_meshFault`.
  - A missing-bone weights request fails alone, checked against `_shownModel` (`RenderPump.cs:94`).
  - The only new state is `_shownModel`, so nothing leaks between renders.
  - Renders without `weights` take the old path.
  - The test fails on the old code, and on half the fix.
- **Render #2, the mirror and flip checks (ec42db3).**
  - ChainBones' flip point is the true y-flip of the rest framing.
  - ChainWeights' guard points land on background when mirrored or flipped.
  - The red checks are now labelled as corner order.
- **Weights #1 / whole #3, the worked example (3a15bc4).** The test runs `set_weights` first and compares the full result with the description's `Result:` line.
- **Weights #2, `weigh` with `bones` (f4cec67).** The name-to-index mapping is exercised, with counts that a null or off-by-one mapping would change. The no-skeleton and empty-list errors add no undo step.
- **Weights #3, the leaf length and the tie (5a377ae).** Both mutations, `* 1.0` and `<=`, fail the new tests.
- **Weights #4, `Math.Pow` (8e89b67, restored by 859ff2a on the person's ruling of 2026-11-11).** `FourthPower` is right, and no committed expectation holds weighed output.
- **Weights #5 / whole #2, the Rig inputs (3bc670e).** The unused link is dropped, and the inputs match the three linked files.
- **Whole #4, `render`'s description (3a8f45e).** `weigh` is listed, and both places say "set_weights or weigh".

Not run by the reviewer: the scenario suites, Core tests and GetWeights tests (read only). `cairn milestone M-17 verify` runs them.

## Summary

Round 2 is one reviewer checking RV-29's answers and the fix range, since the fixes were small and targeted. It found nothing, and every round-1 outcome holds.
