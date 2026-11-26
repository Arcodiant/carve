---
description: >-
  RV-83: the render pump, the bake key and export's copies
---
# T-226 — RV-83: the render pump, the bake key and export's copies

**Status:** Open

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
