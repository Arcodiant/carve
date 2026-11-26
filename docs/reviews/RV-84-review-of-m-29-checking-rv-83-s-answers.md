---
description: >-
  Review of M-29, checking RV-83's answers
---
# RV-84 — Review of M-29, checking RV-83's answers

**Status:** Answered

**Reviews:** M-29

**Round:** 3

**Previous:** RV-83

**Diff:** 40170f35eaf19467e91e4555e7872b3f417e485d against origin/main

**Reviewer:** one reviewer, claude-opus-5-5 (Claude Opus 5.5), checking RV-83's 15 answers against the code and reviewing the three fix members' commits, the render pump fix above all

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-83's answers and T-226's, T-227's and T-228's commits (claude-opus-5-5)

| RV-83 finding | Answered by | Answer |
|---|---|---|
| A model the pump shows while a render's `Resolve` of it lands | T-226 | holds |
| A slot that would refuse its output is served the base-colour bake | T-226 | holds |
| `Copies` still renames a user's `o@mesh` material | T-226 | holds |
| The render retry is tested on a copy of `RenderTiles` | T-226 | holds |
| The export-cancellation test bakes curvature at 2048 twice | T-226 | holds |
| `Unchanged` compares the files at two instants (skipped, T-229) | T-226 | holds: T-229 states the problem truly |
| The reworked title-note break still cannot fail on its own | T-227 | holds |
| check's new one-value reason says carve's check judges the real maps | T-227 | holds |
| The test that the crevice set holds only drive side faces runs in no suite | T-228 | holds |
| The milestone file's rulings leave out the three rulings of the review round | orchestrator | holds |
| Retrospective lines the fixes have made untrue | orchestrator | holds |
| T-168's table and comparison still call the old figures "the ruled set" | T-228 | holds |
| CLAUDE.md still lists the grip check as part of the steel's base colour bake | T-228 | holds |
| README's list of regression suites leaves out the new graphs-regression | T-228 | holds |
| T-220's As built names a commit that is not on the branch | T-228 | holds |

