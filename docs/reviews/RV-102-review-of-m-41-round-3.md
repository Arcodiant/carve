---
description: >-
  Review of M-41, round 3
---
# RV-102 — Review of M-41, round 3

**Status:** Answered

**Reviews:** M-41

**Round:** 3

**Previous:** RV-101

**Diff:** 14dc4194301a6860f94d8148d151bd20da0891d7 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: round 2's answers and the fixes (claude-opus-5-5)

### `The_40_by_0_01_strip_that_would_fit_is_refused_at_once_by_the_margin` asserts a wall-clock limit that only adds flake risk

`tests/Carve.Core.Tests/TessellateTests.cs:265-267`. Rubric points 2 and 6.

The `unit` test asserts `watch.ElapsedMilliseconds < 500` around the first `Tessellate` call, which includes the JIT, GC and contention from parallel suites. B-14 tracks this kind of flake. The assertion catches nothing the others miss:

- **Without the gate,** the strip makes 196,325 vertices and the call succeeds, so `Assert.False(r.Succeeded)` fails.
- **With the hard stop firing instead,** the error reads "more than 200,000", which the test's `about 21[78],...` pattern fails.

To pin "refused at once" without timing, drop the stopwatch. If the "no work done" claim needs its own assert, call the internal overload with `stop` 0 and expect the "about" message, not "more than 0".

**Outcome:** —

## Summary

One reviewer, claude-opus-5-5, who wrote none of the code and reviewed no earlier round. It read `git diff ccb74ec3..HEAD -- . ':!docs'`. Its verdicts on RV-101's three answers:

1. **The prediction now counts narrow faces: holds.**
   - `Predicted` adds 3.5 x length / `max_edge` for every loop edge, holes included.
   - The hard stop fires in three places, after the edge points, per face in the lattice, and as `Refine`'s point cap, and each refuses before `mesh.Edit`.
   - `ModelStore.Edit` pushes only when `Succeeded`, so no partial edit reaches the store or the undo stack.
   - Mutations were caught: `StripPoints` 0 fails the prediction tests, and removing `Refine`'s cap fails the hard-stop test.
2. **`split` is short at summary: holds.** The 40-face test would fail on the old shape. `VerbosityToolTests` was tightened, and `EditToolDefinitionTests`' `split:[` check is unchanged.
3. **The plain refusal message: holds.** It is pinned by a test. The probe found no reachable case where the "too thin" advice would be wrong.

B-14 and B-15 are well formed and off-milestone, and `cairn lint` is clean. It ran 26 Core and 71 server tests. It did not run the full suites or scenarios, time an under-predicted call before the hard stop, check the margin on holed or concave faces, or trace B-15.
