---
description: >-
  Review of M-19
---
# RV-52 — Review of M-19

**Status:** Open

**Reviews:** M-19

**Round:** 1

**Previous:** —

**Diff:** c4c04859ed1aa3eacba40eae6e9fd2bd896d9f8a against 720a6fa

**Reviewer:** three Opus 5.5 subagents in fresh contexts, one per part (product code; scenarios and frame checks; tracker and design)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: product code

Reviewer: Opus 5.5 (claude-opus-5-5), a fresh context that wrote none of this code. Diff: `git diff origin/main...HEAD` (base 720a6fa, head e07fc76), `src/**` and the five test projects named in the brief.

### define_clip's description still says loop closure is X/Z only

`src/Carve.Server/ClipTools.cs:33`. Rubric point 1, against T-83's Goal and its Scope bullet "Update D-6's *Clips* wording and T-45's constants' comments to match".

The tool description says a looping clip warns when its "translation ends more than 1 mm away on X/Z". Since T-83, `ClipChecks.Check` (`src/Carve.Core/ClipChecks.cs:40-42`) compares the 3D distance and its warning text says "on X/Y/Z". T-83 updated the constants' comments and D-6, but not this description. Agents learn the rules from this text.

Failure case: an agent writes a looping Idle whose Hips end 5 cm lower than they start. From the description it expects no warning, because the gap is in Y. `define_clip` then warns "... 50 mm from where it starts on X/Y/Z", which the description says cannot happen. `get_clips`'s description (line 268) and `export`'s description defer to define_clip's wording, so they inherit the error.

**Outcome:** —

### Clip warnings in a model-mode (skinned) export have no test

`src/Carve.Server/ExportTool.cs:230` (`warnings.AddRange(ClipChecks.Check(model))` in `Skinned`). Rubric point 2, against T-45's Scope ("Warnings on `define_clip`, `get_clips`, `save` and `export`"). T-45's As built says this covers the "skinned and clips modes".

`Clip_warnings_reach_define_clip_get_clips_save_and_export` (`tests/Carve.Server.Tests/ClipLibraryToolTests.cs:78-109`) exports only in `mode: clips`, on a skeleton with no mesh. No test in `tests/Carve.Server.Tests` asserts a clip warning on a skinned model-mode export: searching for "clip '", "loops" and `ClipChecks` finds only the clips-mode line. The Orcward and Acceptance load tests assert that the gardener's exports have no warning, so they would not notice the warnings going missing either.

Failure case: delete line 230 and, as far as the tests can tell, every test still passes, while a skinned export of a model with a drifting loop goes out silently. A smaller gap in the same test: the unknown-name error of `get_clips` (line 100) asserts only `IsError`, not the clip list that the As built says it gives.

**Outcome:** —

### get_clips writes `"rotation": null` where the description says the part is left out

`src/Carve.Server/ClipTools.cs:268` (description) against `:316` (`Part` returns `null`). Serialisation is `src/Carve.Server/MeshToolSupport.cs:29`. Rubric point 1, against T-45's Scope (`get_clips` gives channels and keys "as given").

The description says "a part the clip does not key is left out". `Part` returns null for a missing channel, and `Ok` serialises with the default `JsonSerializer` options. Those options write null properties, so a translation-only channel reads back as `{"rotation":null,"translation":{...}}`.

Failure case: a client that reads the result as the description gives it, `"rotation" in channel`, finds a rotation part that is null. No test looks at the missing part, because the test's `drift` clip keys only translation and only `translation` is read. Either the description or the serialisation (ignore nulls for this result) needs to change.

**Outcome:** —

## Findings: scenarios and frame checks

Reviewer: Opus 5.5 (claude-opus-5-5). The worktree is `C:\repos\carve\branches\milestone\M-19` at e07fc76, and the diff is `720a6fa...HEAD`. Paths below are relative to the worktree.

