---
description: >-
  Review of M-21
---
# RV-64 — Review of M-21

**Status:** Open

**Reviews:** M-21

**Round:** 1

**Previous:** —

**Diff:** f21dc38f1b210e1ad167161b1634e4f127cedb0f against 802e486

**Reviewer:** four reviewers, each claude-opus-5-5: the Core; persistence and export; tools, rendering and the viewer; the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: persistence and export (claude-opus-5-5)

### export's target names in the skinned and piece result lines have no test

- **Where:** `src/Carve.Server/ExportTool.cs:227` (`line += TargetsPart(model) + ...` in `Skinned`, which piece mode also goes through); the only test is `tests/Carve.Server.Tests/ExportTargetsResultTests.cs:27` (rubric 2)
- **What is wrong:** T-107's As built says `TargetsPart` adds the names to the result line in model, skinned and piece modes. The one test exports a cube with no skeleton, so it only runs the static path (`ExportTool.cs:169`). No test anywhere checks for "morph target(s)" in a skinned or piece export's result (grep over `tests/` and `scenarios/`). Removing `TargetsPart(model) +` from `Skinned` would fail nothing. The piece claim ("its own targets") is also unchecked against the result text, though `MorphExportTests` does check it against the file.
- **Consequence:** two of the three modes the description promises could lose or misname the targets with no test failing. The arm, which the person approved, is a skinned export.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** `ExportTargetsResultTests.A_skinned_export_names_the_targets_and_a_piece_names_only_its_own` exports a two-mesh chain skinned and as two pieces, and asserts the skinned line and the `upper` piece name `Bend`, and the `lower` piece names no morph target (f7b41dc).

### T-92 still plans to read versions 1 to 7; the version-8 handoff is only in T-101's As built

- **Where:** `docs/tasks/T-92-carve-file-version-7.md:27-29` (not changed in this diff); `docs/tasks/T-101-targets-in-the-carve-file.md` As built, "Version 8" (rubric 7, rubric 5)
- **What is wrong:** T-101 takes 8 and skips 7, which is right. It records what T-92 now owes in T-101 only: read 8 as well, and decide which version holds a model with both parts and targets, since 7 has no `targets` and 8 has no `parts`/`levels`. T-92, still Open, says "Reading takes versions 1 to 7" and describes version 7 as "version 6 plus" parts. Built from its own Scope, it would refuse version 8. It would also hit the parts-plus-targets clash with no ruling, and its Notes tell it to stop and ask about "anything the rulings do not settle".
- **Consequence:** the next builder of M-9 reads a Scope that contradicts the code on `main`. `samples/arm.carve` re-reading would catch the refusal of 8, but only as rework. The clash between the two versions is unplanned.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-92's Scope reads versions 1 to 6 and 8 as well as 7, writes version 8 for a model with targets, and leaves which version holds parts plus targets as an open question for the person at M-9's planning (2fbdc23).

### D-6's Intent still lists morph targets as out of scope

- **Where:** `docs/design/D-6-characters-rigging-animation-and-character-export.md:19` (and the "Milestones planned against this doc" line, which does not name M-21) (rubric 7)
- **What is wrong:** this diff adds D-6's *Morph targets* section, including T-104's `.glb` paragraph. The Intent's "**Out of scope:** ..., morph targets, IK" and the list of milestones planned against D-6 were left as they were.
- **Consequence:** the doc says two opposite things about morph targets.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** D-6's Intent no longer lists morph targets as out of scope, saying M-21 planned them, and M-21 is in its list of milestones planned against it (5b403eb).

## Findings: the whole diff (claude-opus-5-5)

### The retrospective's verify predates the T-71 rebase, and the rebase's product commits are not in it