**The render pump:** The fault RV-83 described is closed. `ShowMesh` now reads the bake once (`RenderPump.cs:199`), and that one value sets what is drawn (`:204`), the mark (`:205`) and what `Step` compares later (`:201`). A `Resolve` landing after the read can only make `Step`'s comparison at `:174` differ, and every request passes that comparison before it is served. I checked this in a scratch copy: the AfterPeek test and the two retry tests fail with the old two-read `ShowMesh` put back and `Render`'s `Baked` taken out (3 failed, 4 passed), and all pass at HEAD. Each order I tried and what it does:
- **An edit at any point between `Resolve` and the serve:** the new model has no `Peek`, so its frame is marked and `Baked` draws again. That holds whether the edit lands before the post, after it (`:73-76` shows the newer model and returns), or between two requests in one step (the loop's next pass sees a new version).
- **A part, level, pose, morph-weights or bone change:** each one goes through `ShowMesh` and so takes a fresh read. A clip time is a pose.
- **A sheet:** every tile passes `:174`, and `OnOneMesh` catches tiles drawn on two versions. The retry is bounded at 3 inside 3 tries. Its error leaves the store as it was, since no write is made.
- **`render_glb`:** it draws no model and its request is never marked.
- **The `uvs` look:** it draws `UvImage` and checks by version, which every edit and every undo bumps.
- **The cache evicting an entry between the read and the draw:** the pump holds the resolved `Model`, whose `MaterialTexture`s carry their own copies of the PNG bytes (`GraphBakes.cs:144`). It holds the bytes, not a key it would look up again.
- **A `Resolve` that throws:** nothing is posted, and render answers with the error.
- **The hook:** `AfterPeek` is `internal` on an internal class, is set nowhere in `src` (grep), and is null in the product. Nothing in `src` overrides the now-virtual `RenderQueue.Post`.

The argument fails on one point. The single read tells "resolved at some time" from "never resolved", but it does not tell "resolved on the files this render read". So two orders hand back a frame with **stale** graph slots, unmarked: an undo that lands during a render, and two renders that straddle a graph save. Both are shown in the finding below. `Resolve` also re-reads the graph files when `Validate` runs, which is cheap (a parse, not a bake). Nothing else in the pump is asked twice.

### The pump's one read says whether the model was ever resolved, not whether it was resolved on this render's files, so an undo or a second render can return stale graph slots unmarked
- **Where:** `src/Carve.Server/RenderPump.cs:199-205` (the mark is `resolved is null`), `:174`; `src/Carve.Server/GraphBakes.cs:192-196` (`_drawn.AddOrUpdate` runs whatever finishes last, and runs even when `Unchanged` was false and nothing was cached); `src/Carve.Server/ModelStore.cs:396` (`Undo` publishes the old `Model` reference again); `src/Carve.Server/SheetVersions.cs:65`; T-226's `### As built` ("Two renders at once: ... it served it with its own resolution or a newer one, or marked")
- **Severity:** Minor
- **Rubric:** 1. Correctness (threading); `### As built` true of the code
- **What is wrong:** `Peek` answers with the last resolution stored for that `Model` reference. That can be one made on older graph bytes than the files this render read, and the frame is then served with `Unbaked == false`. I found two orders and ran both as probes in a scratch copy (`ProbeRv84Tests`, the real `RenderPump`, `SheetVersions.Render` and `GraphBakes`, no engine). Both passed, which shows the fault.
  - **(1) Undo.** M1 is rendered with `g.texgraph` grey. A `define_material` makes M2, and the graph is saved red. Then `render` runs, and an `undo` lands between its `Resolve(M2)` and its post (`BeforePost`). The pump shows M1, and `Peek(M1)` is M1's old grey resolution, so the frame is not marked. The frame returned is drawn with the grey PNG, not the red one the files now hold. `Unbaked` is false, and the answer's `baked:` line names the red bake it made for M2.
  - **(2) Two renders at once.** Render A's `Resolve(M)` reads and bakes the grey file (parked through the `clockMs` seam right after its `Png`). The file is saved red, and render B's `Resolve(M)` bakes red and stores its resolution. Then A finishes, and its `AddOrUpdate` replaces B's, because the keys differ. `Peek(M)` is now A's grey model (`Assert.Same` held), so B's frame is drawn grey, unmarked, while B's answer reports the red bake.

  Neither result lasts: the next render's `Resolve` stores a new key, and `:174` shows it again. Both need a concurrent call and a graph save, so this is narrower than RV-83's Major. But it is the "stale graph slots handed back unmarked" case. It is also the claim T-226's argument makes for two renders at once ("its own resolution or a newer one"), and that claim is untrue. Storing a resolution only if it is not older than the one stored, and having the pump compare against the resolution its request was posted after, would close it. For example, the request could carry its `Drawn` key, and the pump would mark the frame when the drawn key differs.

**Outcome:** skipped: filed off-milestone as B-12 by the person's choice (2026-11-26); it needs a concurrent call and a graph save, and does not last past the next render

### T-226's As built names a code commit that is not on the branch
- **Where:** `docs/tasks/T-226-rv-83-the-render-pump-the-bake-key-and-export-s-copies.md:77` ("Code commit `6543ceed`"), `:79`
- **Severity:** Nit
- **Rubric:** tracker hygiene (`### As built` true of the code)
- **What is wrong:** `git merge-base --is-ancestor 6543ceed HEAD` fails. The code commit on `milestone/M-29` is `2e09c51b` ("T-226: one bake read per mesh shown, ..."), rebased by `finish`. This is the same slip RV-83 found in T-220. The ids of T-227 (`079079d9`, `cc6708ee`) and T-220 (`45f7b023`) are on the branch.

**Outcome:** fixed

**Covered:** I read these whole: RV-83, T-226, the As built sections of T-227 and T-228, T-229, `RenderPump.cs`, `GraphBakes.cs`, `SheetVersions.cs`, `RenderQueue.cs`, `LookHolder.cs`, and `RenderSystem.cs` from its `Update` and from `Make` to the end. I also read the milestone file from "Decided while it ran" to the end, and the fix diff of every one of the 19 files. Of `RenderTool.cs` I read the two changed loops and the `image` loader. Of `ExportTool.cs` I read `Run`'s bake part, `Piece` and `Copies`. Of `GraphSlotBake.cs` I read `Key`, `PackingOf`, `Validate` and `Rgba`.

In a scratch copy (`%TEMP%\m29-rv84-o1`, `git archive` of HEAD) I built and ran these, unit tier only:
- the two probes above;
- the 5 `RenderBakedFrameTests` and the 10 `GraphBakesTests`, all passing;
- the same `RenderBakedFrameTests` with the old two-read `ShowMesh` and with `Render`'s `Baked` removed: the AfterPeek test and both retry tests fail;
- `weathering-unit`'s own command: `CreviceSetTests` passes in 228 ms.

I checked these by reading:
- the refusal comes before the cache for normal and occlusion, on render and export. Metallic-roughness has its own key, and base colour, emissive and the `uvs` image refuse only a missing output. `Validate`'s checks are those of `Rgba`, so no slot that baked is now refused;
- `Copies` and the piece path rename only `Drawn.Copies`;
- patina's new text against render's `image` (`{graph, output, size}`, mode `uvs`). An output that reads no mesh node keeps the rule's fix;
- the title break now subtracts two captures;
- `CLAUDE.md`'s six checks against the test code (2 and 12 texels, 30 and 10 luma, version 10, grip slack);
- the README's tier lists against `tests.json`'s 31 suites;
- T-168's table and comparison, and the milestone's rulings and retrospective lines named in RV-83.

I did not get to: whether a material change in the same `Update` as `:174`'s re-show reaches that frame in the real engine. That is the `continue` at `:177`, unchanged since T-163; the fake scene is synchronous, and I ran no engine. I also did not run `server-unit` or `server-regression` whole, run the patina tests or any scenario, or time the new `GraphExportTests`.

## Summary

Round 3: one reviewer, since the fix diff was small (19 files, about 420 added lines since `09271a55`) and one fix,
the render pump's, was the one the person asked to have re-reviewed. `origin/main` had not moved.

All 15 of RV-83's answers hold. The pump's fix holds for the fault RV-83 described: with the old two-read code put
back in a scratch copy the new tests fail. 2 new findings: 1 Minor (the pump's one read says a model was ever
resolved, not that it was resolved on the graph files this render read, so a frame with stale, not empty, graph slots
can come back unmarked when an undo or a second render straddles a graph save) and 1 Nit (a commit hash in an As
built).
