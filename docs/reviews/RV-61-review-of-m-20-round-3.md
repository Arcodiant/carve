---
description: >-
  Review of M-20, round 3
---
# RV-61 — Review of M-20, round 3

**Status:** Answered

**Reviews:** M-20

**Round:** 3

**Previous:** RV-60

**Diff:** 05540ce30a12545fe3c47159c9e49fa295f5bb74 against 7e13279

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### The labelled bridge refusal is refused for three reasons, not the stated one

- **Where:** `tests/Carve.Server.Tests/ToolFamiliesThroughProcessTests.cs:36` (rubric 1, 2)
- **What is wrong:** RV-60's answer names this refusal "bridge's 4-vs-3 loops". It runs after the first `bridge` has closed the box, though. I logged the result in a scratch copy, and it is refused with 8 problems. Six edges of both loops are "used by 2 faces, not open". In `[4,5,6]`, vertices 6 and 4 share no edge, so the loop is not a cycle. The "4 and 3 vertices" count check is only the last of the 8.
- **Consequence:** the call does not pin the loop-count refusal. If that check were dropped or broken on either side, the call would still be refused for the other seven problems, and the test would still pass. The JSON comparison would catch a difference between the sides in the text, but not a check that is missing on both. Only a real equal-count refusal would cover it, for example placed before the first `bridge` with a loop of 3 that is a cycle of open edges. The cut family's labelled refusal (`merge_faces {"faces":[0,1]}`) has one problem, the stated one ("must be connected by shared edges").
- **Severity:** Minor

**Outcome:** no change needed: the family test's job is that the process and the harness answer alike, which holds for this call on both sides; the loop-count refusal itself is pinned at unit tier by MergeBridgeToolDefinitionTests (line 127, refused with "4 and 3" on a mesh where it is the only problem), so a break in that check fails there.

## Summary

- RV-60 "The family test never checks `Refused`…" (fixed): holds.
  - For every call, `IsError` is asserted equal to `Refused` on the harness and on the process, beside the JSON equality.
  - I logged every call in a scratch copy under %TEMP% (`git archive HEAD`). All 35 unlabelled calls succeed on both sides. The two labelled ones are refused on both.
  - The export family writes `out\m.glb` on both sides (listing `m.carve, out\m.glb` each, 3208-byte glb). The listings are compared, then the bytes.
  - Mutations, each reverted:
    - dropping the `overwrite` from the second export fails with "refused on the harness; it should have succeeded";
    - removing `set_uvs` from the process's tool collection in `Program.cs` alone fails the test (`KeyNotFoundException` on the JSON-RPC error's missing `result`; blunt, but it fails).
  - Nothing was loosened. The diff adds assertions, and the only argument changes are the ones that make calls succeed. The reason behind the bridge refusal is the finding above.
- RV-60 "server-regression is still recorded as 228 tests" (fixed): holds. `--list-tests --filter Tier=regression` in the scratch copy gives 223.
  - T-89 (Timings, After the rebase, Not checked), T-90 and the M-20 table give 223, and each says where 228 came from. The sums read 751 + 223 = 974 and 746 + 228 pre-rebase.
  - In RV-59, only the family finding's `**Fix:**` line changed, and it now says accurately what round 1 did and did not do. The finding's own text is unchanged.
- Nothing else new in b082631: the summary comment in the test matches what the test does.