- **Where:** `docs/milestones/M-21-morph-targets.md:171` and `:210`; commits `f3e5e88`, `8653926` (rubric 4, 7)
- **What is wrong:** The retrospective cites `cairn milestone M-21 verify` "at c5832ab". That commit is no longer on any branch; it is the tree from before the rebase. After the retrospective (`2455994`), two commits rewrote how targets are carried. They move it onto T-71's layer registry (`LayerRegistry.cs` `BlendDeltas`/`ReblendDeltas`/`StandardLayers.Targets`, `MeshEdits.Subdivide`) and edit `AttributeLayerTests`. Neither commit belongs to any item. The retrospective never mentions T-71 or the rebase. "Small product changes outside a member's listed files" leaves these out, and "Every edit matches … within 1e-6 (T-100)" and "18 suites pass" are claims about the earlier code. T-100's product commit `d456c0d` now holds only tests, so its whole product change sits in these unlisted commits.
- **Consequence:** Nothing recorded shows the shipped carriage passed `scenario-morph` or `server-regression`. That matters most for `samples/arm.carve`, which must equal what the scenario saves byte for byte, and those bytes now come from `BlendDeltas`. I ran `core-unit` (1129 passed) and `server-unit` (795 passed) at HEAD in a scratch copy, so the unit tiers hold. The GPU suites were not re-run, as far as the record shows.
- **Severity:** Major

**Outcome:** fixed

**Fix:** M-21's retrospective has "After the person's go (2026-11-18)", recording the rebase onto main and T-71 (f3e5e88), the refactor pass (8653926, efa52d9) and the verifies after each, RV-64 round 1, and a fresh `cairn milestone M-21 verify` at 2fbdc23 (17 ran, 1 carried, `scenario-morph` 19 s and `server-regression` 255 s on Mantorok); "at c5832ab" and "18 suites" now say they are the tree before the rebase, the 1e-6 claim says T-100 measured the removed carriage and its tests pass on `StandardLayers.Targets`, the changes outside members list f3e5e88 and 8653926, and the Rulings line says T-104's record was added for RV-64 (168ad52).

### `core-unit`'s inputs miss files its tests now compile or read

- **Where:** `tests.json:9-29` against `tests/Carve.Core.Tests/Carve.Core.Tests.csproj:28-29` and `tests/Carve.Core.Tests/MorphTargetFileTests.cs:185` (rubric 6)
- **What is wrong:** T-105 linked `scenarios/Carve.Scenario.Morph/ArmPlan.cs` and `scenarios/Carve.Scenario.Crate/CameraPixels.cs` into Core.Tests. T-101's `Every_committed_file_rereads_and_rewrites_equal` reads every `samples/*.carve`. `core-unit`'s inputs list neither linked file, nor `samples/arm.carve`, `arm-lean.carve`, `gardener.carve` or `gardener-soft.carve`. Elsewhere the repo lists linked files explicitly: `server-unit` lists `CameraPixels.cs`, and `scenario-*` list their links. The same gap affects `scenario-morph`, which leaves out `scenarios/Carve.Scenario.Soft/NotPlanarFold.cs`, linked by both the scenario and MorphLoad.Tests (T-106, `bd5e5b1`).
- **Consequence:** A change to `ArmPlan.cs`, the arm samples or `NotPlanarFold.cs` can leave a stale pass carried rather than re-run.
- **Severity:** Minor

**Outcome:** no change needed: answered under the tools section's tests.json finding

### T-107 changed product code with no `**Verify:**` line

- **Where:** `docs/tasks/T-107-morph-targets-acceptance-run.md:7-15`; `d9a7323` (`src/Carve.Server/ExportTool.cs`, `ExportTargetsResultTests.cs`) (rubric 4)
- **What is wrong:** T-107 added `TargetsPart` to the result line of all three export modes and to the tool description, plus a unit test. Its header declares no Verify scope, and its As built says "no `server-regression` run (the verify did not require it)".
- **Consequence:** The export change has no declared suite. The retrospective admits it never had a `server-regression` run of its own.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-107 has `**Verify:** server-unit`, the scope its verify ran, and its As built says the line was added afterwards and that `server-regression` first ran over the export change in the milestone's verify after the rebase (53dcdeb).

### T-103's Verify omits `core-unit`, though it changed Core

- **Where:** `docs/tasks/T-103-render-and-check-at-target-weights.md:17`; `7f76152` (`src/Carve.Core/MeshHealth.cs`, `Skinning.cs`) (rubric 4)
- **What is wrong:** The Verify is `server-unit, server-regression, scenario-morph`. T-103 changed `MeshHealth.Create` on every path, since `AddTargetProblems` runs for every check, and made `Skinning.Morphed` public. `MeshHealthTests` live in `core-unit`. The three new rules have no Core-level test; they are tested only through the `check` tool (`TargetWeightsTests`).
- **Consequence:** The existing Core health tests were not in the declared scope of the change that touched them.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-103's Verify reads `core-unit, server-unit, server-regression, scenario-morph`, and its As built says why and that cairn already required `core-unit` (766764b).

