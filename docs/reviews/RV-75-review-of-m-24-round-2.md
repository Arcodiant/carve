---
description: >-
  Review of M-24, round 2
---
# RV-75 — Review of M-24, round 2

**Status:** Open

**Reviews:** M-24

**Round:** 2

**Previous:** RV-74

**Diff:** 65c7f4d3e3dd7049476d95d5f1e9b17d7698c5bb against 7d7752d

**Reviewer:** claude-opus-5-5, one reviewer checking RV-74's 19 answers and the fix commits

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### RV-74's "block rule written twice" answer reads fixed, but the block rule is still written twice

- **Where:** `docs/reviews/RV-74-review-of-m-24.md:218-220`; `scenarios/Carve.Scenario.Materials/UvImageFrame.cs` against `scenarios/Carve.Scenario.Bakes/BakesChecks.cs` (`CheckUvImage`, `Blended`) (rubric 7)
- **What is wrong:** The finding has two halves: T-121's block rule coded separately in the Materials frame and the Bakes checks, and the duplicated stage helpers. 28ae5ba fixes the helpers (`BakesChecks.GroupsOf`, `SurfaceStage.SteelColour`) and the summary of `UvImageFrame`. The block rule is not touched. The Fix says so ("In part ... left for a later pass"), but the Outcome is `fixed`. 9b23429 then changed the Bakes copy (`Blended` now reads each channel) and not the Materials copy, so the two have already drifted.
- **Consequence:** A reader of the outcomes sees the review's first-named defect as closed. The first half needs `skipped: <reason>`, or a follow-up filed off the milestone. The drift is the risk the finding named: a later change to the rule lands in one scenario and not the other.
- **Severity:** Nit

**Outcome:** —

### The retrospective says rulings and break counts are in each member's As built, but RV-74's ruling and its two breaks are in none

- **Where:** `docs/milestones/M-24-baked-maps.md:240` ("each recorded in the As built of the member named"), `:175` ("with the counts in each member's As built"), `:251-254`; `docs/tasks/T-126-curvature.md:89-90`; `docs/tasks/T-124-occlusion-and-the-orm-bake.md`, `docs/tasks/T-125-bevel-normals.md` (rubric 7)
- **What is wrong:** The person's RV-74 ruling (`blend: 1` on `wrench-orm-uvs-occlusion` and `wrench-uvs-normal`, with `Blended` reading each channel) is listed under the T-126 bullet. No task's As built records it, and no task file holds "blend: 1". T-126's As built still says only the curvature frames pass `blend`, "every other frame's check is unchanged". Yet T-124's and T-125's frames now gain a check they did not have. The counts for the new `dark` (2 failures) and `bevel-wide` (380 of 448, 2 strips) breaks are in the retrospective and the RV-74 Fix lines only, not in T-124's or T-125's As built.
- **Consequence:** The record the retrospective points to does not hold the ruling, and T-126's line reads as if the two frames still have no rule. Either record both in the As built of T-124, T-125 or T-126, or change the two retrospective sentences to say they live in RV-74.
- **Severity:** Nit

**Outcome:** —

### The partial-write error names the failed file by its absolute path and the others relative, with the inner `bake:` prefix

- **Where:** `src/Carve.Server/BakeTool.cs:272`, `:541-547` (`WriteStopped`); `src/Carve.Server/ToolPaths.cs:99`, `:138` (rubric 1)
- **What is wrong:** `WriteStopped` lists the written and unwritten files through `ReportedPath` (relative to the working folder, `/` separators). It prefixes `ToolPaths.Write`'s own error, which is `bake: could not write <FullPath>: ...`, an absolute Windows path with backslashes. The test pins it. One message reads `bake: could not write D:\...\out\strip.mask.h.png: it is a folder. The bake stopped there; written before it, ...: out/strip.mask.g.png; not written: out/strip.base_colour.png.`
- **Consequence:** An agent matching the failed file against the others, or against `path` in a normal answer, sees one file in two forms. The description says paths come back relative. It is cosmetic, and the message is otherwise right and tested.
- **Severity:** Nit

