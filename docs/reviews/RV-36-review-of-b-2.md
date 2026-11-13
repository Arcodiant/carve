---
description: >-
  Review of B-2
---
# RV-36 — Review of B-2

**Status:** Answered

**Reviews:** B-2

**Round:** 1

**Previous:** —

**Diff:** d0009bcaec535509ad6144f6b8a705d241d14c49 against 72049e7

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

### No test asserts the pointer on `move`'s own tool output, the path the symptom is about (Minor)

The symptom is what `move` returns over MCP, but the only `move` assertion of the new pointer is `MoveTests.RefusesAPairThatIsNotAnEdge` (`tests/Carve.Core.Tests/MoveTests.cs:102`), which checks the Core `MeshError`, not the tool result. The server-level `move` case for the same input, `EditToolTests` (`tests/Carve.Server.Tests/EditToolTests.cs:180`, `{"edges":[[0,6]],"by":[0,1,0]}`), still asserts only `edges[0-6]`, and the shed scenario's error path (`scenarios/Carve.Scenario.Shed/ShedScenario.cs:98`) asserts only `[3, 9]` and `is not an edge`. The server path through `RenderTests` is a different tool. Today `MoveTool` passes the text through `MeshToolSupport.Edit`/`Failed` unchanged, so nothing is wrong now, but if `move` ever rewrote or cut the message no test would catch it. For `loop_cut` the pointer is asserted at the server level (`tests/Carve.Server.Tests/LoopCutToolTests.cs:67`). Making the `EditToolTests` move case, or the scenario's `ExpectToolErrorAsync`, also expect `select with kind edges lists the real edges` would cover the case this bug is about.

**Outcome:** fixed

`EditToolTests`' `[[0,6]]` move row now asserts the full message with the pointer, and `ShedScenario.cs` expects `select with kind edges lists the real edges` on the `[3, 9]` error path.

### The bug's Fix says "every caller" of `UnknownElements` is `move` and `render`'s highlight, but there are more callers (Minor)

`docs/bugs/B-2-move-s-non-edge-error-does-not-point-to-select.md` (Fix: "Every caller of the check, `move` and `render`'s highlight, gets the new text"; Cause: "the one check `move` and `render`'s highlight share") lists two callers. `MeshEdits.UnknownElements` is also called by `merge_faces` (`src/Carve.Core/FaceMerge.cs:37`), `delete` (`src/Carve.Core/MeshDelete.cs:62`) and `plane_cut` (`src/Carve.Core/PlaneCut.cs:63`), and `render`'s highlight reaches it twice: once at the call (`src/Carve.Server/RenderTool.cs:423`) and again in the pump against the mesh shown (`src/Carve.Server/RenderPump.cs:85` via `Highlight.Unknown`, `src/Carve.Server/LookMeshes.cs:47`). The conclusion holds, because the other three pass an empty edge list and only `move` and the two highlight checks can produce the non-edge message. But the claim as written is the one a later reader will rely on to judge reach, so it should say that only callers that pass edges (`move` and `render`'s highlight, at the call and in the pump) see the change, and the rest pass no edges.

**Outcome:** fixed

The bug's Fix now says only `move` and `render`'s highlight pass edges, and that `merge_faces`, `delete` and `plane_cut` call the check with none.

## Summary

The fix is in the right place. The non-edge text is built in `MeshEdits.UnknownElements`, the one check that `move` and `render`'s highlight share, and it now ends with the same words `loop_cut` and `mark_edges` already use. Those words match the "facts only" sentence the person ruled on RV-20, so the symptom is gone for every path that can produce the message, and no assertion was loosened (the `MoveTests` and `RenderTests` assertions were tightened and pass). The two findings are minor: `move`'s own tool output has no test for the pointer, and the bug file's caller list is not exact.