### T-108's `Depends on:` leaves out T-103, which it needed

- **Where:** `docs/tasks/T-108-target-weights-in-render-glb-and-the-viewer.md:15` (rubric 7)
- **What is wrong:** It says `T-104`. The retrospective says T-108's continuation was held until T-103 landed, because its `render` comparison (`RenderGlbTargetsTests`, `3e985a8`) needs `render`'s `targets`. The order table and the header both still say T-104 only.
- **Consequence:** The tracker misstates the dependency. The retrospective's own "Next time" calls this out, but the header was not fixed.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-108's header reads `**Depends on:** T-103, T-104`, and M-21's order table says T-108 needs T-103 for its `render` comparison (894dab6).

### The retrospective says every ruling is in the member's As built; the vertex-split ruling is not

- **Where:** `docs/milestones/M-21-morph-targets.md:191,194` against `docs/tasks/T-104-morph-targets-in-the-glb.md:67-72` (rubric 7)
- **What is wrong:** The retrospective says "each recorded in the member's As built" and lists the vertex-split ruling as "Kept". T-104's As built still ends "the person may want to see it". No ruling is recorded there.
- **Consequence:** A reader of T-104 cannot tell that the split was ruled on.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-104's As built records the person's ruling, kept as built (1c7d597), and the retrospective's Rulings line says that record was added in answer to RV-64.

### The weight-range ruling is missing from D-6's Morph targets rule

- **Where:** `docs/design/D-6-characters-rigging-animation-and-character-export.md:223-246` (rubric 5)
- **What is wrong:** `render`, `check`, `render_glb` and the viewer all refuse weights outside [0, 1], and Carve stores no range (the ruling of 2026-11-18). D-6's new section, a **Rule**, records names, evaluation, export, edits and `check`, but not this rule. It appears only in the As builts (T-103, T-108) and the retrospective. The three tools do agree with each other in code (`TargetWeightsArgument.cs`, `RenderGlbTool.ReadTargets`, `ViewerOptions.ParseTargets`).
- **Consequence:** A future tool that takes weights has no design entry to follow.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** D-6's Morph targets rule has "Weights are in [0, 1] where a tool takes them", the person's ruling of 2026-11-18, with Carve storing no range (5b403eb).

### Stale names after the rebase in T-100's As built and the scenario csproj

- **Where:** `docs/tasks/T-100-targets-kept-through-every-edit.md:68`; `scenarios/Carve.Scenario.Morph/Carve.Scenario.Morph.csproj:3` (rubric 7)
- **What is wrong:** T-100's post-rebase note lists the removed names (`CarriedRest`, `CarryRest`, `Apply(Func…)`), but not `WithExtrudedDeltas`, which the As built still names and which no longer exists. The extrude ring is now `BlendDeltas`' `Offset` branch. The csproj comment says "T-105 adds the arm as later stages", but per T-105 and the retrospective the arm stage runs before the cylinder.
- **Consequence:** These point a reader at code that is not there.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** T-100's As built gives `WithExtrudedDeltas` as first written and now `LayerRules.BlendDeltas`' `Offset` branch (9db81a4), and the scenario csproj comment says the arm stages run before the cylinder (2b3ffc1).

### The viewer's `--targets` cannot take every name Core allows

- **Where:** `spikes/Carve.ModelViewer/ViewerOptions.cs` (`ParseTargets`) against `src/Carve.Core/MorphTargets.cs:65-68` (rubric 1)
- **What is wrong:** `NameProblem` refuses only an empty name or one containing `:`. `ParseTargets` splits on `,` and `=`, so a valid target such as `Lean=Thin` or `A,B` cannot be given a starting weight in the viewer.
- **Consequence:** This is minor, since the viewer is a spike, but the name rule is enforced differently in two places.
- **Severity:** Nit

**Outcome:** skipped: the viewer is a spike, and only the starting weight is affected: the weight panel takes its names from the loaded model's target names and drives them by index (`ViewerSystem.BeginInteractive`, `WeightPanel`), so a target named `Lean=Thin` or `A,B` is listed and driven from the keys. Letting `--targets` take such a name needs a new syntax, a CLI design change no finding needs

## Findings: tools, rendering and the viewer (claude-opus-5-5)

