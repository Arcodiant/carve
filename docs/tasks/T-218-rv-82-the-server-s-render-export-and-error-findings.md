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
- **Surprising (first agent):** `ToolHarness` cannot reach the render tool's internals (the server is an extern-aliased
  assembly and `EngineStatus.SetReady` is internal to Carve.Hosting), so the loop went into the linked `SheetVersions`
  and is tested there with the real linked `RenderPump` and `GraphBakes`.

Second agent (claude-opus-5-5), at its call checkpoint (about 57 calls). Rebased on `origin/milestone/M-29`: nothing
had landed (no sibling commit yet, so T-217's key change is still to come in a later rebase).

- **Done, each test seen failing with the `src/Carve.Server` changes stashed (and `PieceExport.Select`'s
  `model.Materials` removed: 6 of 1072 failed) and passing with them (server-unit, 1072 PASS):**
  - **The `@` clash:** `GraphSlotProblem.Clashes(model, readsMesh, so)` lists every per-mesh copy (`m@mesh`,
    `m@part@level`) whose name the model has, or that another copy took, in `Resolve`'s order. `GraphBakes.Resolve`
    throws the first as a `GraphSlotProblemException` before anything bakes (render and export return it as it is);
    `GraphSlotProblem.OnMesh` (check, and define_material's/set_material's warnings) yields those for its mesh with
    "render and export refuse it". Message: `materials['worn']: its copy for mesh 'strip' would be named 'worn@strip',
    which is the material 'worn@strip' the model already has, so nothing is replaced and the model is unchanged; rename
    the material 'worn', the mesh 'strip' or the material 'worn@strip'`. `ExportTool.Copies` now counts as a copy only
    `o@<that mesh>` on that mesh for a still-unbaked (mesh-reading) graph material `o`, so a graph material named with
    `@` keeps its whole name. Tests (`GraphExportTests`):
    `A_copy_whose_name_the_model_already_has_is_refused_by_export_and_render_and_warned_of_by_check` (the reviewer's
    probe), `Two_copies_of_one_name_are_refused_naming_both`, `A_graph_material_named_with_an_at_keeps_its_whole_name`.
    No description or `help` text was changed, so the digest pin was not retaken (whether one is wanted is open).
  - **Export cancellation:** `ExportTool.Run` takes a `CancellationToken`, passes it to `Resolve`, and throws if
    cancelled before writing. `ToolHarness.CallToolWithIdAsync`/`CancelAsync` (copied from Patina's harness). Test:
    `GraphExportTests.An_export_named_in_notifications_cancelled_writes_no_glb` (a 2048 curvature bake, cancelled at
    0.5 s; a second export of the same model answers; the first's .glb never appears).
  - **Mesh-caused failures:** `GraphSlotProblem.Failed` ends a `MeshLayoutException` with "; the model is unchanged"
    (its own text already says to unwrap or set UVs), a `MeshMaskException` with "; the model is unchanged: give mesh
    'strip' a group 'rim' (define_group), or name one of its groups in the graph" (or the material twin, set_material);
    every other cause as before. Test: `GraphMaterialErrorsTests.A_bake_that_fails_on_the_mesh_says_to_fix_the_mesh_through_export_and_render`
    (export through the harness; render's path as `GraphBakes.Resolve`, whose message render returns unchanged).
  - **`PieceExport.Select`:** `PieceExportToolTests.A_piece_wearing_a_plain_and_a_graph_material_is_written_with_both`;
    failed with `Select`'s `model.Materials` argument removed (the code before T-164).
- **In progress, not passing: the `uvs` look.** The claim "it draws no graph slots" holds (it draws `spec.UvImage`,
  not the model's materials), but its graph image is baked on the snapshot and the pump may serve the frame on a later
  model, laying another layout over the image: the same finding. `RenderTool.RunUvs`'s body from `store.Current` on is
  now a local `TryOnce` run through `SheetVersions.Baked` (a new optional wording argument; `SheetVersions.UvImageDrawn`
  and `UvImageStale(graphImage, bakedOn, drawnOn)`), retrying a frame whose version is not the bake's. The test
  `RenderBakedFrameTests.A_uvs_frame_drawn_on_a_later_model_than_its_graph_image_is_drawn_again` **fails** (1 try, not
  2): with no `Resolve` before the post, the pump's opening step finds the request "due" (`RenderPump.Step`, line 72:
  `_version >= PostedVersion`) and serves it on the version it already shows, so the hook's edit does not reach this
  frame. Next: read `RenderPump.Step` to see which interleaving can serve a uvs frame on a later model than its bake
  (if none can, the `uvs` change is not needed: revert it and its test and answer that in `### As built`); if one can,
  make the test drive it.
- **Left after that:** confirm the "3 tries" error leaves the model unchanged and is the only new refusal; the frame
  hash comparison against a run of `origin/milestone/M-29`; `cairn task T-218 verify` (server-regression and the
  scenarios not yet run); `### As built` (fold both hand-offs in); the five Outcome lines as a bare `fixed` (per the
  orchestrator's correction, `cairn lint` takes nothing after `fixed`; commits and test names go in `### As built`);
  `cairn lint`; `finish`.
- **Not checked:** the copy-vs-level clash (`m@part@level`) has no test of its own; render's mesh-cause and clash
  messages are tested through `Resolve`, not through a real render; the Weathered scenario's `ErrorsStage.Tail` is a
  file cause and keeps the old tail.
