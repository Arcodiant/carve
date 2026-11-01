---
description: >-
  Sixth review of M-1
---
# RV-6 — Sixth review of M-1

**Status:** Answered

**Reviews:** M-1

**Round:** 6

**Previous:** RV-5

**Diff:** 2612ccb9f3a581cd759c9fea9c39667b2c485af0 against origin/main

**Reviewer:** claude-sonnet-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-5 fix (claude-sonnet-5-5)

### The refusal message for a same-wound bridge still describes holes to the agent

`src/Carve.Core/PolygonMesh.cs:39`, surfaced through `src/Carve.Server/DefineMeshTool.cs:119-123`. Tool text consistent with itself. The description says "A face has no holes", but a same-wound bridge gets "face covers part of its area twice: a hole joined by a bridge must wind the opposite way to the outline", which tells the agent how to build the holed face the description disclaims.

**Outcome:** fixed

### "Holed faces go to M-2" is not recorded in M-2

`docs/milestones/M-1-a-cube-you-can-see.md:105`, `docs/tasks/T-3-define-mesh-tool.md:59`, `docs/tasks/T-1-polygon-mesh-kernel.md:52`. Tracker notes true to the record. All three say holed faces are left to M-2, but `docs/milestones/M-2-shape-it.md` and D-1 do not mention them.

**Outcome:** fixed

## Summary

Round 6 of M-1, one reviewer on claude-sonnet-5-5 (the fix was written on Sonnet), checking RV-5's answer and fix (`git diff 2248b50 HEAD`). RV-5's answer held. Both findings are wording and record-keeping that follow from the person's decision that faces have no holes in M-1.
