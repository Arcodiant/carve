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

### As built

Four agents, each on claude-opus-5-5 (Opus 5.5), the first three handing off at the call checkpoint, the fourth
comparing frames and finishing. Commits, after the rebases onto `origin/milestone/M-29` (T-221 and T-217, then T-220
landed; both rebases were clean, T-220's `help` text and this member's sentences side by side): `1d8662bc` (the
frame), `587ebc9b` (the clash, cancellation, mesh causes, the piece test), `26652438` (the `uvs` order, the level clash, the
real-render test, the `help` sentences). Each RV-82 Outcome line is a bare `fixed`; the detail is here.

- **The frame (`DrawnOf`), `faeba76a`.** The pump marks a frame drawn on a model with graph slots no render had baked
  (`RenderPump._unbaked`, set in `ShowMesh`, carried as `RenderRequest.ServedUnbaked`, copied by `RenderSystem.Render`
  to `RenderedFrame.Unbaked`). render's `RenderTiles` runs inside `SheetVersions.Baked` (inside `OnOneMesh`), which
  bakes and draws again while any frame is `Unbaked`, up to 3 tries. `GraphBakes.DrawnOf`'s comment is now true. Test:
  `RenderBakedFrameTests.An_edit_between_the_bake_and_the_frame_is_baked_and_drawn_again_never_returned_with_empty_slots`
  (and `An_edit_racing_every_try_is_an_error_not_a_frame_with_empty_slots`), the linked real `RenderPump`,
  `GraphBakes` and store with a queue whose `TryPeek` hook lands the edit as the frame thread finds the request; both
  seen failing with the retry disabled.
