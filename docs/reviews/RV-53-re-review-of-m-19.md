---
description: >-
  Re-review of M-19
---
# RV-53 — Re-review of M-19

**Status:** Open

**Reviews:** M-19

**Round:** 2

**Previous:** RV-52

**Diff:** 8df55c52e23c2b03c7b2479b24f32f0b62a3b230 against 720a6fa

**Reviewer:** two Opus 5.5 subagents in fresh contexts, one per part (code; tracker and design)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: code

### The acceptance silhouette step throws on a model with a deleted element or a holed face

`scenarios/Carve.Scenario.Acceptance/AcceptanceScenario.cs:65` and `:68` (d5f3ec6). Rubric 1, correctness. The new step reads every mesh through `get_mesh` and takes `v.GetProperty("at")` for each vertex and `f.GetProperty("vertices")` for each face. `get_mesh`'s own description (`src/Carve.Server/GetMeshTool.cs:13`, `:17`) says a deleted vertex or face is `null` in its slot, and a face with holes is written as `{loop, holes}` with no `vertices`. So `GetProperty` throws (`InvalidOperationException` on a null element, `KeyNotFoundException` on a holed face). Failure case: a fresh agent's round in which the character was made with `delete`, `knife`, `plane_cut` or a holed `define_mesh` face. The frame step then stops with an exception before any frame is captured, instead of measuring a round that may be good. Before d5f3ec6 the step never read `get_mesh`. A smaller effect of the same parsing: if holed faces were read by their outer loop, the fan fill would cover the holes, and the prediction would come out larger than the frame.

**Outcome:** fixed

**How:** Silhouette.Read (`scenarios/Carve.Scenario.Acceptance/Silhouette.cs`) skips a null vertex or face and reads a holed face as its loop and its holes; `Predict` frames on the live vertices only and draws a holed face from its loop and holes together (even-odd), a plain face by its fan as before, so the gardener's prediction is unchanged. `SilhouetteTests` (server-unit) reads a get_mesh body with a deleted vertex, a deleted face and a holed plate, and checks the hole is open; shown failing with the holed face filled from its loop alone ("the hole's middle is drawn"). Commit e881a57.

### Nothing fails if the two RV-52 render fixes are undone (findings 3 and 9)

`src/Carve.Core/Skinning.cs:164` and `src/Carve.Server/GlbScene.cs:100` (05237fb). Rubric 2, tests cover the scope. These RV-52 findings were "`render_glb`'s rest frames draw no skin…" and "Walk 0.25 s is a key…". Each answer is a behaviour change, and no committed check fails without it:
- **Bind pose:** `RenderGlbTests.cs:183` changed only its comment. Its rest assertion (the head reads as skin) held when the file was drawn unskinned too. `OrcwardLoadTests.Every_wrong_file_fails_the_same_rule` requires one failing frame per break kind, and the walk frames already gave that before the fix. The scenario's own check (`OrcwardScenario.cs:177`) is the same. So `swapped-bone-order-rest-front` and `wrong-inverse-binds-rest-front` could go back to 0 px and every suite would still pass. Nothing tests the new caption, "no clip: skinned at its bind pose" against "the file as loaded, unskinned".
- **Flat meshes:** no Core or LookMeshes test was added or changed for `Skinning.Posed` giving a flat mesh skinned corners, or for the new `FaceHasCornerNormals` rule on a posed flat mesh. The existing `ShadingTests.cs:215-218` cover only unposed meshes. The Orcward guard does not catch a revert either: walk 0.25 differed by 2 px before the fix and still passed D-6's rule, so "every good frame differs by 0 px" is a measurement and not an assertion.