**Outcome:** —

### A new test was inserted between the padding test's summary and its method, so the summary now describes the clamp tie test

- **Where:** `tests/Carve.Core.Tests/BakeLayoutTests.cs:185-215` (1de2a41) (rubric 2)
- **What is wrong:** `/// <summary>Two islands, each covered texel valued by its index, padded, against a brute-force reading of the rule.</summary>` (line 185) now sits above `A_clamped_texel_as_near_two_faces_takes_the_lower_face` (line 187). `Padding_fills_rings_from_the_nearest_island_leaves_covered_texels_and_breaks_ties_by_index` (line 215), which it describes, has lost it.
- **Consequence:** The clamp tie test is documented as a padding test, and the padding test has no summary. Move the line back above line 215.
- **Severity:** Nit

**Outcome:** —

## Summary

I checked all 19 of RV-74's answers against their findings and the fix commits (`git diff 0a14afa..19432d2`). 18 hold. One is recorded as `fixed` but holds only in part: the block-rule duplication (first finding above). For the Core fixes I put the old code back in throwaway edits, ran the tests, then restored the tree. With BakeEdges.cs and BakeRaster.cs as of 0a14afa, `On_a_long_armed_l_block_only_the_inner_edge_is_concave` and `A_centre_on_an_islands_right_boundary_counts_as_a_centre` fail. With `<=` in pass 2's clamp comparison, `A_clamped_texel_as_near_two_faces_takes_the_lower_face` fails. With `Pad`'s `tieToQ` reversed, `Pad_matches_the_row_scan_it_replaced_bit_for_bit` and the existing padding test fail. I compared the reference test's scan to `Pad` as of 0a14afa, and it is that scan verbatim. For the server fixes, BakeTool.cs and RenderTool.cs as of 0a14afa fail all four new `BakeToolTests` and all three `UvImageArgumentTests` cases. Every one passes at HEAD, and I rebuilt at HEAD afterwards. I also read the new code for defects:

- **The `NearEdges` side test** (`cross(n2, run)` against `n1`). It is right for a non-convex face and for hole edges, which Carve winds clockwise, so the face lies left of every run.
- **`Pad`'s lower envelope.** The integer switch point, the pop rule on `start <= from[last]` and the first column's `long.MinValue` are right, and the tie goes to the lower row-major index.
- **`ToolPaths.FolderProblem`.** `'*'` is caught by the segment check.
- **The mask case-clash check, moved before the layout.** It names the same files as `Bake.Masks`, groups then live faces' materials.
- **The surface `known` list**, now only the materials the mesh uses.
- **`set_shading` used to read the wrench's hard edges.** `WithMode` keeps the hard edges, and the call adds only an undo step.
- **The per-channel `Blended`.** It is the same as before on a grey PNG. This is the loosening the person ruled, so it is not a finding.
- **The `dark` and `bevel-wide` breaks**, scenario-only as stated.
- **The `render_glb` control in `BevelStage`**, the same pair `BakesChecks.CompareExportAsync` uses.

No ruled threshold changed: 250, 50, 218, 64, 9°, `PredictedSlack` 2, `blend: 2` on curvature. The `WrenchBevelSeamTests` skip reason is true. The tier rule's `regression` is for a test that starts `carve` or needs a device, and this test does neither.

I did not run any scenario. So I did not check the counts the Fixes quote for scenario runs: byte-identical PNGs, the 22558/28344 placed pixels, the `dark`/`bevel-wide` failure counts, the control's 88205 pixels. I did not check the "76 PNGs" (3bd5c2f) against the "66 PNGs" (9b23429) either. `Pad`'s speed has no test, and I did not time it. The doc-only answers (T-121's Verify, the As built paths, the break table, the description of `path`) I checked by reading.