### `render_glb` words a non-finite weight differently from `render` and `check`, and checks range before names

- **Where:** `src/Carve.Server/RenderGlbTool.cs:168-175` against `src/Carve.Server/TargetWeightsArgument.cs:40-58` (rubric 1)
- **What is wrong:** `render`/`check` refuse `{"Bulge":1e999}` with `targets.Bulge: the weight is not a finite number`. `render_glb` gives `targets.Bulge: expected a number, got 1e999`. I confirmed that `JsonElement.TryGetDouble("1e999")` returns `true, Infinity` on .NET 10, so `IsFinite` is false and the code takes the "expected a number" branch. The same happens for `1e39`, which is finite as a double but overflows the float `IsFinite` casts to. The order also differs. `render` checks the name first and then the range, so `{"Nope":2}` names the unknown target. `render_glb` checks the range in `ReadTargets` and only checks names in `GlbScene.Prepare` if nothing failed, so the same call reports the range. T-108's As built says the refusals use "the same `targets.<name>:` form that T-103's `TargetWeightsArgument` uses". The prefix is shared, but the wording and order are not. No test covers a non-finite weight in `render_glb`.
- **Consequence:** For the same bad input, the three tools give different answers, and one of them tells the agent "expected a number" when it gave a number. This goes against the ruling that the three tools agree.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** `TargetWeightsArgument.WeightRefusal` is shared by `render`, `check` and `render_glb`, so `1e999` and `1e39` give `targets.<name>: the weight is not a finite number` in all three; `render_glb` hands `GlbScene.Prepare` every name and its refusal and Prepare tells an unknown name before its weight (`{"Nope":2}` names `Nope`). Where no engine can check names the weight refusal is told at once. Unit tests: `RenderGlbTargetArgumentTests` (harness for the words, the CPU parser for the order), `RenderGlbTargetsTests` keeps the loader case, commit a7c00c6.

### `check`'s "against the rest mesh" and "not zero-area at rest" have no test that fails without them

- **Where:** `src/Carve.Server/CheckTool.cs:62`, `src/Carve.Core/MeshHealth.cs:254`, `tests/Carve.Server.Tests/TargetWeightsTests.cs:122` (rubric 2)
- **What is wrong:** I ran two mutations in a scratch copy, and all 13 `TargetWeightsTests` still passed under each, as did the Core `Morph|Health` tests under the second:
  - **Rest mesh replaced by the morphed mesh.** `CheckTool` passed `target.Morphed(weights).WithTargets(target.Targets)` as `rest`, so the target rules compared against the mesh at the given weights rather than at rest. `Check_at_weights_..._still_compares_weight_1_with_rest` passes anyway, because its `Inside` target flips from either base.
  - **Rest-area clause removed.** `if (restHas)` was dropped, so a face already degenerate at rest is reported as collapsing.
  Both are Scope clauses of T-103 ("against the rest mesh"; "that is not zero-area at rest").
- **Consequence:** Either rule could regress silently.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** `TargetWeightsTests` gained `Check_at_a_weight_still_judges_a_target_against_the_rest_mesh_...` and `A_face_with_no_area_at_rest_is_not_reported_...`; the first fails with `CheckTool` passing the morphed mesh as rest, the second with `if (restHas)` dropped (each run failing and the source restored), commit 870bf3c.

### Only the GPU scenario catches `render` dropping the morph when a pose is given

- **Where:** `src/Carve.Server/RenderPump.cs:187`; `tests/Carve.Server.Tests/RenderPumpTests.cs:113` (rubric 2/6)
- **What is wrong:** I changed `Skinning.Posed(snapshot.Model, pose, morphed)` to `Skinning.Posed(snapshot.Model, pose)`, so a pose plus targets draws the unmorphed mesh. All 795 server unit tests still passed. `RenderPumpTests` already records what the pump hands its fake scene, but its targets test has no pose. Only `scenario-morph` catches this: the cylinder's `CheckDiffers(posed, posedBulge)` and the arm's curl frame. The pose/targets branch choice is pump logic, not pixels.
- **Consequence:** The "morph before skin" path of `render` is guarded only by a GPU regression suite. A unit test on the fake scene could hold it.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** `RenderPumpTests.A_pose_and_target_weights_draw_the_morph_first_and_then_the_skin` posts a pose and targets on the fake scene and checks the shown mesh is morphed then skinned; it fails with `Skinning.Posed(snapshot.Model, pose)` (run failing, source restored), commit e741625.

