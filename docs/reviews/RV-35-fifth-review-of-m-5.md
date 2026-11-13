---
description: >-
  Fifth review of M-5
---
# RV-35 — Fifth review of M-5

**Status:** Answered

**Reviews:** M-5

**Round:** 5

**Previous:** RV-34

**Diff:** 9ae43a372f921f67bf14578e1f07c527e642934f against 7d957b4

**Reviewer:** one reviewer, claude-opus-5-5: RV-34's answers, B-4 and the Retrospective (documents only; no code changed since RV-34)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-34's answers and the Retrospective (claude-opus-5-5)

Both answers hold: the +y line now says only the swap with -y was shown failing, and B-4 records RV-34's failing inputs, cause and the person's ruling (`cairn lint` is clean).

### The round-4 line calls B-4's failures "a few", on "nearly degenerate" quads

**Severity:** Nit

`docs/milestones/M-5-cut.md:193`. The line says RV-33's fix "lets a few real crossings on nearly degenerate concave quads reach `FaceSplit`". RV-34's Minor (`docs/reviews/RV-34-fourth-review-of-m-5.md:36-40`, copied into B-4) does not show a few. It shows 1276 of 1999 fractions on `(0,0) (10,0) (1,0.002) (0,10)`, including 0.5. It shows 258 of 1999 on the 0.01 arm, and all 300 random rotations of RV-32's dart at 0.9996. It also shows a failing range, 0.9975 to 0.9997, on `(0,0) (10,0) (1,0.5) (0,10)`, a quad RV-34 calls "not thin". RV-34 locates the defect as "near the reflex arm's corner, or anywhere along a thin arm", and B-4's Symptom says the same. The line understates how often the wording is wrong, and it narrows where. "No cut is made wrongly" holds.

**Outcome:** fixed

In 220129d.

### Never verified still says `PlaneCut` was not searched, but round 4 probed it for the exact-sign defect

**Severity:** Nit

`docs/milestones/M-5-cut.md:211`. The line says "`PlaneCut` and older code were not searched". That was true after round 3: `:190` says so of the fixer, and `artifacts/review/RV-33/breaks.log` 1b searched only the knife and `FaceSplit`. But RV-34's Summary says `PlaneCut` "was probed on a cube and on a cube subdivided twice, under the identity and 200 random rotations, and showed no exact-sign defect", and the Retrospective's own round-4 entry (`:192`) says so too. So Never verified contradicts the round-4 entry. What is still unverified is code older than round 1 other than `PlaneCut`, and `PlaneCut` beyond the reviewer's probe of a cube (no code search, no committed test).

**Outcome:** fixed

In 220129d.

## Summary

Round 5 checked documents only, with one reviewer: RV-34's two answers, B-4, and every claim the Retrospective makes about the review and fix rounds, read against the reviews, outcomes, break logs and frame compares. Both answers hold. It found 2 Nits in the Retrospective, both fixed.
