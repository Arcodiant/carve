---
description: >-
  Review of T-224, round 2: checking RV-97's answers
---
# RV-98 — Review of T-224, round 2: checking RV-97's answers

**Status:** Answered

**Reviews:** T-224

**Round:** 2

**Previous:** RV-97

**Diff:** 7676019a against origin/main

**Reviewer:** claude-opus-5-5 (one fresh agent)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### As built says a zero-length base gives (0, 0, 1), but it gives the detail

The task file's `normal_blend` bullet says "a zero-length base or result gives (0, 0, 1)". Since 7676019a, `Blend` (`src/Patina.Graphs/Nodes/NormalBlendNode.cs:31`) first makes the base unit, and `Unit` of a zero vector is (0, 0, 1), so the base is flat and the output is the detail: base (0, 0, 0), detail (0.6, 0, 0.8) gives (0.6, 0, 0.8). A reader expecting a texel whose base blends two opposite normals to zero to come out flat would be wrong, and might "fix" the code to match the doc. The four RV-97 fixes were each checked and hold (descriptions 18/18 under the limit; the short-base case and its broken copy worked by hand; the y strips counted; the bake test does what BakeTool does, quantisation and padding included).

**Outcome:** fixed

Fix: As built now says a zero-length base counts as flat and gives the detail; the zero-length result's (0, 0, 1) stays.

## Summary

One finding, a wrong sentence in As built, fixed in the task file; no code changed in this round.
