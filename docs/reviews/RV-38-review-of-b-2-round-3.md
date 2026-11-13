---
description: >-
  Review of B-2, round 3
---
# RV-38 — Review of B-2, round 3

**Status:** Answered

**Reviews:** B-2

**Round:** 3

**Previous:** RV-37

**Diff:** c5ca184d506a844e1610360b2a58c5d074c2e0fc against 72049e7

**Reviewer:** Claude Opus 5.5 (fresh subagent)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

## Summary

The fix is at the cause: the non-edge text is built once, in `MeshEdits.UnknownElements` (`src/Carve.Core/MeshEdits.cs:363`), and now ends with the same words as `loop_cut` (`src/Carve.Core/LoopCut.cs:57`) and `mark_edges`, the pointer-only text the RV-20 "facts only" ruling allows; of its callers only `move` and `render`'s highlight (at the call and in the pump) pass edges, and `merge_faces`, `delete` and `plane_cut` pass none, so no other output changes. The RV-36 and RV-37 findings are fixed (`EditToolTests` and the shed scenario assert the pointer on `move`'s tool error; the Cause names the callers exactly), every assertion in the diff is the same or stricter, and each claim in the bug's Cause, Fix, Verified by and Changelog holds against the source and history (`loop_cut` landed in T-66, after the T-10 message). No findings.

