---
description: >-
  Review of M-21, round 2
---
# RV-65 — Review of M-21, round 2

**Status:** Answered

**Reviews:** M-21

**Round:** 2

**Previous:** RV-64

**Diff:** 17bcb5fb8906b1b327005faa5bd44e0c4d181c22 against 802e486

**Reviewer:** two reviewers, each claude-opus-5-5: RV-64's Core, persistence-and-export and whole-diff answers; RV-64's tools, rendering and viewer answers

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-64's tools, rendering and viewer answers (claude-opus-5-5)

### With an engine, `render_glb`'s weight refusal for a known target is tested nowhere, and the answer says these refusals come before the engine wait

- **Where:** `src/Carve.Server/RenderGlbTool.cs:114` (the `GlbScene.Prepare(..., weights, weightRefusals)` call) and `:176-177`; `tests/Carve.Server.Tests/RenderGlbTargetArgumentTests.cs:32-50`; `tests/Carve.Server.Tests/RenderGlbTargetsTests.cs:150-153` (rubric 2/6; RV-64 findings "`render_glb` words a non-finite weight differently ... checks range before names" and "`render_glb`'s range and type refusals are tested only at regression tier")
- **What is wrong:** Since a7c00c6, `ReadTargets` no longer refuses a range or non-finite weight. It adds the name to `weights` with weight 0 and puts the refusal in a side dictionary. The refusal reaches the agent in only two ways: the no-engine branch (`:97`), or `Prepare` receiving `weightRefusals`. c673656's Fix says the range and non-finite refusals "come before the engine wait". That is no longer true: they are held until after the wait, the path checks and the file read. The `RenderGlbTargetArgumentTests` theory runs on `ToolHarness`, which has no engine, so it only exercises the fault branch. The Prepare-order test passes the dictionary to `Prepare` directly. The regression class tests only unknown names. I removed `weightRefusals` from the tool's `Prepare` call in a scratch copy. All 13 `RenderGlbTarget*` tests still passed, including the regression ones on the real engine.
- **Consequence:** If that one argument is lost, `render_glb` with `{"Bulge":2}` or `{"Bulge":1e999}` on a real engine draws the file at weight 0 and reports success, and no suite fails. The fix created this route, where a refused weight is carried on as 0 toward the draw. The answer's tier claim describes a code path that real use no longer takes.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** `render_glb` hands `GlbScene.Prepare` a refused weight as NaN and not as 0, and `Prepare` refuses a non-finite weight of a known name by itself, so losing `weightRefusals` can no longer draw the file and report success; with the words kept, the text is as before (name before weight, as `render` and `check`). `RenderGlbTargetsTests.A_known_target_with_a_refused_weight_is_refused_and_nothing_is_drawn` runs weights 2 and `1e999` on the real engine and asserts the refusal and that no image is returned. Shown failing with `weightRefusals` removed from the `Prepare` call: the weight-2 case fails (it gets the generic `not a finite number` text instead of `outside 0 to 1`), where before it passed unnoticed; the `1e999` case is refused by the NaN fallback in both shapes. c673656's Fix said these refusals come before the engine wait: that was untrue after a7c00c6 and is still untrue (a refused weight is told after the engine wait and before any path error, because the names need the engine), so that Fix line stands as round 1's record and this one corrects it; RV-64 itself is not edited. Result text is unchanged for a weight that is refused with its words (5626dba).

### `render_glb` still differs from `render`/`check` when one call has several bad targets, and a bad weight now comes after a path error

- **Where:** `src/Carve.Server/RenderGlbTool.cs:91-92`, `:99-102`, `:168-173`; compare `src/Carve.Server/TargetWeightsArgument.cs:37-55` (rubric 1; RV-64 finding "`render_glb` words a non-finite weight differently from `render` and `check` ...")
- **What is wrong:**
  - **Several bad targets.** For `{"Bulge":"half","Other":2}`, `render` and `check` report both entries: Bulge's type, then Other's unknown name or weight. `render_glb` returns at `:92` with only the type error, so the name and weight of `Other` are never reported in that call.
  - **Changed text after a path error.** The change also alters `render_glb`'s answer when an engine is present. `{"path":"missing.glb","targets":{"Bulge":2}}` used to name the weight. It now answers `render_glb: no file at ...` and tells the weight only on the next call. RV-64 did not ask for that change, and neither the Fix nor T-108's As built mentions it.
- **Consequence:** The ruling that the three tools agree still does not hold for calls with more than one bad entry. An agent fixing one problem per call takes extra round trips.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** `render_glb` tells every bad entry in one call, as `render` and `check` do (a type error and each entry's unknown name or weight, a name before its weight), and tells what is knowable before the file is read (clip and time, a target's type, a weight not a number or outside 0 to 1) before a path error, as before a7c00c6; only names wait for the file (`RenderGlbTool.cs`). Tests: unit tier `RenderGlbTargetArgumentTests.Render_glb_tells_every_bad_entry_in_one_call_as_render_does_and_before_a_path_error` on the harness, and `RenderGlbTargetsTests.Every_bad_entry_is_told_in_one_call_and_a_weight_is_told_before_a_path_error` on the engine, which also keeps a name's path error after a good weight (regression tier, since the harness has no engine for the names). The order is recorded in T-108's As built (14ff762). Result text changed only here: a call with a bad weight and a missing file now names the weight, as it did before a7c00c6, and a call with several bad entries now lists all of them (5626dba).

### `CheckExport`'s unit tests run only inside the GPU soft scenario

