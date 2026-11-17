---
description: >-
  Review of M-7, round 4
---
# RV-50 — Review of M-7, round 4

**Status:** Answered

**Reviews:** M-7

**Round:** 4

**Previous:** RV-49

**Diff:** 54c3facfe59592a095406fa6291a5d3d59d16604 against 5bc4559

**Reviewer:** claude-opus-5-5 (one reviewer, documents only: RV-49's answers and the records)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-49's answers and the records (claude-opus-5-5)

### The round-2 run line's suite and test counts are not what its only record says

**Where:** `docs/milestones/M-7-materials-and-textures.md:277`, `docs/reviews/RV-49-review-of-m-7-round-3.md:48`, `:52`
**Rubric:** 7 Tracker hygiene
**Severity:** Nit

The new line says that at `3f9ba34` "all 12 frame and materials suites ran through the queue, and server-unit and server-regression (584 tests, including the new release test) were carried from the first fixer's runs on that same commit". The evidence README says `head.sha256` came from "the queued runs of the eleven suites at 3f9ba34", which are every frame suite except scenario-materials; RV-49's finding also says eleven, its Fix and the Retrospective say 12. The README gives 584 to the `Tier=regression` run alone, which is server-regression, but the line puts both server suites in front of "(584 tests ...)". The substance is not in doubt: the code at `3f9ba34` is identical to `3893cf6` and to HEAD, and the regression run is recorded.

**Outcome:** fixed

**Fix:** the line now attributes the 584 tests to server-regression's run alone. The 12 stands: the RV-48 fixer's report lists 12 suites queued and passing at `3f9ba34`, the 11 frame suites and scenario-materials. The README's eleven counts only the suites whose frames make `head.sha256`, and the line now says so. RV-49's finding text is the reviewer's own and is left as written (orchestrator)

## Answers checked

Both of RV-49's answers hold. Every short SHA cited in RV-47 and RV-48 is an ancestor of HEAD, except `3f9ba34`, which the text names as on `fix/RV-48`, with its code identical to the landed `3893cf6` (`git diff`, docs left out, empty both ways). The Fix attributions check out: `343eb56`, `2bf6e40`, `3893cf6` and `dbb7bb7`. The round-2 entry records the queue runs, server-regression at the head and at `307d692` (584 and 561), and the round-3 re-run (3 of 3). `3f9ba34` stays resolvable only while `origin/fix/RV-48` is kept.

## Summary

Round 4 of M-7's review, documents only: `git diff 9e79cd1..1e3fd4f` changed three documents and no code. One reviewer, claude-opus-5-5, read RV-47 to RV-49, the whole Retrospective, the As built of T-77 to T-81, and the evidence README and tables' line counts. It checked every SHA the four documents cite against the history, and the severity counts of RV-47 (3/16/6) and RV-48 (0/3/4). Both answers hold, and there is one records-only Nit. It returned its findings in its report, which the orchestrator copied in. The person chose to fix the Nit and run a short round 5.