I checked the code paths themselves and found them right. Flat, smooth, mixed (combined) and unweighted meshes and pieces all agree with `SkinnedExport` (`Shading.CornersOf`, a flat face's normal at each corner). `Model.Combined`'s flat flags are unchanged for every case. Deleted faces are safe because a tombstone has no corners. What is missing is a test that holds them.

**Outcome:** fixed

**How:** `RenderGlbTests.With_no_clip_a_file_with_a_wrong_skin_draws_wrong_at_rest` (server-regression) swaps the thighs' inverse binds with Orcward's `GlbEdit` and requires the rest frame to differ from the good one past 0.5 % and the caption to say bind pose; with GlbScene's bind pose removed it failed on the caption, and with the caption check also off on the pixels ("differs ... by 0 px"). `ShadingFileTests.A_posed_flat_mesh_is_lit_by_its_skinned_corners_and_shows_its_faces` (core-unit) poses a flat cube with blended weights and requires its corners to be `Skinning.CornerNormals` and its faces flat in the normals look; with `Skinning.Posed` giving only a smooth mesh corners it failed ("a posed flat mesh carries no skinned corners"). Both fixes restored, never committed reverted. Commit 3c16e98.

### `NotPlanarFold`'s new checks have no unit test, beside a unit-test class for the old ones

`scenarios/Carve.Scenario.Soft/NotPlanarFold.cs:25` and the fold-last check in the private `Check` (45ac156). Rubric 2. RV-52 finding "`NotPlanarFold.Check` does not check which faces are named, or that the fold line comes last" was answered "Shown failing both ways". That showing is not committed. `tests/Carve.SoftLoad.Tests/NotPlanarFoldCheckTests.cs` (unit tier, no run folder needed) still calls only the count overload. No test passes the id overload a wrong set of named faces, or puts a named face after the fold line. Failure case: a change that drops the `SequenceEqual(lowest)` comparison or the `foldSeen` check passes every unit test. Only a soft scenario run whose export happened to misorder its warnings would catch it.

**Outcome:** fixed

**How:** `NotPlanarFoldCheckTests` gains the id overload passing the right text, failing named faces that are not select's lowest (the count overload passes them), and failing a face named after its mesh's fold line (both overloads). Shown failing with the `SequenceEqual(lowest)` and `foldSeen` checks disabled: 2 of 9 fail. Commit 1092901.

### OrcwardLoad.Tests does not require the new break, and its comment is now false

`tests/Carve.OrcwardLoad.Tests/OrcwardLoadTests.cs:97` and `:102` (45ac156, 05237fb). Rubric 1/2, low. The table is still read correctly. The new `colour` column is the ninth field, and `p[6]` "fails" already folds a wrong colour in through `Fails(r)`; I confirmed this on run `orcward/3` (7 of 7 pass). But the list of break kinds that must be present has no `swapped-colours`. The RV-52 answer says that break "fails on colour at all 3 rows", and the load checks would not notice if the break vanished from the table. Separately, the comment at `:102` says "A rest frame is drawn with no palette, so a wrong bone order or inverse bind shows only in a clip frame". So does `OrcwardScenario.cs:174`. Since 05237fb both kinds fail at rest (2669 and 10361 px in run 3).

**Outcome:** fixed

**How:** `swapped-colours` added to the break kinds `Every_wrong_file_fails_the_same_rule` requires; the comments at `OrcwardLoadTests.cs:102` and `OrcwardScenario.cs:174` now say a wrong bone order or inverse bind fails at rest too, since render_glb skins at the bind pose. OrcwardLoad.Tests on `orcward/3`: 7 of 7 pass. Commit ca47b0a.

## Findings: tracker and design

### D-6's and T-48's after-fix tables give a measured 0 for good frames at Walk 0.5 s front, but no good frame is drawn there
`docs/design/D-6-characters-rigging-animation-and-character-export.md:192` and `docs/tasks/T-48-orcward-done-when-scenario.md:81`: the row "good frames ... and the control | 0 | 0 | 0" puts a 0 under the column "Walk 0.5 s front". Rubric point 7 (the As built matches the code) and point 5 (D-6's measured table). `OrcwardScenario.cs:66-80` draws its good pairs at rest front and side, Idle front and Walk side only. The control is Walk 0.5 s side only (`:120`). At Walk 0.5 s front the scenario takes only `render`'s frame (`break-model-walk-0.500-front`, `:102`), as the reference for the breaks, and colour-checks it (`:115`). No `render_glb` of the good file is drawn there. The `differences.tsv` of job J-20261117-231941-730f6e (at 455c21d) has no good or control row for `walk-0.500-front`. The other cells of both tables match that file exactly. The third cell should read "—" or "not drawn"; it should not read 0.

**Outcome:** fixed

**How:** The "Walk 0.5 s front" cell of the good frames and the control reads "not drawn" in D-6's and T-48's after-fix tables. In the RV-53 docs commit.

### T-49's As built still says the rounds were not measured under the luma rule
`docs/tasks/T-49-character-acceptance-run.md:47`: "the rounds were not measured again under it". Rubric point 7. In the fix round (d5f3ec6), the acceptance step was run on a copy of round 2 (`C:\Users\andre\artifacts\carve\M-19\review\acceptance-copy\acceptance\differences.tsv`). Under luma past 4, all 14 good pairs pass, the largest is 1 px (walk 0.000 side), and the control is 21,550 px, patch 16,855. The milestone's *The review (RV-52)* "Not checked" bullet mentions these numbers in passing. T-49's own *Brought up to date* section records only the silhouettes and the mirrored and flipped copies. It does not record the luma measurement, and line 47 is left saying the opposite. The fix is one dated line, noting that the run came before 05237fb and that its frames will move.

**Outcome:** fixed

**How:** T-49's *Brought up to date (RV-52)* gains a dated line: the fix round's acceptance run on a copy of round 2, all 14 good pairs pass, the largest 1 px (Walk 0.000 side), control 21,550 px, patch 16,855, checked against that copy's `differences.tsv`; the run came before 05237fb, so its frames will move. Line 47 now says the rounds were not run again, and points to it. In the RV-53 docs commit.

## Summary

**Code.** I ran on Opus 5.5. These RV-52 code answers hold:
- define_clip's X/Y/Z text, the clip-warnings test for a skinned export, and get_clips leaving out a part the clip does not key (all e889af0).
- `ColourRule.cs` and the other new inputs in `tests.json`.
- CrateBuild's corrected comment, and the one shared `CheckFrontOrder` (same `MinimumStep` 6; the sky check now goes through `ColourRule.Lit`, with the same arithmetic).
- The negatives script: it exits 1 on an uncaught break or a stopped run, `-NoBuild` is real, the checks are bounded through `bounded-run.ps1`, and `$sceneFailed` starts `$false` on every run.
- The lit-palette checks for the can, the near hand and T-84's shoulder.
- Orcward's colour checks and `colour` column: in run 3 all three swapped-colours rows fail on colour while luma passes them (45 to 77 px).

The silhouette prediction is sound for the models it can read. It uses the same `CameraPixels.Perspective` framing as the crate checks, and a camera fault that both draws share fails it through the side view, even though the front view is nearly symmetric left to right. It is IoU 0.9 against the rest pose only, so it guards the camera and not the pose. `GlbScene`'s bind pose goes through the same engine path as a clip. The engine uses the file's first skin (that is the engine's documented behaviour, not new here). With no skin, `Palette` stays null and the caption says unskinned. Bones are the skin's joints only, so a joint node outside the skin is not posed, the same as with a clip.

What I ran:
- `dotnet build Carve.slnx`: 0 warnings, 0 errors.
- `Carve.Core.Tests`: 1029 passed.
- `Carve.SoftLoad.Tests`: unit tests pass.
- `Carve.OrcwardLoad.Tests` against `artifacts/scenarios/orcward/3`: 7 of 7 pass.

What I did not run or check:
- I ran no scenario, no Server regression tests and no acceptance run.
- I did not confirm the acceptance crash by running it; it is read from `get_mesh`'s contract.
- I did not check whether every picked thigh and band face is unoccluded at every time; that rests on run 3 passing.
- I did not check `RenderPump`'s other looks (weights, highlight) on a posed flat mesh beyond the code path.

**Tracker and design.** Re-reviewed by Opus 5.5 (claude-opus-5-5), a fresh context that wrote none of this.

**RV-52's tracker and design answers that hold:** all 10.
- CLAUDE.md's acceptance rows and the two new script rows. Checked against `tests.json`: `server-unit` has the acceptance closures, and the six suites name `ColourRule.cs`. Checked against `run-acceptance-negatives.ps1`: `-NoBuild`, `-ChecksTimeoutSeconds` 180 and `Caught` are there.
- *Known and left*'s uncovered files.
- T-85: Done, with the ruling and `RenderSky.cs`.
- D-1's sky lines.
- T-48's and T-82's notes. T-82's facet 13 against 1, `SideEnds` 4, the soft rim and the dropped grade were checked against `CubeScenario.cs:266-340`.
- The suite count: 15 = 7 + 8.
- T-47 and T-50 in the partial-guard list.
- T-49's *Models* line.
- D-6's loosening sentence, with the milestone's line matching it.
- T-45's 3D distance.

**The rest of the docs pass:**
- D-6 records the three 2026-11-17 rulings ("Add colour checks", "Skin at bind pose", "Fix render in M-19").
- B-5 records "File a bug, off-milestone".
- The milestone file records "Close it" and "Add as inputs".
- No ruling was found left out of the docs.
- T-47's caption claim matches `RenderGlbTool.cs:147`.

**RV-52's form:**
- Every finding body is unchanged from ae76320.
- All 23 have `**Outcome:** fixed`, in the vocabulary, with the detail on a separate `**How:**` line.
- Status is Answered.

One cosmetic point, not counted as a finding. In 20 of the 23 findings, the `**Outcome:**` line follows the body with no blank line, as the reviewers wrote it. It therefore renders as part of the paragraph above, and at line 179 as part of the last bullet.

**Ran:**
- `cairn lint`: clean.
- `cairn milestone M-19 plan`: 0 ready, 0 blocked, 9 done.
- `cairn test artifacts scenario-orcward`, written to a temp folder outside the worktree and deleted afterwards. It fetched job J-20261117-231941-730f6e at 455c21d. Its `differences.tsv` matches every number in D-6's and T-48's after-fix tables: the breaks, the twists and swapped colours 45 / 77 / 45. The only exception is the cell in finding 1.

**Did not check:**
- the frames themselves;
- B-5's face ids and normals, beyond seeing that the Orcward colour checks read faces at those centroids and normals;
- the commit-by-commit "what changed" claims of the fix round beyond these spot-checks;
- the changelog;
- whether `cairn milestone M-19 verify` passes after the docs pass.

**Code, one line:** none seen.
