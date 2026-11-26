---
description: >-
  RV-83: the render pump, the bake key and export's copies
---
# T-226 — RV-83: the render pump, the bake key and export's copies

**Status:** Done

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** graphs-unit, server-unit, server-regression, scenario-export, scenario-weathered

## Goal

No order of events draws a model with empty graph slots and calls it baked; a slot that refuses an output refuses it
from the cache too; export never renames a material the user named; and the render retry is held by a test of
`render` itself.

## Scope

Each finding of RV-83 named here, by its heading there:

- "A model the pump shows while a render's `Resolve` of it lands is drawn with empty graph slots, marked baked, and stays so"
- "A slot that would refuse its output is served the base-colour bake from the cache instead of refusing"
- "`Copies` still renames a user's `o@mesh` material when the mesh-reading graph material `o` is worn by no mesh"
- "The render retry is tested on a copy of `RenderTiles`, not on render, so reverting its one wiring line passes every suite"
- "The export-cancellation test bakes curvature at 2048 twice and takes 6 s in a `unit` class"
- "`Unchanged` compares the files at two instants, not with the bytes the bake read, so an A, B, A rewrite caches B's image under A's key"

- **The pump, as the person ruled (2026-11-25):** this finding has survived two rounds, and the person chose to
  fix it once more with a re-review of the fix alone. Declined: filing it as a bug and shipping. The pump takes the
  bake **once** for a model it shows: one read decides both what is drawn and whether the frame is marked unbaked, so
  the two can never disagree. Then read the whole path again for any other place where "is it baked" is asked twice
  or asked of a different model than the one drawn (`RenderPump`, `RenderSystem`, `GraphBakes.DrawnOf`,
  `SheetVersions`, the `uvs` look), and write in `### As built` the argument for why no order of events is left: an
  edit, a part, level, pose or clip change, two renders at once, a sheet's several frames. The reviewer could not
  force the window in a test; build the seam that makes it forceable (a hook at the one read, as T-218's queue hook
  did) and hold it with a test that fails on the code as it is.
- **The retry on the real path:** T-218's retry test runs a copy of `RenderTiles` written in the test, so removing
  the retry's one wiring line in `RenderTool.cs` passes every suite. Hold the retry by a test that runs the tool's
  own code (extract the loop so the tool and the test call the same method, or drive `render` through the process
  in a `regression` test with the hook), and show it fails with the wiring removed. The same for the `uvs` look's.
- **The key and a refusing slot:** with every slot but metallic-roughness keyed as base colour, a slot that would
  refuse its output (occlusion given a colour output, normal given a grey one) is served base colour's cached bake.
  The refusal comes before the cache is asked, on every path (render, export, the `uvs` look), with the reviewer's
  probe as the test. The slot-table test T-217 added is extended to cover the cache, not only the bake.
- **Export's copies:** `ExportTool.Copies` never renames a material the user named: a material `o@mesh` is a copy
  only if the bake made it for that mesh in this resolve. Decide it from what `Resolve` reports, not from the name's
  shape; the reviewer's probe is the test.
- **The cancellation test** takes 6 s in a `unit` class: make it fast enough for the tier without weakening what it
  shows (cancel during a bake, no file written), or move it to a `regression` class.
- **The A, B, A rewrite (Nit):** the guard compares the bytes the bake read with the bytes on disk after it, not two
  later instants.
- **Owns:** `src/Carve.Server` (`GraphBakes.cs`, `RenderPump`, `RenderSystem`, `RenderTool.cs`, `SheetVersions.cs`,
  `ExportTool.cs`), the key and `Unchanged` in `src/Carve.Graphs/GraphSlotBake.cs`, and their tests.

## Notes

- **A fix member of RV-83,** round 2 of the review of M-29 (`docs/reviews/RV-83-review-of-m-29-checking-rv-82-s-answers.md`). Each finding listed in the Scope is read in
  full there: where it is, what is wrong, and how the reviewer showed it. RV-82 is round 1; the member whose fix a
  finding is about says what it did in its `### As built`.
