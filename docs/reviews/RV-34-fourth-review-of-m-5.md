---
description: >-
  Fourth review of M-5
---
# RV-34 — Fourth review of M-5

**Status:** Answered

**Reviews:** M-5

**Round:** 4

**Previous:** RV-33

**Diff:** 9ae43a372f921f67bf14578e1f07c527e642934f against 7d957b4

**Reviewer:** one reviewer, claude-opus-5-5: RV-33's answers and the fix diff 30ae748..06e0779, plus PlaneCut under rotation

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-33's answers and the fix diff (claude-opus-5-5)

All 6 of RV-33's answers hold. The `loop_cut` fix brings in one new defect: a real crossing is refused in `FaceSplit`'s words. There is also one Retrospective Nit.

### `loop_cut`'s new tolerance skip passes real crossings to `FaceSplit`, which refuses them as `cuts[0]`

**Severity:** Minor

`src/Carve.Core/LoopCut.cs:158`, new in 38a3d87. This is new; it comes from the fix to RV-33's Major. The skip leaves out *any* edge that either end of the cut lies within `tol` of, not only the entry and exit edges. On a concave quad, the end of the cut can be within `tol` of the side edge that the cut really crosses. That happens near the reflex arm's corner, or anywhere along a thin arm. The crossing is then never tested, so the cut goes to `FaceSplit`, which refuses it in its own words. This breaks the person's ruling (2026-11-13, RV-32) that `loop_cut` refuses such a cut "in its own facts-only words ... never `cuts[k]`". The message also names the segment 4-5, and the person's mesh has no vertices 4 or 5. At 30ae748 every one of these cases was refused as `crosses its edge [1, 2]`. The geometry is never wrong and nothing is skipped silently: only the words are.

I ran a probe outside the worktree (`%TEMP%\rv34\probe`), with Core built from 06e0779 and from 30ae748, on axis-aligned quads at z = 0, entry edge (0, 1). I checked every case against an exact rational test of whether the cut crosses an edge:
- RV-32's own dart `(0,0) (4,0) (1,1) (0,4)` at 0.9996 and 0.9997 gives `cuts[0]: the segment 4-5 runs outside faces[0]`. 0.9995 still gives `crosses its edge [1, 2]`. The dart under 300 random rotations at 0.9996 gives `cuts[0]` in all 300.
- On `(0,0) (10,0) (1,0.01) (0,10)` (the arm is 0.01 thick, 7 times `tol`), 258 of 1999 fractions give `cuts[0]`, for example 0.9. At 30ae748, 0.9 gave `crosses its edge [1, 2]`.
- On `(0,0) (10,0) (1,0.002) (0,10)`, 1276 of 1999 give `cuts[0]`, including 0.5.
- On `(0,0) (10,0) (1,0.5) (0,10)`, which is not thin, 0.9975 to 0.9997 give `cuts[0]`.

In every sweep, a cut the exact test calls crossing is refused, and every cut that succeeds really is across: there is no silent wrong split. `LoopCut`'s remarks (`:25-27`) and T-66's As built still say such a cut is refused naming the fraction, the face and the edge. No test has a concave quad cut near its reflex arm. One possible approach: skip only the entry and exit edges, by vertex id, as `FaceSplit.Pieces` does, and keep the tolerance for those two.

**Outcome:** skipped: filed as B-4 (off-milestone, Minor) by the person's ruling of 2026-11-13; no cut is made wrongly, only the refusal's wording is wrong; the failing inputs are in B-4

### The Retrospective says a +y tile with a 180-degree wrong up "is caught", but RV-32 says it was never shown failing

**Severity:** Nit

`docs/milestones/M-5-cut.md:204` (Never verified), new in 4c6cacf. This concerns RV-33 "The x-mirrored +y tile gap that RV-32 raised is not recorded". The gap is now recorded, so that answer holds. The added clause says "a swap with -y and a 180-degree wrong up are caught (RV-32, recorded in RV-33)". RV-32's finding (`docs/reviews/RV-32-re-review-of-m-5.md:85` and `:94`) reasons that `FromAbove` would fail a wrong up, but says "Only the swap was shown failing in the scenario. K4 breaks the Core test's camera, not the product's." So the 180-degree case is the reviewer's reading, not a check shown failing. It belongs under Never verified, but not stated as caught.

**Outcome:** fixed

In fcadaa8: the line now says only the swap with -y was shown failing, and that the wrong-up case was the reviewer's reasoning.

## Summary

Round 4: one reviewer, checking RV-33's 6 answers and the fix diff `30ae748..06e0779` at `06e0779`. All 6 answers hold. `PlaneCut` was probed on a cube and on a cube subdivided twice, under the identity and 200 random rotations, and showed no exact-sign defect.

There are 2 findings: 0 Major, 1 Minor and 1 Nit. The Minor is `loop_cut`'s refusal wording on nearly degenerate concave quads. No cut is made wrongly; only the refusal names `cuts[0]`. The person ruled to file it as a bug and finish.
