---
description: >-
  Third review of M-16
---
# RV-26 — Third review of M-16

**Status:** Answered

**Reviews:** M-16

**Round:** 3

**Previous:** RV-25

**Diff:** ae62fcc3b5597ea98e3923e8bea9d3e84125eb52 against 620d4a0

**Reviewer:** claude-opus-5-5 (one reviewer, checking RV-25's answers and the round-2 fix diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings

No findings.

## Summary

All six RV-25 answers hold, R1 to R6. In each, the commit does what its Answer says and the defect is gone. R1: `shiftedFor` is now set only when `wantsIds` is true, so a render that names the lid and asks for no ids cannot be refused. R2: render's error list quotes the refusal word for word as `SheetVersions` writes it. R3: export's description and SkinnedExport's doc state B4's fallback. R4: Model's docs and open's texts name the ':' refusal, and open's error list matches `PathAndMeshArguments`, which refuses an empty name or a ':' for every file version. R5: the new refusal text is true, and both pinned tests changed only their expected text. R6: `WithMeshFrom` names the empty case, and its new test reaches it through `Library()`, which has no meshes. The D1 gap is closed by the new `NamedMeshTests` test, which drives D1 through render's own path. Its labelled half cannot pass with the `ShiftedIdsRefusal` call removed or given `current.Version` twice, because it requires at least one refusal. Its plain half cannot fail on a correct product, because with no ids `shiftedFor` is null. On timing: the labelled half needs a `define_mesh` to commit between the call's snapshot and the frame thread's read of the store, and `RenderPump` shows the newest model whenever a request is not yet due. So a refusal per try is likely but not certain. The test passed 5 of 5 runs locally (about 2 s each) and 4 of 4 with 64 busy-loop processes on 32 cores (about 8 s each). The only way it can fail on a correct product is if every one of 10 tries is drawn on the exact snapshot version, and I judge that unlikely; it is not raised. Its `MostCalls` bound of 2000 copies the existing `RenderTests` stream test. No text in src, tests or docs (reviews aside) still states the old rules: I grepped for the old no-meshes text, "non-empty and unique", "whatever it is", the material rule and "its meshes are". The C4 skip is the fixer's stated choice, and RV-25 did not raise it as a finding. Not covered: I did not run the queue, any suite or the whole test project, and looked at no frames (verify had passed, per the brief). I ran only the one NamedMeshTests test, and did not measure its per-try refusal rate. I did not reproduce the R1 and R6 breaks the Answers claim.
