---
description: >-
  RV-82: the server's render, export and error findings
---
# T-218 — RV-82: the server's render, export and error findings

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, server-regression, scenario-export, scenario-parts, scenario-weathered

## Goal

`render` never returns a frame with empty graph slots, a per-mesh copy never replaces a material the model has,
`export` can be cancelled, a bake that fails on the mesh says what to do about the mesh, and the piece-export fix
has its test.

## Scope

Each finding of RV-82 named here, by its heading there:

- "`DrawnOf` says no render draws empty graph slots, but a render whose frame is served on a newer model returns exactly that frame" (the server)
- "A per-mesh copy's name can be a material the model already has, and export then renames that material's faces away" (the server)
- "Export cannot be cancelled: a cancelled or timed-out export bakes on and then writes the .glb" (the server)
- "A bake that fails on the mesh tells the agent to redefine the material or restore the file, which is the wrong fix" (the server)
- "`PieceExport.Select` now carries the model's materials, a real fix for a pre-existing failure, with no test" (the server)

- **The frame:** a `render` answers with a frame of the model it baked, or bakes again for the model the frame
  will draw; it never hands back a frame drawn with empty graph slots. The comment that says no render draws that
  becomes true, and the retrospective's line about the shortcut is corrected by the orchestrator from this member's
  `### As built`.
- **The `@` clash, as the person ruled (2026-11-25):** when a per-mesh copy's name (`<material>@<mesh>`, or a kept
  level's) is a material the model already has, `render` and `export` **refuse**, naming the material, the mesh and
  the existing material whose name the copy would take, and saying to rename one; nothing is replaced and the model
  is unchanged. Declined: picking a free name. `check` warns of it. The description of `define_material` or `export`
  gains a clause only if there is room under the length test; otherwise the sentence goes into that tool's `help`
  entry.
- **Cancellation:** `export` passes its cancellation token to the bake as `render` does, and writes no `.glb` after it
  is cancelled, with a test (M-27 found the SDK's client does not send `notifications/cancelled`; the test may send it
  by hand, as that milestone's did).
- **Mesh-caused failures** (no UVs, overlapping UVs, a missing group) end with what fixes them (unwrap or lay out
  the mesh, name a group that exists), not with "define the material again"; a test reads each whole message.
- **Not this member's:** bake memory beyond the cap (the review's fourth server finding) is filed off-milestone as
  T-222 by the person's choice; the cache key is T-217's.
- **Owns:** `src/Carve.Server/GraphBakes.cs` (all but the key's use, which is T-217's), `GraphSlotProblem.cs`,
  `RenderTool.cs`, `RenderPump`, `RenderSystem`, `ExportTool.cs`, `PieceExport`, `CheckTool.cs`, and their tests.

## Notes

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.

### Hand-off

Written at the call checkpoint (about 60 tool calls) by the first agent, on claude-opus-5-5.

- **Done: the frame finding** (`DrawnOf` / an edit between bake and frame), commit `2ed3e9b2`. The pump marks a
  frame drawn on a model with graph slots no render had baked: `RenderPump._unbaked`, set in `ShowMesh`, carried as
  `RenderRequest.ServedUnbaked` and copied by `RenderSystem.Render` to `RenderedFrame.Unbaked`. render's
  `RenderTiles` is now wrapped in `SheetVersions.Baked` (inside `OnOneMesh`), which bakes and draws again while any
  frame is `Unbaked`, up to `SheetAttempts` (3), then errors ("The model changed between its bake and its frame on
  each of 3 tries, ..."). `GraphBakes.DrawnOf`'s comment is corrected. Test: `RenderBakedFrameTests` (unit; the
  linked pump, bakes and store, and a queue whose `TryPeek` hook lands the edit as the frame thread finds the
  request waiting). Its first two tests were seen to fail with the retry disabled (2 failed of 1066) and pass with
  it (server-unit PASS, 1066). Not yet in `### As built`; its RV-82 Outcome line is not filled yet:
  `fixed, 2ed3e9b2, RenderBakedFrameTests.An_edit_between_the_bake_and_the_frame_is_baked_and_drawn_again_never_returned_with_empty_slots`.
- **Left:** the `@` clash (refuse in render and export, warn in check, a description or `help` clause and the
  digest pin), export's cancellation (token into `Run` and `Resolve`, no .glb after a cancel, a test sending
  `notifications/cancelled` by hand), the mesh-caused failure wording of `GraphSlotProblem.Failed` (a test reading
  each whole message: no UVs, overlapping UVs, a missing group), the `PieceExport.Select` test (a piece export of a
  mesh wearing a PNG and a graph material); then `### As built`, every Outcome line, the frame hash comparison
  against `origin/milestone/M-29`, verify, lint and finish.
- **Checked:** server-unit only. Not run: server-regression and the scenario suites; no frames compared yet.
- **Surprising:** `ToolHarness` cannot reach the render tool's internals (the server is an extern-aliased assembly
  and `EngineStatus.SetReady` is internal to Carve.Hosting), so the loop went into the linked `SheetVersions` and is
  tested there with the real linked `RenderPump` and `GraphBakes`. The `uvs` look's own post (`RenderTool.cs`, near
  line 740) draws no graph slots and was left as it was.