### `render_glb`'s `targets` with `clip` is untested, so its morph-before-skin is unverified

- **Where:** `src/Carve.Server/RenderSystem.cs:285-292`; `scenarios/Carve.Scenario.Morph/ArmStages.cs:161-168`; `tests/Carve.Server.Tests/RenderGlbTargetsTests.cs` (rubric 2)
- **What is wrong:** The tool description says the weights are "blended before skinning, with or without a clip", and the T-108 Scope says "with `clip`/`time` as before". No test or scenario frame calls `render_glb` with both `targets` and `clip`. Frames 8 to 10 are unposed: the arm is skinned at its bind pose, where morph-before-skin and morph-after-skin give the same picture. The rubric asks about the order in both tools; for `render_glb` it is never shown.
- **Consequence:** If `MorphWeights` and `SkinnedPose` on one entity do not combine as claimed, nothing here fails.
- **Severity:** Minor

**Outcome:** —

### `render_glb`'s range and type refusals are tested only at regression tier

- **Where:** `tests/Carve.Server.Tests/RenderGlbTargetsTests.cs:12,135-152`; `src/Carve.Server/RenderGlbTool.cs:91-92` (rubric 6)
- **What is wrong:** `ReadTargets` refuses an out-of-range weight, a non-number or a non-object before the tool waits on the engine or reads the file. Those refusals are tool behaviour that the in-process harness can run, but they are only tested in the `regression` class next to the name refusals, which do need the loader. CLAUDE.md says tool behaviour belongs in `server-unit`.
- **Consequence:** `server-unit` would not catch a change to the ruled 0 to 1 range in `render_glb`.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** Range, non-finite, type and non-object refusals are now `RenderGlbTargetArgumentTests` on `ToolHarness` (they come before the engine wait); the regression class keeps the name refusals that need the loader, commit c673656.

### `get_mesh`'s target example is out of place and not marked, so it is never run

- **Where:** `src/Carve.Server/GetMeshTool.cs:22-25` (rubric 1, worked examples)
- **What is wrong:** The new line `Worked example reading a target ...: {"target":"Bulge"}` sits between "Worked example on define_mesh's unit cube; the output is shown trimmed ...:" and that example's `ExampleMarker + "{}"` and `Output:`. The cube's introduction now reads as introducing the target call. The target example also has no `ExampleMarker`, so `ToolList.Examples`, and the test that runs or parses the examples, never sees it.
- **Consequence:** An agent reading the description gets a confused layout, and the target example could drift without any test noticing.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** The target example follows the cube example's output with its own introduction and `ExampleMarker`; the examples theory defines the bulge for `get_mesh` and runs both, and a test compares the shown output with the tool's; `TargetToolTests` reads the marked example. Commits bb542f7 and 09301dd.

### `define_mesh`'s `drop_targets` refusal is checked outside the store lock

- **Where:** `src/Carve.Server/DefineMeshTool.cs:182-184`; compare `src/Carve.Server/ModelStore.cs:65-89` (rubric 1)
- **What is wrong:** `store.Current.Model.Find(...)?.Targets` is read, and `store.Define` runs afterwards. A `define_target` landing in between is dropped without the refusal. T-101 added `ModelStore.OpenModel`/`OpenMesh` overloads that run open's matching refusal under the write lock for exactly this reason. `define_mesh` does not use that pattern. T-102's As built says "no new `ModelStore` method".
- **Consequence:** Under concurrent tool calls, targets can be lost without the refusal the design promises. The window is narrow.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** New `ModelStore.Define` overload takes the refusal and runs it with the push under the write lock, as `open`'s do; `ModelStoreTests` shows a competing writer cannot land inside the refusal and a refusal pushes nothing; T-102's As built corrected, commit 2dc8635.

### `tests.json` is missing linked source files from the new suites' inputs