- **The `uvs` look, the same finding, `f6a73f79` and `a60d80cb`.** It draws no graph slots (it draws
  `spec.UvImage`), but its graph image is baked on the snapshot the call read, and the frame can be served on a later
  model, laying another layout over the image. An order that does it, from `RenderPump.Step`: the pump still shows an
  older version than the call's post (an edit it has not yet drawn, the ordinary case right after a tool returns);
  the call reads version V, bakes the image on V and posts at V; an edit to V+1 lands before the frame thread's
  opening peek; the request is not due (`_version < PostedVersion`), so the step shows `store.Current`, V+1, and the
  next step serves the request on V+1. (The part, level, pose and bake changes, which show `store.Current` mid-step,
  are further such orders.) So the retry stays: `RunUvs` from `store.Current` on is `TryOnce`, run through
  `SheetVersions.Baked` with `SheetVersions.UvImageStale(graphImage, bakedOn, drawnOn)`. The second agent's test
  failed because it posted with the pump already on V, where the request is due on the opening peek; it now drives
  the order above deterministically (an unshown edit before the call, the hook's edit at the peek, no sleep):
  `RenderBakedFrameTests.A_uvs_frame_drawn_on_a_later_model_than_its_graph_image_is_drawn_again`. Seen failing with
  one try allowed (the first frame is stale, "The model changed ..." returned) and passing with 3.
- **The `@` clash, `f6a73f79` and `a60d80cb`.** `GraphSlotProblem.Clashes(model, readsMesh, so)` lists every per-mesh
  copy (`m@mesh`, `m@part@level`) whose name the model has or another copy took, in `Resolve`'s order;
  `GraphBakes.Resolve` throws the first as a `GraphSlotProblemException` before anything bakes, which render and
  export return as it is; `GraphSlotProblem.OnMesh` (check, and define_material's and set_material's warnings) yields
  those for its mesh ending "render and export refuse it". `ExportTool.Copies` counts as a copy only `o@<that mesh>`
  on that mesh for a still-unbaked mesh-reading graph material `o`, so a graph material named with `@` keeps its whole
  name. Tests: `GraphExportTests.A_copy_whose_name_the_model_already_has_is_refused_by_export_and_render_and_warned_of_by_check`
  (the reviewer's probe), `A_kept_levels_copy_whose_name_the_model_already_has_is_refused` (new: the copy for part
  'strip' level 0), `Two_copies_of_one_name_are_refused_naming_both`, `A_graph_material_named_with_an_at_keeps_its_whole_name`;
  through a real render (regression tier, `CarveProcess`): `RenderGraphRefusalTests`.
- **Export cancellation, `f6a73f79`.** `ExportTool.Run` takes the call's `CancellationToken`, passes it to `Resolve`,
  and throws if cancelled before writing. `ToolHarness.CallToolWithIdAsync`/`CancelAsync` (as Patina's harness has).
  Test: `GraphExportTests.An_export_named_in_notifications_cancelled_writes_no_glb` (a 2048 curvature bake cancelled at
  0.5 s by a hand-sent `notifications/cancelled`; a second export of the same model answers; the first's .glb never
  appears).
- **Mesh-caused failures, `f6a73f79`.** `GraphSlotProblem.Failed` ends a `MeshLayoutException` with "; the model is
  unchanged" (its own text already says to unwrap or set UVs), a `MeshMaskException` with what to do about the group;
  every other cause (the file, its faults) as before. Tests:
  `GraphMaterialErrorsTests.A_bake_that_fails_on_the_mesh_says_to_fix_the_mesh_through_export_and_render` (export
  through the harness, render's path as `GraphBakes.Resolve`) and `RenderGraphRefusalTests` (no UVs through a real
  render).
- **`PieceExport.Select`, `f6a73f79`.** `PieceExportToolTests.A_piece_wearing_a_plain_and_a_graph_material_is_written_with_both`;
  seen failing with `Select`'s `model.Materials` argument removed (the code before T-164).
- **Every test above was seen failing** with the `src/Carve.Server` change it covers taken out (6 of 1072 for the
  second agent's set, 2 of 1066 for the frame, 1 of 1073 for the `uvs` order) and passing with it.

**The new refusals, whole.** Each is a tool error; render and export change no model (render never does; export's
refusals come before anything is baked or written), so the model is unchanged and no file is written.

- render, an edit between bake and frame on every try: `The model changed between its bake and its frame on each of 3
  tries, so its graph slots would be drawn empty; call render again once the edits have settled.`
- render's `uvs` look, likewise: `The model changed between its bake and its frame on each of 3 tries, so its graph
  image would lie under another model's layout; call render again once the edits have settled.`
- render and export, a copy whose name the model has: `materials['worn']: its copy for mesh 'strip' would be named
  'worn@strip', which is the material 'worn@strip' the model already has, so nothing is replaced and the model is
  unchanged; rename the material 'worn', the mesh 'strip' or the material 'worn@strip'`; for a kept level, `... its copy
  for part 'strip' level 0 would be named 'worn@strip@0', ...; rename the material 'worn', the part 'strip' or the
  material 'worn@strip@0'`; for two copies of one name, `materials['worn@zz']: its copy for mesh 'second' would be named
  'worn@zz@second', which is the name of the copy of 'worn' for mesh 'zz@second', so nothing is replaced and the model
  is unchanged; rename the material 'worn@zz', the mesh 'second' or the mesh 'zz@second'`. check's warning is the same
  with "render and export refuse it" in place of "nothing is replaced and the model is unchanged".
- render and export, the mesh-caused endings: `materials['m'].occlusion_texture: mesh 'strip': the mesh has no UVs, so
  it has nothing to bake into; give it UVs first (unwrap, project_uvs or set_uvs); the model is unchanged`;
  `materials['m'].occlusion_texture: mesh 'strip': the UVs overlap, so a texel would show two faces: faces[0] and
  faces[1]; move the islands apart (unwrap or set_uvs) and bake again; the model is unchanged`;
  `materials['m'].occlusion_texture: mesh 'strip' has no group 'rim'; its groups are none and its materials 'm'; the
  model is unchanged: give mesh 'strip' a group 'rim' (define_group), or name one of its groups in the graph`.

**Help, not descriptions.** No tool description changed (render's and export's sit at the length limit), so the
tools/list digest is untouched. Two `help` entries gained text, the place an agent reads "every error":
`RenderTool.Reference`, after "A slot that cannot be baked fails the render.", the sentence "Each of these is a tool
error with the model unchanged: a bake that fails on the mesh (no UVs, overlapping UVs, a missing group) says to unwrap
or lay out the mesh or name a group it has; a per-mesh copy's name (<material>@<mesh>, or a kept level's) that is a
material the model already has is refused as export refuses it, saying to rename one; and an edit landing between the
bake and the frame on each of 3 tries (...)", quoting both "3 tries" messages; and `ExportTool.Reference`'s error list,
before "a path naming a folder", the items "a per-mesh copy's name (<material>@<mesh>, or a kept level's) that is a
material the model already has, or that two copies would take, named with the mesh and the material it would replace,
to rename one (render refuses it too, and check warns of it); a graph slot that cannot be baked, whose error, when the
mesh is the cause (no UVs, overlapping UVs, a missing group), says to unwrap or lay out the mesh or name a group it
has; a cancelled export, which writes no .glb;". T-220 owns the descriptions and `help`; whoever lands second keeps
both. `README.md`: nothing needed changing (it does not list render's or export's errors).

**Frames.** Compared by SHA-256 by the fourth agent: this member's verified runs at `a60d80cb` (scenario-export
J-20261125-230738-a9fb84, scenario-weathered J-20261125-230741-7538cc, scenario-parts J-20261125-230743-8fff46) against
fresh runs of `origin/milestone/M-29` at `a8392d14` (cairn had no artifacts of a pass at that commit; these were queued
for the comparison: J-20261125-231825-e56895, J-20261125-231827-370725, J-20261125-231826-ad6fec, all PASS). 62 PNGs
(export 7, weathered 40, parts 15: frames, viewer captures and baked textures); none differs.

**Not checked.** The `uvs` retry inside `RunUvs` itself is exercised only through the same `SheetVersions.Baked` and
`UvImageStale` it calls, on the linked pump, not through a real uvs render (the harness cannot drive render's frame,
`EngineStatus.SetReady` being internal to Carve.Hosting); the real-render test covers the clash and no-UV refusals,
not the overlap or group endings or a race. The Weathered scenario's `ErrorsStage.Tail` is a file cause and keeps the
old tail. Bake memory beyond the cap is T-222's.
