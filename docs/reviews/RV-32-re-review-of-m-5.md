---
description: >-
  Re-review of M-5
---
# RV-32 — Re-review of M-5

**Status:** Answered

**Reviews:** M-5

**Round:** 2

**Previous:** RV-31

**Diff:** 90eb42a6fd65193f554df9978c4eee8a00813d3b against 7d957b4

**Reviewer:** two reviewers, each claude-opus-5-5: fixer A's answers and the product fixes; fixer B's answers, the scenario fixes and the retrospective

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: fixer A's answers and the product fixes (claude-opus-5-5)

19 of the 21 answers hold. Two do not hold in full: the knife's midpoint skip (FaceSplit's skip is still reachable through `loop_cut`), and the knife's boundary-touching refusals (a closed path still reaches the final validation). There is also one new Nit.

### FaceSplit still silently skips a cut whose midpoint is on the boundary, and `loop_cut` still hands it one

**Severity:** Minor

`src/Carve.Core/FaceSplit.cs:211` (`AlongBoundary`, midpoint only) with `src/Carve.Core/LoopCut.cs:118-122`. This is RV-31's "A cut segment whose midpoint lies on the boundary is skipped silently" (Core cut algorithms). That finding named `FaceSplit` as well as the knife ("The knife, and `FaceSplit` itself, need the same"). 8ae768a fixed the knife only. HANDOFF-A says FaceSplit's skip is safe because "the knife no longer hands it one, and plane_cut never did". That leaves out FaceSplit's third caller. `loop_cut` passes FaceSplit one straight edge-to-edge segment per quad and doesn't check it. On a concave quad, that segment can run along a side for part of its length. I ran this throwaway probe against HEAD (Core built from 3877800): the single quad `(0,0,0) (4,0,0) (1,1,0) (0,4,0)` (vertex 2 is reflex), then `LoopCut.Cut(mesh, (1, 2), [1/3])`. The cut runs from (3, 1/3) on edge 1-2 to (0, 4/3) on edge 0-3. That line is collinear with edge 1-2 from the start point to vertex 2, so its midpoint is on that edge. The result is `Succeeded`, `split: []`, new vertices 4 and 5 put into face 0's loop (`[0,1,5,2,3,4]`), and the face left whole. This is the same silent success-with-no-cut as the round-1 Major. `plane_cut` along the same line would cut the face. Related, in the same caller: at `at: [0.5]` from edge 0-1 the same quad is refused as `cuts[0]: the segment 4-5 runs outside faces[0]`. `LoopCut` does not map `cuts[k]` back to anything the agent gave, as the knife now does. Either `FaceSplit` should refuse (or split) a cut that runs partly along its boundary, rather than skip it on its midpoint, or `loop_cut` should check its segments as the knife now does. No test puts a concave quad under `loop_cut`.

**Outcome:** fixed

In 094714e. All 187 frames byte-identical on fa2d1ed.

### A closed knife path that grazes the boundary at one corner is still refused only by the final validation

**Severity:** Minor

`src/Carve.Core/Knife.cs:83-95`, the one-point-touch check, against the new corner stops in `Runs` (`:309-317`). This is RV-31's "A segment that touches the boundary inside its span is refused only by the final validation" (Core cut algorithms), and the person's ruling of 2026-11-13 recorded in T-67's As built: "Anything still impossible is refused in the knife's own facts-only words, naming the point or segment, never a face id that does not exist." The touch check counts only listed points that snapped to an edge or vertex (`snaps[i].Kind != Kind.Inside`). It does not count a corner that a segment passes through, which `Runs` now finds as a stop. Probe against HEAD: the notched face `(0,0) (4,0) (4,4) (3,4) (2.5,2.5) (2,4) (0,4)` (the review's own "grazed reflex vertex" face), with the closed knife path `(1,2.5) (3.5,2.5) (2.5,1)`. All three points are inside the face, and the first segment passes through reflex vertex 4. The touch check sees zero touches. The parts go to `FaceSplit`, and `PolygonMesh.Edit` refuses the pinched piece with `faces[0][8]: index 4 repeats faces[0][4]`. That names a corner of a would-be face, not the agent's points, and `Segments` doesn't rename it. When the same path lists the corner as a point instead, `(1,2) (2.5,2.5) (3.5,2) (2.5,1)`, it is refused in the ruled words: `points[1]: the closed path touches the face's boundary at one point only, at [2.5, 2.5, 0]; ...`. The check should count the corner stops as touches, naming the segment. `KnifeTests` has no closed path that touches the boundary only through a stop.

**Outcome:** fixed

In 54fe010. All 187 frames byte-identical on fa2d1ed.

### subdivide's description gives one tombstoned face point per deleted face, but each level makes one

**Severity:** Nit

`src/Carve.Server/SubdivideTool.cs:23`, new in cdb1b01: "when the mesh has a deleted face the face point made for it is in that block as a deleted slot". `MeshEdits.Subdivide` makes a tombstoned face point for each deleted face at each level. Probe: the unit cube with face 3 deleted, then `subdivide` 2. The appended block is 8..90 (83 slots, 81 in `NewVertices`), with tombstones at 11 and 29. So with `levels` 2 or 3, the block holds `levels` tombstones per deleted face, not one. The report itself is right: `created.vertices` is `{first: 8, count: 83}`, and `triangles` comes from `Face.TriangleCount`. Only the sentence undercounts. `SubdivideToolTests` checks `levels` 1 only.

**Outcome:** fixed

In 8d1d92b. All 187 frames byte-identical on fa2d1ed.

## Findings: fixer B's answers and the scenario fixes (claude-opus-5-5)