- **Where:** `tests.json:407-436` (`scenario-morph`) and `tests.json:8-29` (`core-unit`) (rubric 6)
- **What is wrong:** `Carve.Scenario.Morph.csproj` and `Carve.MorphLoad.Tests.csproj` both link `scenarios/Carve.Scenario.Soft/NotPlanarFold.cs`, but `scenario-morph`'s inputs do not list it. `Carve.Core.Tests.csproj` now links `scenarios/Carve.Scenario.Morph/ArmPlan.cs` and `scenarios/Carve.Scenario.Crate/CameraPixels.cs` for `ArmGeometry`, but `core-unit`'s inputs list neither. Other linked files are listed by hand, for example `GatehousePlan.cs` in `core-unit` and `CrateBuild.cs` and `ComparisonPng.cs` in `scenario-morph`. (The same gap as the whole-diff reviewer's `core-unit` inputs finding, found independently.)
- **Consequence:** A change to the not-planar fold or to the arm's ray test does not re-trigger the suites that depend on it.
- **Severity:** Minor

**Outcome:** —

### The not-planar pin fails if the export becomes clean

- **Where:** `scenarios/Carve.Scenario.Morph/ArmStages.cs:148`; `tests/Carve.MorphLoad.Tests/MorphLoadTests.cs:118` (rubric 1)
- **What is wrong:** `warned < 0 ? "no warnings"` and `Assert.True(warned >= 0, ...)` fail when the export has no warnings, even if `select faces where non_planar` also finds none. The ruling pins the warnings to the select count, and a count of 0 with no warnings meets it.
- **Consequence:** If T-127 or a subdivide change makes the quads planar, the scenario fails on a correct product.
- **Severity:** Nit

**Outcome:** —

### Garbled control characters in an As built path

- **Where:** `docs/tasks/T-105-the-arm-with-dials.md:114`; the same corruption is at `docs/tasks/T-107-morph-targets-acceptance-run.md:48` (rubric 7)
- **What is wrong:** `G:\My Drive\artifacts\...\T-105\round-1` was written with `\a` and `\r` taken as escapes. The file holds a BEL (0x07) and a CR+LF that splits the path across lines ("`G:\My Drive<BEL>rtifacts\carve\M-21\T-105` / `ound-1`").
- **Consequence:** The record of where the approved frames are is unreadable.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** Both paths are plain text again (`G:\My Drive\artifacts\carve\M-21\T-105\round-1` in T-105 and the T-107 equivalent `G:\My Drive\artifacts\carve\M-21\T-107\round-1\`; T-107 had the same loss in its scratch folder, now `%TEMP%\carve-m21-acceptance\round-1\`), with no control character left in either file, commit 0de8af0.

## Findings: the Core (claude-opus-5-5)

### T-100's As built still describes carriage code the rebase removed

- **Where:** `docs/tasks/T-100-targets-kept-through-every-edit.md:68-71` and `:79-86`, also `:34` (rubric 7)
- **What is wrong:** The 2026-11-18 header note lists `CarriedRest`, `CarryRest` and `Apply(Func<int, Vector3>)` as removed. The body still names other code that no longer exists, with no "(now …)":
  - `WithExtrudedDeltas` (line 68). It is not in `src/`. The extrude ring now gets its deltas from `LayerRules.BlendDeltas`'s `Offset` branch.
  - "a stencil on an earlier new vertex is resolved down to source vertices" (line 70). `FaceSplit`'s stencils now only reference source vertices (`Stencil.Lerp(lo, hi, t)` and `Barycentric` over the original face), so nothing does that resolving.
  - `Subdivision.Stencil` (lines 34 and 69). The type is now the top-level `Stencil`.
  - The broken-product table was measured against the removed per-edit code, not against `StandardLayers.Targets`.
- **Consequence:** A reader checking the As built against the code finds names that are not there, and the mutation evidence does not cover the code that ships. I re-ran equivalent mutants on the new layer and the tests do still catch them (see the Summary), so the table can be redone from that.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-100's As built gives each removed name as first written and what it is now (`Stencil`, `LayerRules.BlendDeltas`), drops the resolving of earlier new vertices, and has its broken-product table redone against `StandardLayers.Targets` (9db81a4).

### Knife point inside a face is not checked against the stencil rule, as Scope asks

- **Where:** `tests/Carve.Core.Tests/TargetsKeptThroughEditsTests.cs:199-215` (rubric 2)
- **What is wrong:** Scope says that where the edit is not exact (a knife point inside a face), the test should "assert the stencil rule directly". The test uses targets that are affine in position, and checks each new vertex's delta against the affine function at its position. For such a target, any barycentric blend that reproduces the point gives the same delta. So the test cannot tell whether a delta used the stencil that blends the vertex's weights. In the scratch copy I made `FaceSplit.Barycentric` pick the worst triangle instead of the best. All 11 `TargetsKeptThroughEditsTests` still passed.
- **Consequence:** A future change that gives the targets layer a different stencil from the weights layer for an interior point would pass. So would any non-affine case (the case the rule exists for). The code is correct today: one `VertexOrigin.Blend` stencil feeds both layers.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** `TargetsKeptThroughEditsTests.KnifePointInsideAFaceBlendsATargetThatIsNotAffineByTheTriangleThePointLiesIn` gives a target that is not affine and checks the new interior vertex's delta against the barycentric blend of the triangle the point lies in, within 1e-6 (64e148c).

### Two pieces of target API are dead after the rebase

- **Where:** `src/Carve.Core/MorphTargets.cs:49` (`MorphTarget.Renamed`), `src/Carve.Core/MorphTargets.cs:304` (`MorphTargets.Select`); T-99 As built's API list (rubric 3, 7)
- **What is wrong:** Neither has a caller in `src/` or `tests/`. `Select` was T-99's hook for the per-edit carriage that the registry replaced, yet T-99's As built still lists it as an API "T-100 … call". No As built mentions `Renamed`.
- **Consequence:** Public surface with no use and no test, and an As built that says it is used.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** `MorphTarget.Renamed` and `MorphTargets.Select` are removed, and T-99's As built says so and that the `Targets` layer does the carrying (7b7a4fa).

### A target made from absolute positions can store a non-finite delta

- **Where:** `src/Carve.Core/MorphTargets.cs:92` and `:105` (rubric 1)
- **What is wrong:** `FromPositions` checks that the given position is finite, then stores `given - base` in float without checking the result. Two finite coordinates far apart can subtract to infinity (for example base `-3e38`, given `3e38`). `CarveFile` writes that with `ToString("R")`, which is not valid JSON, and its own reader refuses non-finite numbers.
- **Consequence:** `save` would write a file that `open` refuses. This only happens with coordinates near float's limits.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** `FromPositions` refuses a position whose delta from the base is not finite, naming `positions[id]`, with a test in `MorphTargetsTests` (be52c10).

## Summary

- **Split** (round 1, four reviewers, all claude-opus-5-5, run in parallel). The diff, 86 files over Core, Server, the viewer, a new scenario, samples and docs, was split by area. M-21's members overlap in the Core after the rebase onto T-71, so a split by member range would have cut across them.
  - **The Core** (T-99, T-100, T-101's Core reader and writer, T-103's `MeshHealth`, and the targets layer on T-71's registry). It ran core-unit (1129 passed) and mutations on the shipped layer: a zero ring delta (2 fail), first stencil term only (11), vertex points keeping their own delta (5), the layer reading only stored deltas (4), the layer dropped (24), `Combined` without the id offset (1). Two mutations failed nothing: the `Barycentric` worst triangle (finding) and `Subdivide`'s `.WithoutVertices(gone)` removed (redundant by construction). It confirmed the Core morph tests are byte-identical before and after the rebase.
  - **Persistence and export** (T-101, T-104, T-107's export change). It hashed `origin/main`'s four no-target exports against the pinned SHA-256s (equal). Its probes found a flat/smooth and multi-primitive model exact, static and skinned, with every primitive carrying the same number of targets. Removing the vertex split's tag failed 3 of 12 `MorphExportTests`.
  - **Tools, rendering and the viewer** (T-102, T-103, T-105, T-106, T-108). It ran 54 target server tests and three mutations, all surviving (findings). It confirmed the break switches come only from `CARVE_MORPH_BREAK`, and that undo of `define_target` and `delete_target` is tested.
  - **The whole diff.** Cross-member interfaces, docs, `tests.json`, Verify lines, As builts and the retrospective. core-unit (1129) and server-unit (795) passed at HEAD in a scratch copy, and `cairn lint` was clean.
- **Not run by any reviewer:** the GPU suites (`scenario-morph`, `server-regression`, `viewer-cube`), the engine docs, and the frames. The milestone's `verify` after the rebase and after the refactor pass did run them (see the whole-diff finding's answer).
- **Count:** 26 findings: 1 Major, 18 Minor, 7 Nits. The `tests.json` inputs gap was found by two reviewers independently.