### Orcward's luma rule cannot see the trousers drawn in the band's red (or the reverse)
`scenarios/Carve.Scenario.Orcward/ComparisonPng.cs:28-30`, `OrcwardScenario.cs:198-222`. Rubric points 1 and 2.
The code implements D-6's rule as ruled: luma past 4, more than 0.5 % of pixels, or an 8-connected patch over 50 px. But luma is blind to hue, and two of the gardener's colours have almost the same luma. Trousers `#3C54A0` give 84.4 and band `#C0392B` give 84.7 unlit. I worked them through `ColourRule`'s model (ambient, sun and sky, front view; specular left out), and lit faces give trousers 80.6 / band 79.3 at normal +Z, 83.5 / 80.9 at the shoulder-slope normal, and 79.5 / 77.9 at (0.5, 0, 0.87). Each gap is 1.3 to 2.6, under the threshold of 4.
So a `.glb` whose trousers came out red, or whose hat band came out blue, gives 0 differing pixels in every good frame. That is the kind of fault an export would make with a material index off between primitives or meshes. The only colour checks on the good frames, `CheckFrame`, test the can's face and the near hand (skin). They never test trousers or band.
The scenario therefore does not show that "a game's load matches the preview" in colour for those two groups. A hue check needs to cover them, either through the positive checks or as a chroma term. This does not reopen the ruling: the patch and count limits can stay as they are.
**Outcome:** —

### `ColourRule.cs` is missing from the inputs of six suites that compile it, `scenario-sky` among them
`tests.json:104-123` (`scenario-sky`), `:425-452` (`scenario-orcward`), and the `scenario-character`, `scenario-soft`, `scenario-shed` and `scenario-uvs` entries. Rubric point 6.
cairn's `dotnet-closure` follows `ProjectReference`s and project folders only (`C:\repos\cairn\scripts\cairn\generators.py`). It does not follow linked `<Compile Include="../...">` items, which is why these entries list `CrateBuild.cs`, `GardenerSpec.cs` and so on by hand.
`ColourRule.cs` is linked by the Sky, Orcward, Character, Soft, Shed and Uvs projects. The Character, Shed and Uvs links are new in this diff. Only `scenario-cuts` and `scenario-gatehouse` list it.
Failure case: change `SkyLight`'s coefficients or `Light`. `scenario-sky` exists to hold `ColourRule` to the measured sky, yet it carries its pass, and so do Orcward, Character, Soft, Shed and Uvs, whose lit-palette and "not highlighted" checks depend on the file.
**Outcome:** —

### `render_glb`'s rest frames draw no skin, so the two good rest pairs prove nothing about the skin a game applies
`OrcwardScenario.cs:67-68, 112-113` and the measured table (`round-3\jobs\m19-orcward\orcward\differences.tsv`). Rubric point 1. I raise it under my part because of what the scenario claims to prove.
`swapped-bone-order-rest-front` and `wrong-inverse-binds-rest-front` both differ by 0 px. With UpperLeg_L/R's joints swapped and their inverse binds kept, the joint matrix at rest is world(R) x IBM(L), which is not the identity. A game that skins the loaded file at its rest nodes would draw that leg moved onto the other leg. `render_glb` draws it correct, so at rest it is not drawing what a game would draw from this file.
The "at least one frame" ruling covers the scenario's verdict, and it stands. Even so, the rest pairs `rest-front` and `rest-side`, and every break's rest frame, check only the static mesh and materials, and the summary's "matches at rest" should say so. The `src/` reviewer may want to check whether rest without a clip skips skinning.
**Outcome:** —

### The acceptance frame step has no check that a mirrored or flipped camera fails
`scenarios/Carve.Scenario.Acceptance/AcceptanceScenario.cs:73-93`. Rubric points 2 and 4.
T-49's Notes carry the rule "a positive pixel check on every new frame, and one a mirrored or flipped camera would fail". The scenario's only checks are that the character covers at least 2 % of the frame, which survives any mirror or flip, and a front-render against back-glb control, which shows that the measure tells views apart and not that either draw is the right way round.
If a camera fault is shared by `render` and `render_glb`, both mirror, every pair measures 0, and the round passes. T-49's As built does not list this gap, and the milestone's list of weak checks does not include it.
**Outcome:** —

### `CrateBuild.CheckFront` still claims a mirrored camera fails, but the check that caught it was dropped, and the same gap reaches more frames than "cube front"
`scenarios/Carve.Scenario.Crate/CrateBuild.cs:192-202`, with `CrateLabels.cs` (front tile) and `scenarios/Carve.Scenario.Export/ExportScenario.cs:52, 76, 132` as callers. Rubric point 4.
The comment reads: "A mirrored camera puts +X where +Z is ... so either fails". Under the sky ruling (row 1) `CheckFrontOrder` no longer orders +Z over +X, and that order was what failed a mirror. The comment in `CrateLabels` was updated to "a flipped camera reorders"; the one in `CrateBuild` was not.
The milestone records the mirror gap for "row 1 (cube front)" only. The same rule now judges the crate front, the label sheet's front tile and three frames of the export scenario. None of these has been shown failing under a mirror. The remaining `FrontBarTop`/`FrontPanelBehindBar` difference or the background floor might still catch one, but that is not shown.
**Outcome:** —

