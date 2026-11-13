---
description: >-
  Review of B-2, round 2
---
# RV-37 — Review of B-2, round 2

**Status:** Answered

**Reviews:** B-2

**Round:** 2

**Previous:** RV-36

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

### The bug's Cause says every edit goes through `UnknownElements`, but most edits do not (Minor)

`docs/bugs/B-2-move-s-non-edge-error-does-not-point-to-select.md:21` calls `MeshEdits.UnknownElements` "the check for listed elements that every edit and `render`'s highlight go through". Its only callers are `move` (`src/Carve.Core/MeshEdits.cs:34`), `merge_faces` (`src/Carve.Core/FaceMerge.cs:37`), `delete` (`src/Carve.Core/MeshDelete.cs:62`), `plane_cut` (`src/Carve.Core/PlaneCut.cs:63`) and `render`'s highlight (`src/Carve.Server/RenderTool.cs:423`, and `src/Carve.Server/LookMeshes.cs:47` from the pump). `extrude` does its own face check (`src/Carve.Core/MeshEdits.cs:97`), and `loop_cut` (`src/Carve.Core/LoopCut.cs:57`) and `mark_edges` (`src/Carve.Server/ShadingTools.cs:101`) build their own non-edge text. The Cause relies on the second of those to explain why the two messages disagreed. The Fix paragraph gives the callers correctly, so the conclusion holds, but RV-36's second finding was about exactly this kind of overstated reach, and the Cause still has it. A reader judging what else the change reaches would be misled. "the check for listed elements that `move`, `merge_faces`, `delete`, `plane_cut` and `render`'s highlight go through" would be accurate.

**Outcome:** fixed

The Cause now names the callers: `move`, `merge_faces`, `delete`, `plane_cut` and `render`'s highlight.

## Summary

The fix is at the cause. The non-edge text is built once, in `MeshEdits.UnknownElements`, and now ends with the same words as `loop_cut` and `mark_edges`. That matches the RV-20 "facts only" ruling, and only `move` and `render`'s highlight, the two callers that pass edges, can show it. Both RV-36 findings are fixed: `EditToolTests` asserts the full message on `move`'s tool error, and the shed scenario adds the pointer as a third mention, which `ExpectToolErrorAsync` checks by ordinal `Contains`, so the scenario is only stricter. No assertion was loosened. The one remaining issue is a sentence in the bug's Cause that overstates which edits use the check.
