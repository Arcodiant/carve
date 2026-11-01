---
description: >-
  Fifth review of M-1
---
# RV-5 — Fifth review of M-1

**Status:** Open

**Reviews:** M-1

**Round:** 5

**Previous:** RV-4

**Diff:** f6489c05293b9b98623263f5ca933775250d6544 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-4 fix (claude-opus-5-5)

### The new hole sentence's plain reading is refused, and the form that is accepted can never be closed

`src/Carve.Server/DefineMeshTool.cs:28`. Correctness of the tool's contract. "Walk an edge to an inner loop wound the opposite way, then back along the same edge" reads in an indexed mesh as reusing indices, which the same description lists as an error. A 10x10x1 wall with a 4x4 window: front `[0,1,2,3,0,4,7,6,5,4]` and back `[8,11,10,9,8,12,13,14,15,12]` are refused with `faces[0][4]: index 0 repeats faces[0][0]` and three more. The only accepted form duplicates the bridge-end vertices, and then the solid reports `closed=false` with 12 open-edge warnings, so a correct solid with a holed face can never come out closed.

**Outcome:** fixed

## Summary

Round 5 of M-1, one reviewer on claude-opus-5-5, checking RV-4's answer and fix (`git diff 393e2b4 HEAD`). RV-4's answer held: both inputs refused, no false refusal across hand cases and 300,000 random outlines, and the exactness argument is sound. The one finding is in the tool text. The person decided that faces have no holes in M-1: an opening is built from several faces around it, and holed faces go to M-2 with the edge-connected structure.