### `run-acceptance-negatives.ps1` asserts nothing and documents a switch it lacks
`scripts/run-acceptance-negatives.ps1:8, 31-41, 67-68`. Rubric point 2.
The script prints a table of what failed and exits 0 whatever the table says, so a negative that wrongly passes is visible only to someone who reads the table. The help text says "Needs -NoBuild only if everything is already built", but there is no `-NoBuild` parameter. It always runs `run-scenario.ps1 -NoBuild` and `dotnet test --no-build`, so on a stale build it silently tests old binaries. The `dotnet test` call is also unbounded, unlike `run-acceptance-check.ps1`'s `Invoke-BoundedDotnet`.
**Outcome:** —

### `NotPlanarFold.Check` does not check which faces are named, or that the fold line comes last
`scenarios/Carve.Scenario.Soft/NotPlanarFold.cs:44-55`. Rubric point 2.
T-72's reading, as landed, is that the "first 10" are the lowest face ids and the fold line goes last. The check compares counts and requires the named ids to be in ascending order, but it never compares them with `select`'s ids, which the scenario already writes to `select-non-planar-<mesh>.txt`. It also accepts a fold line anywhere.
Failure case: an export naming faces 3, 7, 12, ... (any 10 ascending non-planar ids, not the lowest), or putting the fold line first, passes both `scenario-soft` and `SoftLoad.Tests`.
**Outcome:** —

### Orcward's good-frame colour checks and T-84's shoulder check use the bare palette, while the rest checks moved to the lit one
`OrcwardScenario.cs:206, 215` and `CharacterScenario.cs` (T-84 shoulder `ExpectGroup`), against `GardenerPixels.cs:62-81`. Rubric point 1.
Under the sky the bare palette was shown to misread boots as skin, and ruling row 6 moved `ExpectRestColours` to `ColourRule.Lit`. The can, near-hand and shoulder-slope checks still classify against the bare palette at a 0.98 match. They pass today, but they are not applying the prediction the ruling made the rule for these frames, and the shoulder slope is the most upward-facing (bluest-lit) point checked. This is the same class of check as the one the ruling changed, applied unevenly.
**Outcome:** —

### Walk 0.25 s is a key, yet D-6 explains its 2 px difference as "between keys"
`OrcwardScenario.cs:75-79` against T-48's Note ("If the frames differ at a key, that is a bug and not a tolerance: find it"). Rubric point 4.
Walk's keys are at 0, 0.25, 0.5, 0.75 and 1 s, so `walk-0.250-side`, the one good frame that differs, is at a key. The cause D-6 and `ComparisonPng.cs:11-13` give, posed faces against skinned corners, would apply at a key too, so the fault is in the wording rather than the code. Still, T-48's note was not answered on its own terms. The docs reviewer may want D-6's "between keys" corrected.
**Outcome:** —

### Small duplications the refactor pass left
Rubric point 1 (risk of drift). `CheckFrontOrder` exists twice, in `CubeScenario.cs:757-779` (private) and `CrateBuild.cs:232-250`, with the same text and the same background constant 53. `SkyScenario.Expect` (`SkyScenario.cs:88-93`) restates `ColourRule`'s private `Lit` with its own ambient 0.25, which works against the refactor's "lit surface in one place". A change to the ambient in `ColourRule` would leave the sky check on the old one.
**Outcome:** —

## Findings: tracker and design

### CLAUDE.md still says the acceptance projects have no tests.json entry
`CLAUDE.md` layout rows `scenarios/Carve.Scenario.Acceptance` ("No suite (no `tests.json` entry)") and `tests/Carve.AcceptanceLoad.Tests` ("has no suite (no `tests.json` entry)"). Rubric point 7, and a ruling not carried into the docs. Since e07fc76 ("Add as inputs", 2026-11-17) both projects are `dotnet-closure` inputs of `server-unit` (`tests.json` lines 40 and 43). The rows should say that they have no suite of their own but are built by `server-unit`. T-49's As built ruling (3) ("No `tests.json` suite for the two acceptance projects; their `CLAUDE.md` rows say...") needs a one-line note saying the later ruling now covers them. The layout also has no rows for `scripts/run-acceptance-check.ps1` or `run-acceptance-negatives.ps1`, although every other run script has one.
**Outcome:** —

