---
description: >-
  Review of M-28, confirming T-183
---
# RV-79 — Review of M-28, confirming T-183

**Status:** Answered

**Reviews:** M-28

**Round:** 4

**Previous:** RV-78

**Diff:** 259ffea3a852f2db63a470df9ba2c09adfcb64c9 against origin/main

**Reviewer:** one reviewer, claude-opus-5-5 (Claude Opus 5.5), checking only T-183's fix, by the person's choice

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: confirming T-183 (claude-opus-5-5)

### An inner reader of the collided node takes the outer node's faults
- **Where:** `src/Patina.Graphs/GraphValidator.cs:142-176`, and the claim at `tests/Patina.Graphs.Tests/NestedGraphTests.cs:209`.
- **Severity:** Nit
- **What is wrong:** suppose the nested file's `c` is read by another inner node: `c -> d (blend a,b) -> o`. Each of `g/d`'s sources then resolves to the outer `g/c`. If that outer node has no output port (type `output`), the faults land on the expanded node `g/d`, not on `g/c`: `node 'g/d' input 'a': names 'g/c.out', but 'g/c', a node of type output, has no output port`, and the same for `b`. That is 5 faults in all (`chain-output` in the reviewer's probe log `p5.log`). An unknown type is kept quiet at line 176 (`sourceOfUnknownType`), but a known type without the port is not. The test's comment, "Every fault is the outer node's own", holds only because `plain`'s `c` is read by the output alone. Nothing throws. This behaviour was there before T-183, and the fix did not cause it.

**Outcome:** skipped: filed off-milestone as T-186 by the person's ruling (2026-11-22); nothing throws and the graph is still refused, only with extra faults

## Summary

A narrow confirming review of T-183, the fix for the colliding-node finding that RV-76, RV-77 and RV-78 each raised.
The person chose it over a full fourth round. The reviewer read every lookup the collision can reach and found each
guarded. It reverted the fix in a copy and saw the new test fail, then ran Patina.Graphs.Tests (464 of 464 passed).
RV-78's probe now loads the unknown-type case with two faults instead of throwing. Extra shapes were tried: a
three-level nesting, the collided node read by another inner node, a self-read and an outer reader. None throws.
1 finding, a Nit that predates the fix, filed as T-186.