9 of the 10 answers hold. The Retrospective answer does not: the fix adds claims about work that was not done. The y-axis answer holds for what its finding asked, but leaves the -y tile with a gap (below).

### The Retrospective now credits the fix round with breaks and a fixer split that did not happen

**Severity:** Minor

`docs/milestones/M-5-cut.md:164,168`. This concerns RV-31 "The milestone Retrospective still says B-3 is open", answered in c4b7540. B-3, the 13 suites and the refactor are now right. The new paragraph adds three claims that are not:
- "Two Opus fixers fixed all 30". The Retrospective finding was the orchestrator's own fix (c4b7540), not a fixer's.
- "each with a break". Several fixes had no break and could not have one. The D-1 answer says "documentation only, no test" (6626bd2). Others with no break: the `CLAUDE.md` Layout rows (2d0aa12), the `tests.json` inputs (fc20c5c), the test rename (8b3ea61) and the Retrospective itself. `breaks-B.log` lists breaks only for C1, C2, G1 to G5 and K1, K2, K4.
- "6 of them Major: ... two frames whose checks could not fail, the y-axis tiles ...". RV-31 has one Major about a frame (`loop-cut-band-extruded`; the gatehouse cornice is a remark inside it). Its six are completed by `subdivide` counted twice (the server finding and the whole-diff finding). The list is right only if "two frames" stands in for the second `subdivide`.

The 187 frames do hold: 187 of 187 are identical at 6626bd2 (`frames-A/6626bd2.compare.txt` plus `cube-6626bd2-local.sha256`).

**Outcome:** fixed

In fa2d1ed: the six round-1 Majors are named as the five defects they were, each fixer's count is given (A 20, B 9, the orchestrator 1), the five text-only fixes with no break are named, and round 2 is added.

### The -y axis tile still has no check that its camera can fail, short of a swap with +y

**Severity:** Minor

`scenarios/Carve.Scenario.Gatehouse/GatehouseFrames.cs:173-178`. This concerns RV-31 "The +y and -y axis frames and their sheet tiles have no check worked from the geometry" (88644c5). The ray-test `Look` now runs on both Y tiles, with up -Z, which matches the product's `ViewFraming.UpFor` (`src/Carve.Server/ViewFraming.cs:142`). `FromAbove` makes +y fail on a swap or on a 180-degree wrong up, and G2 shows the swap failing.

The -y tile still cannot fail on its own orientation:
- From below the wall shows only faces 2 (the slab's bottom) and 17 (the cornice's underside). They share a normal, and the new grouping holds them to one shade.
- The cornice and the ring run the full width, so the silhouette is the bounds rectangle `x -1.5..1.5, z -0.15..0.23`. `Camera` centres it, so a 180-degree turn or a mirror maps it onto itself.
- So a -y rendered with +Z up, or mirrored, passes every probe: background, not-background and shade.

The same holds for an x-mirrored +y. The roof's slopes are in x and symmetric, and each roof face has no same-normal partner, so it is held only to its own first probe.

The commit message says "A swap of the two views, or a top view with the wrong up, fails both". Only the swap was shown failing in the scenario. K4 breaks the Core test's camera, not the product's.

**Outcome:** no change needed: the person ruled on 2026-11-13 to keep the approved frame and record this as a known limit; it is in the Retrospective's Never verified (fa2d1ed)

### The load test's "no corner where only a deleted vertex is" assert cannot fail

**Severity:** Nit

`tests/Carve.GatehouseLoad.Tests/GatehouseLoadTests.cs:55-57`. This is new in 3061e05, answering RV-31 "The load check's 'live triangles only' is measured against the product's own triangulation". `deletedOnly` is built with every key in `live` taken out, and the assert before it requires every index's position to be in `live`. So the `DoesNotContain(..., deletedOnly)` assert can fail only where the line above has already failed. The fixer says so in `breaks-B.log` (G5b), but the assert and its comment ("none is where only a deleted vertex is") stay as if they were a separate check. The answer itself holds: the face-worked count (G4, G5) and the live-corner set (G5b) are shown failing.

**Outcome:** fixed

In 944cb9e. All 187 frames byte-identical on fa2d1ed.

### The Y tiles' Core counts are held by nothing in the scenario, and their `sure >= 10` assert tests the InlineData

**Severity:** Nit

`tests/Carve.Core.Tests/GatehouseGeometry.cs:257-268` against `GatehouseFrames.cs:292-303`. `TheAxisTilesAlongYShowTheRoofFromAboveAndTheBottomFromBelow` works 46 sure probes and the faces "15 26 27" / "2 17". But `Look` compares `GatehousePlan.Expected`'s sure count and faces shown only at 512 px (`size == GatehousePlan.Size`). A 256 px tile gets only `tested < 10`. So nothing ties the Core numbers to the frames, although the test's summary says the scenario holds both frames to them. `Assert.True(sure >= 10, ...)` asserts the test's own `InlineData` argument, which is already pinned by the `Assert.Equal` before it, so it cannot fail.

**Outcome:** fixed

In 3613d2a. All 187 frames byte-identical on fa2d1ed.

## Summary

Round 2: a check of RV-31's 30 answers, and of the fix diff `caa149b..3877800` (43 files, about 800 lines), at `3877800`. It was split the way round 1's fixes were: fixer A's product answers (21) and fixer B's scenario answers plus the orchestrator's retrospective answer (10). 28 of the 31 answers checked hold. 7 findings: 0 Major, 4 Minor, 3 Nit.

- Two cases slipped past round 1's fixes: `loop_cut` on a concave quad can still make the silent no-cut, and a closed knife path grazing a corner gets the mesh's refusal, not the knife's.
- The retrospective overclaimed.
- The -y axis tile cannot be checked for orientation.