- Every finding gets a fix with a test that fails without it, shown failing (in a scratch copy with the fix's hunk
  reverted), or, where the finding is about a record, the record made true. A finding that turns out not to hold is
  answered `no change needed: <reason>` with the evidence.
- The member sets the `**Outcome:**` line of each of its findings in RV-83 to a bare `fixed` (`cairn lint` accepts
  nothing after it) or `no change needed: <reason>`, and touches no other finding's line. Commits and test names go
  in this member's `### As built`.

### As built

One agent, on claude-opus-5-5 (Opus 5.5). Code commit `6543ceed`. Five of the six findings are fixed; the Nit (the A, B, A
rewrite) is skipped, filed off-milestone as T-229. Every new or moved test was seen failing in a scratch copy
(`%TEMP%\t226-scratch`, `git archive` of `6543ceed`) with the five fix hunks reverted at once (each test reaches only its own
hunk): 7 of the 8 tests below failed, `With_no_edit_the_frame_is_drawn_once_with_its_bakes` passed as it should; all pass with the fixes.

- **The pump (Major, third attempt).** `RenderPump.ShowMesh` reads the bake once, `resolved = bakes.Peek(snapshot.Model)`, before
  anything else; `GraphBakes.DrawnOf(model, resolved)` (now static) draws from that read, `_unbaked` is `resolved is null` for a
  model with graph slots, and `_shownBakes` (what `Step` later compares) is the same value. The fault path no longer peeks a second
  time. Seam: `GraphBakes.AfterPeek`, run right after `Peek` has read, so a test lands a `Resolve` exactly there. Test:
  `RenderBakedFrameTests.A_resolve_landing_as_the_pump_reads_the_bake_never_leaves_empty_slots_unmarked` (an unshown edit, a
  request posted, the hook resolving that model inside the pump's read). On the old two-read `ShowMesh` it fails with "a frame drawn
  with empty graph slots is marked baked"; with the fix the frame is drawn baked (the next step sees `Peek` differ from the null it
  read and puts the mesh in again before serving).
- **The argument that no order is left.** Only the frame thread reads or writes `_shownBakes`, `_unbaked` and `_bakedFor`, and only
  `ShowMesh` sets them. The scene holds one mesh, the one the last `ShowMesh` put in, and every change of it goes through
  `ShowMesh`: an edit (`store.Current` newer), a pose, targets, weights bone, part or level, and a bake change (`Peek` no longer the
  one shown). In `ShowMesh` the one read `resolved` decides both the slots drawn and the mark, so for every mesh in the scene
  "graph slots drawn empty" and "frames marked unbaked" are the same fact; a request is served with the mark of the mesh it is drawn
  on (`ServedUnbaked = _unbaked` at the serve, copied by `RenderSystem.Render` to the frame). A `Resolve` landing after the read
  cannot change what was drawn or the mark; it only makes the next `Step`'s comparison (`Peek(_bakedFor)` against `_shownBakes`)
  differ, which puts the mesh in again, baked, before the next serve. So a frame is either drawn on a resolution or marked, and a
  marked frame is baked and drawn again by render (`SheetVersions.Baked`, up to 3 tries, then the "3 tries" error). Two renders at
  once: each resolves before it posts, so whichever request the pump serves, it served it with its own resolution or a newer one, or
  marked. A sheet: the comparison is made again before each tile, each tile carries the mark of its mesh, `Baked` retries if any is
  marked and `OnOneMesh` if the versions differ. `Resolve` with the same keys keeps the same `Model` reference, so an equal
  re-resolve does not put the mesh in again and does not need to (it was drawn baked). The glb path draws no model. The `uvs` look
  draws `UvImage`, not graph slots; its own check is by version (`UvImageStale`), not by this mark. No other place asks "is it
  baked": `grep` for `Peek(`/`DrawnOf` over `src` finds only `ShowMesh` and `Step`'s change check.
