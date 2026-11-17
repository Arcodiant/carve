---
description: >-
  Review of M-7, round 2
---
# RV-48 — Review of M-7, round 2

**Status:** Answered

**Reviews:** M-7

**Round:** 2

**Previous:** RV-47

**Diff:** 188078deaa5e6c407ef7c95011a4bb6d921c6622 against 5bc4559

**Reviewer:** claude-opus-5-5 (one reviewer: RV-47's answers and the fix diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-47's answers and the fix diff (claude-opus-5-5)

### No test fails if RenderSystem stops calling Keep, or calls it with the wrong set

**Where:** `src/Carve.Server/RenderSystem.cs:222-223`, `tests/Carve.Server.Tests/PbrReleaseTests.cs:46-92`
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

The person ruled that render releases the textures and materials no model uses. `PbrReleaseTests` drives `PbrMaterials.Keep` by hand in the test host, so it shows that `Keep` works. It does not show that `render` ever calls it. These changes would all leave every suite passing:
- deleting `_pbr?.Keep(_materials.Concat(_drawn))`;
- passing `_materials` alone;
- moving the call to before the entity's materials are swapped. That would destroy textures the entity still draws, which the engine calls undefined (`resource-lifetime.md`: "CPU handles are not tracked").

`MaterialRenderTests` redefines `leaf` between two renders (lines 294 and 302), but releasing an untextured material changes no pixel, and nothing reads a count. The outcome and the Retrospective say this gap exists, but saying so does not cover the ruled behaviour. One real-`carve` test would close it: redefine a textured material, render, and read `IGraphicsResourceManager.Residency`.

The release logic itself has no defect:
- `Keep` runs only in `Install`, after the entity holds the new materials.
- `_drawn` is always taken from the `_materials` the same `Make` resolved against.
- A material shared by two meshes, or used by a posed `Combined` mesh, is in `_materials` and so is kept.
- A material redefined between renders is released only once the entity draws the new one.
- The equality test's host builds `PbrMaterials` with no residency and never calls `Keep`.

**Outcome:** fixed

**Fix:** RenderReleaseTests (Tier regression) runs the real carve with the debug extension and a Tiny scenario mode `Tiny:Mode=release`, which reads `IGraphicsResourceManager.Residency` from DI with no product hook. It walks a face from plain to PNG A to PNG B to the UV look and back, measuring textures and the material table at each stage. Breaks: the `Keep` call deleted, given `_drawn` alone, or moved before the swap each fail. Given `_materials` alone it passes, because at every Install `_drawn` is a subset of `_materials`, so no reachable state tells them apart. Carve.Scenario.Tiny now references Arcodiant.Empyrean.Graphics (fixer, Opus 5.5, 343eb56)

### "Every existing frame stayed byte-identical" rests on a table taken before render's release change

**Where:** `docs/milestones/M-7-materials-and-textures.md:270`, `docs/tasks/T-80-textured-barrel-scenario.md:151`, `G:\My Drive\artifacts\carve\M-7\RV-47\evidence\README.txt`
**Rubric:** 4 Honest Verify scope / 7 Tracker hygiene
**Severity:** Minor

The evidence README says `new.sha256` came from the `fix/RV-47-scenario` tree, which is fixer B's branch. Fixer B's commits (`3a9c79c` to `0e0ecae`) come before fixer A's. Fixer A's `68f810e` was committed at 20:43, after the evidence (20:23), and it changes what every render does: an asset holder per material, and `Keep` on every `Install`. So the 229-frame table never saw the head's render code.

After its rebase, fixer A re-ran core-unit, server-unit, scenario-uvs and scenario-materials. Those check pixels against the geometry, not against base hashes, and they leave out `server-regression`, which holds the new `PbrReleaseTests` and the 87 server frames. The Retrospective's sentence presents the frame guard as covering the head, and it does not. A gate `verify` on `7f86436` was running at review time. Either compare that run's frames with `base.sha256`, or reword the sentence to say which commit the table covers.

**Outcome:** fixed

**Fix:** The guard was re-taken at 3f9ba34 on fix/RV-48 (its code is identical to the landed 3893cf6), after 68f810e. All 229 scenario frames hash the same as base.sha256 (307d692), in head.sha256. server-regression's 120 test frames (not 87) hash the same at 307d692 and at the head, in server-base.sha256 and server-head.sha256, and no frame is new. README.txt in `G:\My Drive\artifacts\carve\M-7\RV-47\evidence\` says which commit each table covers. The Retrospective now cites the head table (fixer continuation, Opus 5.5; orchestrator)

### The Retrospective still says a member looked at T-80's frames, so the no-change reason for finding 18 is false

**Where:** `docs/milestones/M-7-materials-and-textures.md:228`, `:287`; `docs/tasks/T-80-textured-barrel-scenario.md:152`; `docs/reviews/RV-47-review-of-m-7.md:277`
**Rubric:** 7 Tracker hygiene
**Severity:** Minor

RV-47's outcome says no change is needed because "T-80's earlier agents opened no frame, and the Retrospective says so". The Retrospective does not say so:
- Line 228 still reads "Every look above is a member's or the person's".
- Line 287 says the frame reviews in T-78 and T-80 "both ... were done by a fresh agent with room to spare".

T-80's As built (line 152) says agent 3 ("guard and frame review") sent the frames to Waystone and "Frames were not opened by this agent". So no member agent looked at T-80's ten frames, and the Retrospective still implies one did. The person's own review stands. The record is what is wrong, not the frames.

**Outcome:** fixed

**Fix:** The Retrospective now says no T-80 agent opened its frames, and that the person's look was the only one on them (orchestrator, dbb7bb7)

### The Retrospective's count of who answered what does not add up to 25

**Where:** `docs/milestones/M-7-materials-and-textures.md:268`
**Rubric:** 7 Tracker hygiene
**Severity:** Nit

The Retrospective says "Fixer A fixed the 14 product findings. Fixer B fixed 6 ... The orchestrator answered the other three." That is 23. By RV-47's own Fix lines, fixer A answered 16 findings, 15 distinct defects with the duplicate `set_material` one. Fixer B answered 6 and the orchestrator 3, so the total is 25.

**Outcome:** fixed

**Fix:** The count now reads 16, 6 and 3 (orchestrator, dbb7bb7)

### T-78's Plan and As built still say render's materials and textures are held for the process

**Where:** `docs/tasks/T-78-materials-in-render.md:124`, `:240`
**Rubric:** 7 Tracker hygiene
**Severity:** Nit

`68f810e` makes `PbrMaterials` release what no model uses. T-78 is the member that owns `PbrMaterials`, and its As built still ends "Engine materials are cached by the Carve record ... for the process". Its Plan, line 124, still says "Both are held for the process, like `Checker`". RV-47 updated T-76's and T-80's As built, but not T-78's. The code's own summary was updated (`PbrMaterials.cs:12-13`).

**Outcome:** fixed

**Fix:** T-78's Plan notes that 68f810e changed this, and its As built describes the holders, `Keep` in Install, and the two release tests (fixer, 3893cf6)

### "Eight frames" and the barrel link are still named after barrel-bottom was added and BarrelSpec unlinked

**Where:** `scenarios/Carve.Scenario.Materials/MaterialsScenario.cs:55`, `scenarios/Carve.Scenario.Materials/Carve.Scenario.Materials.csproj:15`, `docs/tasks/T-80-textured-barrel-scenario.md:3`, `docs/milestones/M-7-materials-and-textures.md:139`, `docs/changelog/UNRELEASED.md:9`
**Rubric:** 7 Tracker hygiene
**Severity:** Nit

`CLAUDE.md` now says nine frames, but the scenario's class summary still says "seen in eight frames". So do T-80's description, the milestone's member line, and the changelog entry that `finish` will carry to `main`. The csproj comment still opens "Linked, never copied: the Uvs scenario's barrel and its mesh reading", but `3a9c79c` removed the `BarrelSpec.cs` link and only `UvsScene.cs` is left.

**Outcome:** fixed

**Fix:** The words are now nine frames in MaterialsScenario.cs's summary, the csproj header, T-80's description and UNRELEASED.md, and the milestone's member line (dbb7bb7); the csproj link comment no longer names the barrel (fixer, 3893cf6)

### save's new sentence says the texture was read by define_material, but open reads it too

**Where:** `src/Carve.Server/SaveTool.cs:78`
**Rubric:** 1 Correctness (tool-description content)
**Severity:** Nit

The description says the warning comes for "each texture whose PNG has been moved or deleted since define_material read it". `MissingTextures` checks every held texture, and `open` reads each PNG again as well. After `open` and a deleted PNG, `save` warns of a texture that `define_material` never read. "since define_material or open read it" would match the code.

**Outcome:** fixed

**Fix:** save's description now reads "since define_material or open read it" (fixer, 2bf6e40)

## Answers checked

24 of RV-47's 25 answers hold. The one that does not is "T-80's own agents did not look at the frames": its reason says the Retrospective records that T-80's agents opened no frame, but the Retrospective still implies a member looked (the third finding above). Two hold with a note: the render caches (the logic is sound; its call site is untested, the first finding above), and the caption exclusion in `Pool` (its break shows the exclusion is live, not that it is per tile, as the outcome says).

## Summary

Round 2 of M-7's review. One reviewer, claude-opus-5-5, read RV-47's 25 answers against the fix diff `307d692..7f86436` and reviewed that diff for new defects. One reviewer was enough: the fix diff is small, and its one risky part, render's cache release, was read closely (shared, posed and redefined materials, and the equality test's host) and found sound. The reviewer built and ran nothing. Its harness refused its write of a findings file, so it returned the findings in its report, and the orchestrator copied them here unchanged in substance.

7 findings: 0 Major, 3 Minor, 4 Nit. None is a code defect. One is a missing test of the release call site; the rest are records and wording.
