---
description: >-
  Review of M-27, round 3
---
# RV-70 — Review of M-27, round 3

**Status:** Answered

**Reviews:** M-27

**Round:** 3

**Previous:** RV-69

**Diff:** 289c51b7158ef5f8c0377d74a94061533a8e2297 against a494e1f

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-69's answers and the changes since round 2 (claude-opus-5-5)

I read `git diff 306d862..HEAD` (code and docs) against the tree and ran `Patina.Graphs.Tests` (98 pass) and `Patina.Server.Tests` `Tier!=regression` (101 pass, planks and `grain_2` counts unchanged). I judged whether the new tests fail without the fix by reasoning from the code. Without the fix, the early `return` drops the mask fault, and the edit is accepted because the `b` fault stays at the same place. I checked every hash cited in T-111, T-128 and RV-68's Fix lines with `merge-base --is-ancestor`. I did not re-run `scenario-patina`.
- The ruled kind check skips every input of a node on a cycle: holds (the person's rule is now kept on a cycle: off-cycle inputs are checked and the node's outputs get no kind)
- T-128's and T-111's As built name hashes no ref holds: holds
- The scenario's 1024 check never applies the rule to roughness: holds (roughness is exported alone at 1024, held to `Tolerance` 4 and to the half-tile shift, u-mirror and flat map)
- ExportMatch's summary cites a one-stave shift fault `Faults` does not make: holds for `ExportMatch.cs`, but the same claim is left in the docs (below)

### T-115 and RV-68 still say the base colour catches a one-stave shift, which nothing makes

**Where:** `docs/tasks/T-115-png-export-at-a-given-size.md:86`; `docs/reviews/RV-68-review-of-m-27.md:266`

**What:** 5ed77cb removed the one-stave shift from `ExportMatch`'s summaries. T-115's As built still ends "A half-tile or one-stave shift is invisible in the normal ...; the base colour catches those", and RV-68's Fix line says "the base colour catches it". `ExportMatch.Faults` makes no one-stave shift for any role, and no test in `tests/` or `scenarios/` does either.

**Failure scenario:** This is RV-69's round-2 Nit, one file over. A reader of T-115 takes the one-stave shift as checked on the base colour. A later loosening of `Tolerance` that still fails the three faults that are made would leave that claim unshown.

**Severity:** Nit

**Outcome:** fixed

**Fix:** both lines now say the base colour is shown catching the half-tile shift and that no check makes a one-stave shift (a research probe measured it at 35, untested). Docs only, so the reviewed diff is unchanged.

## Summary

One reviewer, Opus 5.5, checked RV-69's four answers against the code and read the whole diff since round 2 (`306d862..HEAD`). All four answers hold. One Nit, in the docs only: T-115's As built and an RV-68 Fix line still claimed a one-stave shift is caught, which no check makes; both corrected by the orchestrator (claude-opus-5-5). No code changed after this round.