- **The retry on the real path.** render's model-look loop (resolve, post every tile, wait, `Baked` inside `OnOneMesh`) is now
  `SheetVersions.Render`, and the `uvs` look's (check, bake the image, post, wait, `UvImageStale`, `Baked`) is
  `SheetVersions.Uvs`; `RenderTool` calls each with its own checks, spec and answer, so the code the tests run is the tool's. The
  test queue (`QueueWithAHook`) steps the linked pump as each frame is posted (`RenderQueue.Post` is virtual for it) and can edit
  just before a post (`BeforePost`). Tests: `An_edit_between_the_bake_and_the_frame_is_baked_and_drawn_again_never_returned_with_empty_slots`,
  `An_edit_racing_every_try_is_an_error_not_a_frame_with_empty_slots`, `A_uvs_frame_drawn_on_a_later_model_than_its_graph_image_is_drawn_again`
  (now an edit between the image's bake and the post, once and then on every try). Seen failing with `Render`'s `Baked` removed
  (the code before T-218) and with `Uvs` allowed one try.
- **A refusing slot and the cache.** `GraphBakes.Resolve` runs `GraphSlotBake.Validate` for a normal or occlusion slot (the two keyed
  as base colour that refuse some of its outputs) before the cache is asked; the key itself is unchanged, so base colour, emissive
  and occlusion still share one bake of a grey output. The `uvs` image is packed as base colour, which refuses only a missing output,
  so it needs nothing. Tests: `GraphExportTests.A_slot_that_refuses_its_output_refuses_it_from_the_cache_too` (the reviewer's probe:
  the second export refuses with "'x' is a colour output; occlusion_texture takes a grey one", no `b.glb`; then a material with
  occlusion alone on the cached colour `x`, through `Resolve`, render's path); and the slot table through the cache,
  `GraphBakesTests.Every_slot_refuses_an_output_from_the_cache_exactly_when_its_own_bake_would` (every slot and every output of the
  planks, base colour's bake cached first). Both seen failing with the check removed.
- **Export's copies.** `GraphBakes.Drawn.Copies` lists each per-mesh copy `Resolve` made for a live mesh (copy, material, mesh);
  `ExportTool.Copies` (and the piece path) renames only those still on that mesh's faces, never by the name's shape. Test:
  `GraphExportTests.A_users_at_material_is_not_taken_for_a_copy_of_a_mesh_reading_material_no_mesh_wears` (the reviewer's probe: the
  .glb's one material is `worn@strip`); seen failing on the old rule.
- **The cancellation test** moved, unchanged in what it does, to `GraphExportCancelTests` (`Tier` `regression`, same file), so it
  runs in `server-regression`; the suites select by trait, so `tests.json` needs nothing.
- **The A, B, A rewrite (Nit): skipped, filed off-milestone as T-229.** Raised in two review rounds, so closed here by the person's
  standing rule (the orchestrator's answer, 2026-11-25); not fixed in either form. What was found: the bake reads its files again
  besides `Read`: `Rgba` loads the top file, and `GraphEvaluator`'s analysis (`Patina.Graphs.GraphValidator.Expand`,
  `File.ReadAllText`) reads every nested file. Comparing with the bytes the bake read needs a hook in `Patina.Graphs`, which this
  member does not own. Nothing is cached wrongly unless a graph file is rewritten and restored within one bake.

**Frames.** Compared by SHA-256: this member's verify at `6543ceed` (scenario-export J-20261126-041854-7378b9, scenario-weathered
J-20261126-041857-a6a1e5) against runs of `origin/milestone/M-29` at `8d4b6be5` queued for it (J-20261126-041906-e3f792,
J-20261126-041907-d918d8, both PASS). 47 PNGs (export 7, weathered 40); none differs. The verify: graphs-unit carried, server-unit,
scenario-export, scenario-weathered and server-regression ran, all PASS, none re-run.

**Help and README.** No tool description or `help` sentence changed: render's "3 tries" errors and export's error list are still
true. `README.md`: nothing needed (it lists neither render's retries nor export's copies).

**Not checked.** A real render through the process with an edit forced at the pump's read (the seam is in the linked pump, not in
`Carve.Server.exe`); the `uvs` loop with a real `bakes.Image` of a mesh-reading graph (the test's graph does not read the mesh).
