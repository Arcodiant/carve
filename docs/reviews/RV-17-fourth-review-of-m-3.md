---
description: >-
  Fourth review of M-3
---
# RV-17 — Fourth review of M-3

**Status:** Answered

**Reviews:** M-3

**Round:** 4

**Previous:** RV-16

**Diff:** 4df4fdd28d286017e461934069d4eba64741d234 against origin/main

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

### "Once 100 changes have followed an open" counts changes that were undone (Nit)

`src/Carve.Server/OpenTool.cs:17` says "once 100 changes have followed an open, the open itself can no longer be undone". This is the one changed sentence that gives a threshold for a named step, and it is true only if no undo came in between. `ModelStore.Undo` removes the entry it takes back (`src/Carve.Server/ModelStore.cs:68`), so an undone change no longer counts towards the 101 that `PushLocked` keeps (`:78`). After `define_mesh`, `open`, 60 edits, 60 undos, 60 edits and 60 undos, 120 changes have followed the open, not counting the undos, and undo still returns `"open"` and the earlier mesh. That was run, as probe P6 below.

The wrong reading costs little: an agent told an undo is gone would not try one that works, and only past 100 edits with undos among them. The clause before it, "only the last 100 changes are kept", is true if "kept" is read as still in the history, and the sentence would be true with "100 changes kept after it" or similar. `UndoTool.cs:16` ("Past 100 changes the oldest drop off") leans on the same word but only says what happens at the limit, and a sequence with undos in it just reaches the limit later, so it is not raised. Rubric 1.

**Outcome:** skipped: the limit is stated correctly and only an undone change is miscounted; a wording fix would force a fifth round for a case an agent meets only past 100 changes.

## Summary

Checked `git diff f82ad46..HEAD`: the wording of d845d12 against `ModelStore.cs`, the rest of the undo and history text against it, and that nothing else moved. RV-16's two fixed findings hold: the sentence in `UndoTool.cs` now has the first-mesh condition, and "the one exception" and "only the first mesh" are gone from `OpenTool.cs` and `README.md`. The viewer finding was skipped by the person and was not looked at again. One Nit, above.

**The changed sentences.** Each sentence in `UndoTool.cs:16`, `OpenTool.cs:17` and `README.md:40` was read against `ModelStore.cs`, and all are true apart from the Nit. `define_mesh`, `open` and the edits through `MeshToolSupport.cs:88` are the only callers of `Replace`, `Push` and `Edit`. A process with a mesh before the `open` can undo it straight after: the push leaves at least two entries, and the drop at `:78` never goes below 101. A process whose first mesh is the `open` has one entry, so `Undo` returns null, `UndoTool.cs:27` gives the tool error, and neither mesh nor version changes.

**The 100-step limit was run, not traced.** Six probes against the real `ModelStore`, using `Push(mesh, "open")`, the call at `OpenTool.cs:58`, all passed:

| Probe | Sequence | Result |
|---|---|---|
| P1 | `define_mesh`, `open`, undo | `"open"`, back to the first mesh, next undo null |
| P2 | `open` as first mesh, undo | null, same mesh, same version |
| P3 | `define_mesh`, `open`, 99 edits, 99 undos, undo | `"open"`, back to the first mesh |
| P4 | `define_mesh`, `open`, 100 edits, 100 undos, undo | on the opened mesh, then null, and the mesh stays |
| P5 | `define_mesh`, 100 edits, 100 undos; then `define_mesh`, 101 edits, 100 undos | first: on the first mesh, then null; second: stops at the first edit's mesh, then null |
| P6 | `define_mesh`, `open`, 60 edits, 60 undos, 60 edits, 60 undos, undo | `"open"` (the Nit) |

So the threshold in `OpenTool.cs:17` is exact where no undo intervenes: 99 can reach past the open, 100 cannot. "The last 100 changes" in `README.md:40` and `UndoTool.cs:12,16` matches P5. The opened mesh itself stays reachable after the limit, and the sentence does not say otherwise.

**Other text.** Searched `src/` and `README.md` for undo, undone and history, and read D-1's *Undo and history* (`D-1-carve-concept.md:155`). `UndoTool.cs:9-18`, `DefineMeshTool.cs:32`, `MoveTool.cs:23`, `ExtrudeTool.cs:25`, `ExportTool.cs:28`, `SaveTool.cs:49,58`, `RenderTool.cs:28,50`, the rest of `OpenTool.Text`, `README.md:36` and D-1:121,155 do not disagree with the new wording. D-1:155 still says "within a session" beside "process", as RV-16 noted, and its next clause settles it. `git grep` finds no remaining "first open" or "one exception" about undo outside `docs/reviews`; its one hit, `T-4:81`, is about an injected exception.

**Nothing but wording moved.** Outside `docs/`, the range changes one line each in `README.md`, `OpenTool.cs` and `UndoTool.cs`, and the two source lines are parts of the `Text` string constants. 3137c72 and 6840c6f touch only `RV-16` and this file, and RV-16's diff is its status and outcomes. In a scratch export of HEAD, `server-unit` (`Tier!=regression`) gave 138 passed, which is the 132 RV-16 ran plus my six probes, so no unit test fails on the changed text.

**The patch-id** comes from `reviews.diff_patch_id` against `origin/main` at HEAD 6840c6f, with the pathspecs `verify` uses. The same pipeline on 24b3f76 gives RV-16's 36bf71f, so it reproduces the recorded value. `cairn milestone M-3 verify` was not run.

**Not checked.** No GPU suite was run and no image was opened. `server-regression` was not run, so `Undo_straight_after_the_first_open_of_a_fresh_process_is_a_tool_error_and_the_mesh_stays` and the other undo tests through the real server did not run here, and the probes stop at the store: from `OpenTool.Run` and `UndoTool.Run` down to it was read, not run. `core-unit`, `dotnet test Carve.slnx` and `verify.ps1` were not run. Text about undo in `docs/` outside D-1 and the review items was not read, beyond the phrase search above. Nothing outside the scope of `f82ad46..HEAD` was re-reviewed. Scratch is in `%TEMP%\m3-rv17\`.