### Milestone "Known and left" contradicts itself on uncovered files
`docs/milestones/M-19-a-clip-library-for-a-game.md:145-146`. Rubric point 7. Line 145 says "`CLAUDE.md` and the acceptance check's files are inputs of no suite; the latter by ruling". The sub-bullet below it, and line 200, say the acceptance projects are now inputs of `server-unit`. Only `CLAUDE.md` and the `run-acceptance-*.ps1` scripts remain uncovered, so line 145 is stale.
**Outcome:** —

### T-85's premise was overtaken by M-7's sky
`docs/tasks/T-85-an-environment-map-in-the-render-scene-so-reflections-show.md` (Goal), together with T-82's As built (`**Review:**` paragraph, line 70: "the scene has no environment map") and the milestone's Known and left (line 142). Rubric point 7. After the third rebase the render scene has M-7's fixed sky. `src/Carve.Server/RenderSky.cs` says its diffuse ambient and specular probe reach PBR faces, and since T-82 that includes every colour face. T-85's "Today the render scene ... has no environment map" is no longer true. Its scope (find an environment map in the engine docs, and accept that every frame changes again) is largely done by M-7 plus this milestone. T-85 should be re-scoped (for example, to "metallic/roughness so a reflection can show") or closed, and the two other mentions updated.
**Outcome:** —

### D-1 contradicts T-82's ruling after the rebase
`docs/design/D-1-carve-concept.md:197` (from M-7): "`render` draws material faces through `Material.Pbr`, under a fixed sky, and leaves other faces as they were". Rubric point 5 (a ruling not consistent across the decision doc). T-82's ruling, recorded in the same doc at lines 85-94, draws colour faces through PBR too, so "leaves other faces as they were" no longer holds on this branch. Lines 85-94 also describe the scene light as "unchanged (ambient 0.25, a directional light of 0.9)" and do not mention the sky that now lights those faces. The rebase kept both texts and did not reconcile them.
**Outcome:** —

### T-48's and T-82's As built were not brought up to date after the sky rebase
Rubric point 7. The refactor pass updated T-45, T-50 and T-49 only.
- `docs/tasks/T-48-orcward-done-when-scenario.md`, *The rule*: "Orcward's 8/255 and 0.5 % stay for shading noise". The code is `Comparison.PixelThreshold = 4` on luma (D-6, 2026-11-17). T-48 owns the rule and its As built now describes a measure the scenario no longer uses. Its table (smallest twist patch 130) is also pre-luma (now 101), with no note saying so, unlike T-49's.
- `docs/tasks/T-82-...md`, table: the cylinder ends are "facet 14 / 3 to 13 / 3", and the soft rim compares "against facet 3". The cylinder grade is "facet 2 to facet 14, `least: 3`". `CubeScenario.cs:270-327` now uses facet 1, not 3, under the sky ("Accept all", rows 3a and 3c). The smooth side's grade "is no longer checked in the shaded frame" (line 274). The light model "ambient 0.25 + 0.9 n.l / pi" is no longer the whole model (`ColourRule` adds the sky and the specular). The As built needs a dated "brought up to date" note, as T-49's has.
**Outcome:** —

### Retrospective: verify suite count does not add up
`docs/milestones/M-19-a-clip-library-for-a-game.md:189`: "across 14 suites (7 ran, 8 carried)". Rubric point 7 (retrospective accuracy). 7 + 8 = 15, and `tests.json` at df70148 has 15 suites. Separately, the verify points df70148 and 91bb472 (lines 57 and 189), and T-82's and T-84's baselines eded55f and 91438b0, are pre-rebase commits that are not ancestors of HEAD. That is expected after a rebase, but the retrospective could say so.
**Outcome:** —

### T-47's and T-50's narrow Verify scopes, and the retrospective's list of partial guards
Rubric point 4.
- T-47 (`**Verify:** server-regression, scenario-cube, scenario-crate`) changed `RenderSystem` and `RenderPump`, which every frame suite draws through. Its guard hashed only the 87 cube and crate frames. Character, soft, rig, shed and export were neither in scope nor hashed.
- T-50 changed `ExportTool`'s mode handling with `scenario-export`, `scenario-soft` and `scenario-rig` out of scope.
- The milestone's "Hash guards that were partial or argued" (lines 118-123) lists T-45, T-83, T-50, T-49, T-72 and T-48 but leaves out T-47.

