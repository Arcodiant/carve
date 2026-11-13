---
description: >-
  Third review of M-5
---
# RV-33 — Third review of M-5

**Status:** Open

**Reviews:** M-5

**Round:** 3

**Previous:** RV-32

**Diff:** 3cfc2d227eafec38285fb23d34685472464eb71c against 7d957b4

**Reviewer:** one reviewer, claude-opus-5-5: RV-32's answers and the fix diff 458ca57..387113c

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-32's answers and the fix diff (claude-opus-5-5)

5 of the 7 answers hold. Two do not hold in full: the `loop_cut` fix refuses ordinary cuts across quads that are not axis-aligned, and the Retrospective's new round-2 paragraph claims a break for a fix that had none. There are also four Nits.

### `loop_cut` now refuses ordinary cuts across any quad that is not axis-aligned, as "crosses its edge"

**Severity:** Major

`src/Carve.Core/LoopCut.cs:139-160` (`Across`, called at `:125`), new in 094714e. This concerns RV-32 "FaceSplit still silently skips a cut whose midpoint is on the boundary, and `loop_cut` still hands it one". The concave quad is now refused as the person's ruling asks. But the new "crosses its edge" test runs `PlaneGeometry.Crosses` (strict signs, no tolerance) against all four edges of the quad, and that includes the entry and exit edges, where the cut's own ends lie. For `p` on entry edge r-s, `Orient(r, s, p)` is zero only in exact arithmetic. Once it is projected through `Basis(n)`, it is rounding noise of either sign. Meanwhile `Orient(p, q, r) * Orient(p, q, s) < 0` always holds, because r and s are on opposite sides of the cut. So `Crosses` comes out true about half the time. On axis-aligned quads (the cube, the boxes, the scenarios) the noise is exactly 0. That is why all tests pass and all 187 frames stay identical. I ran a probe (`C:\Users\andre\AppData\Local\Temp\rv33\probe`) against Core built from 387113c:
- `LoopCut.Cut` on a single planar 2 x 1 rectangle rotated by the quaternion (0.3, 0.5, 0.2, 0.8), edge (0, 1):
  - at 0.5 it gives `edge: the cut at 0.5 across faces[0] crosses its edge [2, 3]; loop_cut cuts only across a quad`;
  - 1/3 is refused the same way;
  - 0.25, 0.3 and 0.7 succeed.
- 2000 randomly rotated rectangles at random fractions: 1438 refused.
- The unit cube after `subdivide` 1: 34 of 36 loop cuts (12 faces' first edges at 0.3, 0.5, 1/3) are refused. After `subdivide` 2: 36 of 36.

The same probe against Core built from 458ca57 (outside the worktree) refuses none of these, and a direct `FaceSplit.Split` of the rotated rectangle at 0.3 splits it. So the fix turns ordinary loop cuts on any tilted or subdivided surface into refusals that name a crossing that is not there. The crossing test should leave out the entry and exit edges, or use the tolerance as `Overlaps` does. No test has a loop cut on a quad that is not axis-aligned.

**Outcome:** —

### The Retrospective's round-2 paragraph says every fix had a break, but the load-test fix had none

**Severity:** Minor

`docs/milestones/M-5-cut.md:185`, new in fa2d1ed: "An Opus fixer fixed five, each with a break". `artifacts/review/RV-32/breaks.log` item 4 (the load test, 944cb9e) says "No break: this removes an assert". Four of the five fixes have a break. This is the overclaim RV-32 found in round 1's paragraph ("each with a break"), and fa2d1ed corrected it there, naming round 1's five text fixes with no break. The new round-2 sentence repeats it. So the answer to RV-32's Retrospective finding holds for round 1's text, but the Retrospective still claims more than was done. The rest of the round-2 paragraph and the round-1 corrections check out against RV-31, RV-32 and their artifacts:
- 28 of 31 answers held;
- 7 findings, none Major;
- 6 Majors covering five defects, with `subdivide` rated Major twice and Minor once;
- A 20, B 9, orchestrator 1;
- three and two fixer agents.

**Outcome:** —

### `loop_cut`'s "passes through its corner" refusal cannot be reached, yet T-66's As built quotes it

**Severity:** Nit

`src/Carve.Core/LoopCut.cs:149-151` and `docs/tasks/T-66-loop-cut.md:83` (new in 094714e). In a quad, every corner is an end of the entry edge or the exit edge. Say a cut passes through corner c, more than the tolerance from its ends p and q. Then the part from c to the end on c's edge lies along that edge, and the `Overlaps` test before it returns "runs along its edge" first. For example, the RV-32 dart from edge (0, 1) at 1/3 passes through corner 2, and it is reported as `runs along its edge [2, 3]`. The branch is dead. As built nevertheless gives `passes through its corner 2` as a message `loop_cut` produces, and `LoopCut`'s remarks (`:25-27`) list it. No test reaches it.

**Outcome:** —

### `loop_cut`'s description does not name the new refusal

**Severity:** Nit

`src/Carve.Server/LoopCutTool.cs:23`. The "Bad input" list names a pair that is not an edge, no quad, a Mobius band and a bad `at`. It doesn't mention the refusal 094714e adds: a cut that runs along or crosses an edge of a concave quad. An agent reading the description is not told that a ring through a concave quad can be refused. (The list also leaves out the non-manifold and self-crossing ring refusals, from before this round.)

**Outcome:** —

### `AlongBoundary` keeps its old midpoint summary above the new one

**Severity:** Nit

`src/Carve.Core/FaceSplit.cs:336-339`, new in 094714e. The new `<summary>` ("lies wholly along a loop ... A segment only partly along the boundary is not") was added below the old one ("its midpoint within tolerance of a boundary edge") instead of replacing it. So the method now carries two summaries, and the first describes the midpoint test that RV-32 had removed.

**Outcome:** —

### The x-mirrored +y tile gap that RV-32 raised is not recorded with the -y known limit

**Severity:** Nit

`docs/milestones/M-5-cut.md:199`. This concerns RV-32 "The -y axis tile still has no check that its camera can fail". The finding also said "The same holds for an x-mirrored +y": the roof is symmetric in x, so `FromAbove` and the shade probes pass a mirrored +y. The person's ruling and the Never-verified entry cover only the -y tile, and say "only a swap with +y is caught". That answer holds for -y. But the +y mirror gap the finding named is neither answered nor recorded.

**Outcome:** —

## Summary

Round 3, one reviewer, checking RV-32's 7 answers and the fix diff `458ca57..387113c` at `387113c`. 5 of the 7 answers hold. There are 6 findings: 1 Major, 1 Minor and 4 Nit.

- **Major:** round 2's `loop_cut` fix tests every edge with an exact sign and no tolerance, so it refuses ordinary cuts across any quad that isn't axis-aligned. Every test and frame used axis-aligned quads, so none caught it.
- **Minor:** the Retrospective overclaimed breaks again. RV-32 raised the same thing, so it went to the person, who chose to fix it and run round 4.