- **Where:** `tests/Carve.SoftLoad.Tests/NotPlanarFoldCheckTests.cs:7` (the new facts added in 86c10c7); `tests.json:324`; `scripts/run-soft-scenario.ps1:39` (rubric 6; RV-64 finding "The not-planar pin fails if the export becomes clean")
- **What is wrong:** The Fix calls `NotPlanarFoldCheckTests` "(unit)". The class carries `Tier=unit`, but its project is run only by `run-soft-scenario.ps1`, inside the `scenario-soft` suite, which is a GPU regression suite. No CPU suite runs it, and `scenario-morph`, the suite that relies on `CheckExport`, does not run it either. The class was placed there before M-21. 86c10c7 added the new ruling's tests to it.
- **Consequence:** The person's not-planar ruling is pinned only where a Vulkan device is present. The trait suggests otherwise.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** `NotPlanarFoldCheckTests` (its `Check` and `CheckExport` facts, both CPU-only) moved to `tests/Carve.Core.Tests`, which now links `scenarios/Carve.Scenario.Soft/NotPlanarFold.cs`, so `core-unit` runs them; `tests.json`'s `core-unit` inputs list `NotPlanarFold.cs` (the test file is under the project's own closure). `tests/Carve.SoftLoad.Tests` keeps its link of `NotPlanarFold.cs` for `SoftLoadTests`. 14 pass in `core-unit` (cd0fed7).

## Findings: RV-64's Core, persistence-and-export and whole-diff answers (claude-opus-5-5)

### The retrospective's last cited verify is older than the tools-section fixes, which changed product code

- **Where:** `docs/milestones/M-21-morph-targets.md:235-236` (rubric 4, 7; RV-64 finding "The retrospective's verify predates the T-71 rebase, and the rebase's product commits are not in it")
- **What is wrong:** The new "After the person's go" part cites `cairn milestone M-21 verify` at 2fbdc23 as the verify of "every fix above". It then says only that the tools findings "are answered by a further agent". The commits after 2fbdc23 (e741625 through 86c10c7) change product and suite code: `RenderGlbTool.cs`, `GlbScene.cs`, `TargetWeightsArgument.cs`, `ModelStore.cs`, `DefineMeshTool.cs`, `GetMeshTool.cs`, `ArmStages.cs`, `NotPlanarFold.cs` and `tests.json` (`git diff --stat 2fbdc23 HEAD`). The retrospective records no verify over them, nor what those answers changed.
- **Consequence:** This is the round-1 Major again, one round later. The tree that will merge has `server-regression`, `scenario-morph` and `scenario-soft` inputs changed after the last cited verify, and the part says nothing about them. The part's own "Next time" ("cite that run, not the one before it") is not followed.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** M-21's retrospective, "After the person's go", now says what the tools-section answers changed (e741625 to 86c10c7), the verify at b9310b5 that passed every check but `review` (18 ran, 0 carried, `server-regression` 239 s on Mantorok, `scenario-morph` 26 s on Xellotath), this round and how it was answered, and ends on the fresh verify of this round, the one to cite last (a commit 968bb67).

### T-107's As built says `server-regression` first ran over the export change after the rebase; the pre-rebase verify ran it

- **Where:** `docs/tasks/T-107-morph-targets-acceptance-run.md:57` against `docs/milestones/M-21-morph-targets.md:171-173` (rubric 7; RV-64 finding "T-107 changed product code with no `**Verify:**` line")
- **What is wrong:** The added text says "`server-regression` first ran over the export change in the milestone's verify after the rebase onto T-71". But c5832ab is `Merge task/T-107` and already holds `TargetsPart`. The retrospective says the verify at c5832ab ran 16 suites, including `server-regression` (294 s on Xellotath).
- **Consequence:** The task record and the retrospective disagree about when the export change was first regression-tested. The rest of the answer, the `server-unit` Verify line, holds.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** T-107's As built now says `server-regression` first ran over the export change in the milestone's verify at c5832ab, before the rebase onto T-71, since that tree already held `TargetsPart` (e1beaf6).

### D-6's milestone list lost a comma in the fix

- **Where:** `docs/design/D-6-characters-rigging-animation-and-character-export.md:17` (rubric 7; RV-64 finding "D-6's Intent still lists morph targets as out of scope")
- **What is wrong:** 5b403eb changed "M-18 and M-19" to "M-18 M-19 and M-21". The fix dropped the separator.
- **Consequence:** A cosmetic slip in the design doc.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** D-6's list reads "[M-18](...), [M-19](...) and [M-21](...)" (4cf918c).

## Summary

- **Split** (round 2, two reviewers, both claude-opus-5-5, in parallel). Each checked RV-64's answers in its part against the code and the commits, and re-ran the stated mutations in a scratch copy.
  - **RV-64's tools, rendering and viewer answers.** 8 of 10 hold. The two `render_glb` answers each yield a finding: a refused weight with an engine reaches the agent only through one argument that no test guards, and several bad entries are still not all reported. It ran the original round-1 mutations: the rest mesh, `restHas`, and the pose without the morph each now fail a server-unit test. Moving the lock refusal outside the lock fails `ModelStoreTests`. It ran `scenario-morph` clean and with `glbclipweights`: the clean run passes, and the break fails with 11,258 px.
  - **RV-64's Core, persistence-and-export and whole-diff answers.** All hold, apart from two record slips and the retrospective not citing a verify after the tools fixes. It re-ran the layer-dropped mutation (25 core-unit fail, 12 in the edits tests), worst-triangle `Barycentric` (1 fails, the new knife test), and `TargetsPart` removed from `Skinned` (1 fails). core-unit (1131) and server-unit (810) passed.
- **Count:** 6 findings: 2 Minor, 4 Nits. No Major.