The impact is small, since the milestone verify ran every suite and T-82 re-baselined every frame afterwards. Still, the retrospective's list should be complete.
**Outcome:** —

### T-49's As built contradicts itself on the session model
`docs/tasks/T-49-character-acceptance-run.md`, *Models*: "The acceptance agents of both rounds ran on Sonnet 5.5". The first paragraph and the milestone retrospective (line 51) say every `claude -p` session ran on Opus 5.5, which matches the ruling "sessions on Opus". The *Models* line probably means the member's own agents. As written, it is wrong.
**Outcome:** —

### D-6's frame-rule text still ties loosening to T-48
`docs/design/D-6-characters-rigging-animation-and-character-export.md:165`: "It is loosened only on differences measured at T-48, and only by the person." Rubric point 5 (wording). The person changed the rule on 2026-11-17 on differences measured in the third rebase's sweep (line 180). The ruling is properly recorded with its table. The opening sentence should drop "at T-48" (for example, "only by the person, on measured differences") so that the Rule does not appear to be broken by its own later ruling.
**Outcome:** —

### T-45's As built states loop closure per axis, but the code uses a 3D distance
`docs/tasks/T-45-clip-checks-and-read-back.md`, closing-tolerance bullet: "translation closes when within 1 mm on X, Y and Z". Rubric point 7. `ClipChecks.cs:40-41` compares `(b - a).Length()` with 1 mm, a 3D distance, as T-83's As built says. A gap of 0.8 mm on each axis passes the per-axis reading and fails the code. D-6 line 109 ("within 1 mm of its first, compared on X, Y and Z") is acceptable as written. T-45's text should say "by 3D distance".
**Outcome:** —

## Summary

**Product code.** **What I read:**

- The task files: the Goal, Scope and As built of T-45, T-47, T-50, T-72, T-82 and T-83.
- Both CLAUDE.md files and the rubric.
- The whole product diff:
  - `ClipChecks`, `MeshWarningFold` and `GetClipsTool`;
  - `ExportTool` (all three modes and the warning suffix), `PieceExport`, `GameLimits` (`wholeBody`), `SaveTool`, `CheckTool` and `MeshToolSupport.Report`;
  - `RenderGlbTool`, `GlbScene`, `RenderPump` (the glb branch and its failure path), `RenderSystem` (`ShowGlb`, `ShowModel`, the cache and `ApplyView`'s framing) and `SurfaceMaterial`.
- The assertions of `ClipLibraryToolTests`, `RenderGlbTests`, `PieceExportToolTests`, `AcceptanceLoadTests` and `OrcwardLoadTests`, by search.

**Checked and found sound:**

- The 3D loop closure and the X/Z-only root travel.
- q/-q counted as equal.
- The bone lookup in `ClipChecks`: `define_skeleton` refuses a bone change while clips exist, so the lookup cannot throw.
- Piece mode's refusals, order and "no clips". Clip warnings are correctly empty for a piece.
- The fold's lowest-ids-first choice and its singular wording.
- The glb branch of the pump skipping the model's target, highlight and weights re-checks.
- The model being hidden on an `Install` while a glb is shown.
- `render_glb`'s argument errors.
- `SurfaceMaterial` matching the export's parameters.

**Not findings, because they are recorded in the As built:**

- Each distinct glb loaded by `render_glb` is never released (T-47's *Known limits*). A long edit-export-render_glb loop grows GPU memory once per export.
- The fold line contains "; ", so it collides with the export line's "; " separator (T-72's note).

**What I ran:** `dotnet test tests/Carve.Core.Tests --filter ClipChecks|MeshWarningFold`, 19 passed.

**What I did not run:**

- Any regression test (`Carve.Server.Tests`), so nothing that needs `carve` or a GPU.
- `render_glb` on an empty store, with no model opened. Every `RenderGlbTests` case opens the gardener first. From reading, the glb branch of the pump bypasses `_meshFault`, so it should work, but this is unverified and untested.
- A frame or hash comparison.
- The second finding's mutation was worked out from searching the tests, not run.
- The third finding was worked out from `System.Text.Json`'s default options, not observed in a live call.

**Outside my part (one line):** whether D-6's *Clips* wording matches T-83 is the docs reviewer's to check.

**Scenarios and frame checks.** **Read:** the Orcward scenario (`OrcwardScenario.cs`, `ComparisonPng.cs`, the headers and edits of `GlbEdit.cs`), `AcceptanceScenario.cs`, `SkyScenario.cs`, `ColourRule.cs` and every caller of it, `GardenerPixels.cs`, the diffs of the Cube, Crate (`CrateBuild`, `CrateColours`, `CrateLooks`, `CrateLabels`), Shed, Character, Soft (with `NotPlanarFold.cs`), Cuts (`KnifeStage`, `CutsChecks`) and Gatehouse scenarios, the three new scripts, and `tests.json`'s diff. Against those I read D-6's frame-rule section, the milestone's Retrospective, the Scope and Notes of T-48, T-49 and T-84, the Orcward load tests' assertions, and cairn's `dotnet-closure` generator.
**Ran:** one Python calculation of the gardener palette's luma and bare-palette classification under `ColourRule`'s ambient, sun and sky model, with specular left out, in a temp folder. I also read the measured `differences.tsv` of the round-3 Orcward run.
**Not checked:**
- I built nothing and ran no suite or scenario.
- I opened no frame, so the crate and export mirror question is argued, not measured.
- I did not check the geometry behind the crate's tilted "lid's front rim" points, which are judged with normal +Y.
- I did not check the soft hat rim's `RimStage`/`RimBreak` headroom under the sky.
- I read the Scope and As built of T-45, T-47, T-50, T-72 and T-82 only through the milestone file.
- I did not read `tests/Carve.AcceptanceLoad.Tests` or `tests/Carve.SoftLoad.Tests` beyond a search.

**Tracker and design.** Reviewed by Opus 5.5 (claude-opus-5-5).

**What I read:**
- the rubric (`review.md`, the header parts of `artifacts.md`, `design-firmness.md`);
- the milestone file whole;
- the `docs/` diff of `origin/main...HEAD`;
- the As built of T-45, T-47, T-48, T-49, T-50, T-72, T-82, T-83 and T-84, and the bodies of T-85 and T-86;
- D-6's changed decisions with their firmness lines, D-1's T-82 paragraph and its M-7 materials lines;
- `CLAUDE.md` (PBR rule and layout).

**Claims spot-checked against the code:**
- `ClipChecks.cs` tolerances;
- `GameLimits.Warnings(..., wholeBody: !asPiece)` (height only is gated);
- `Comparison.PixelThreshold` 4 and `PatchLimit` 50;
- `MinimumStep` 6 in the cube, crate and `RenderTests`;
- `NamedMeshTests.IsOrange`;
- `GardenerSpec.ShoulderSegment` and T-84's `CheckRest` check;
- `tests.json` inputs (gardener.carve, the acceptance closures, 17 suites; 15 at df70148);
- which projects link `ColourRule.cs` and `GardenerPixels.cs` (`CLAUDE.md`'s refactor-pass claim holds);
- that the cited commits exist and whether they are ancestors of HEAD.

**What I ran:**
- `cairn lint`: clean;
- `cairn milestone M-19 plan`: 0 ready, 0 blocked, 9 done.

All nine members are `Done`, and T-85 and T-86 are `Open` and off-milestone. The `Depends on:` lines are consistent with the order: T-48 reaches T-84 through T-82, and T-49 rightly does not depend on T-72, which came out of it. The unchecked boxes in Required tasks are cosmetic.

**T-82 `**Review:**`:** this is not a header. It is a bold lead in the As built (line 70), after `## Notes`. `cairn show T-82` does not parse it as a header, and T-69, T-70 and T-81 on `main` use the same form. It holds no `deep` or `skip` value and none is needed, so this is not a finding.

**D-6 against the rulings:** "1° / 1 mm", "all axes", the cluster rule, "at least one frame", luma past 4, and "render goes PBR" with the highlight ruling are all recorded with dates. No Invariant (pieces carry every bone, one glTF mesh) is broken. T-50's no-height reading departs from a Guideline and is mentioned in the task, which is enough. The other rulings (IsOrange, "Drop that wall", the hash pins, "Add as inputs") are in the milestone file, and "Add as inputs" is missing from `CLAUDE.md` (finding 1).

**Not checked:**
- the frames and artefact folders;
- the transcripts behind T-49;
- the job ids and timings in the retrospective;
- each D-6 table number against `differences.tsv`;
- the per-member diffs before the rebase (the As built "what changed" claims beyond those spot-checks);
- the changelog wording.

The third rebase's re-derived checks and the new `scenario-sky` belong to no member task and are recorded only in the milestone file. I note this without counting it as a finding.
