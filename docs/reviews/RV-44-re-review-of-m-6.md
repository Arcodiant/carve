---
description: >-
  Re-review of M-6
---
# RV-44 — Re-review of M-6

**Status:** Answered

**Reviews:** M-6

**Round:** 2

**Previous:** RV-43

**Diff:** 66fc05b265bf387165b41ed950803a6e2f2c1512 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### The into element check is still worded per tool
**Where:** `src/Carve.Server/MeshToolSupport.cs:323` (`Single`, used by project_uvs's `into[k]`) vs `src/Carve.Server/MeshToolSupport.cs:333` (`UvNumber`, used by unwrap's `into[j]`); pinned at `tests/Carve.Server.Tests/ProjectUvsToolTests.cs:135` and `tests/Carve.Server.Tests/UnwrapToolTests.cs:99`
**Rubric:** 1 Correctness (RV-43's "Face and into errors are worded differently in each UV tool", answered `fixed` as "one face and into check in Core")
47559b6 moved the face check, the shape of `into` and its area into `UvArguments`, but an `into` number out of float range is still checked by two different Server helpers:
- project_uvs `"into":[0,0,1e99,1]` gives `into[2]: 1e99 is out of range: a number must lie within ±3.40282347E+38`;
- unwrap `"into":[0,0,1e39,1]` gives `into[2]: 1e39 is not finite: a UV must lie within ±3.40282347E+38`.

Both texts are pinned by the tests named above, so the same fault in the same argument still reads two ways across the two tools. `UvToolTests.The_uv_tools_word_the_face_and_into_refusals_alike` pins the shape and the area only. Found by reading the two `Into` parsers; not run end to end. A non-number element agrees (`into[2]: expected a number, got ...` in both).
**Outcome:** fixed

In da43862. One Core wording, `UvArguments.IntoElementRange`, through `MeshToolSupport.IntoNumber`, is used by both `project_uvs` and `unwrap`: `into[2]: 1e99 is out of range: an into element must be a finite number within ±3.40282347E+38`. Both pinned texts moved to it, still exact-prefix pins, and `UvToolTests.The_uv_tools_word_an_into_element_out_of_range_alike` gives both tools the same input and asserts the same full text.

### T-55's and T-56's As built still list the error texts the fixes replaced
**Where:** `docs/tasks/T-55-projections.md:71`; `docs/tasks/T-56-unwrap.md:154`
**Rubric:** 7 Tracker hygiene (As built matches what the diff does)
The fixes changed the wording of several refusals, but only T-58's As built was updated:
- T-55's As built gives `faces[5]: face 5 is deleted, so it cannot be mapped`. project_uvs now says `faces[5]: face 5 is deleted` (47559b6).
- T-56's As built gives `into: expected [u0, v0, u1, v1], four numbers, ...` and `into: [0, 0, 0, 1] has no area; give [u0, v0, u1, v1] with u1 > u0 and v1 > v0`. unwrap now says `into: expected 4 numbers [u0, v0, u1, v1], got 3` and `... has no area; u1 must exceed u0 and v1 exceed v0`.
- T-56's As built has no too-few-seams refusal (9f3c8cb) beside `no extent`.

These lists are what a dependant reads for the exact texts, so they now teach the old wording. T-54's list (`faces[5]: face 5 is deleted, ...`) still fits. Found by grepping `docs/tasks` for the old texts.
**Outcome:** —

## Summary

Round 2 of the review of M-6. It checks RV-43's 16 answers against the fix commits `91ececc..e3c9994` (27 files outside `docs/`), the outcomes, and the person's rulings named in them. Those rulings were not re-opened: the unified UV wording in Core, the too-few-seams refusal, the Carve file reader's wording left alone, the label backing clipped to the margin at 256 with the text in place, and the 6 glyph-top pixels on row 239 predicted as text.

**What was run.** The solution builds with warnings as errors. These tests pass at head:
- Core: the UV, unwrap, refusal-culture and engine-free tests, 105.
- Server: UvToolTests, ProjectUvsToolTests, UnwrapToolTests, UvLayout*, CheckerArgumentTests and the description and refusal tests, 124.

**Planted breaks.** Four breaks were planted locally, each restored afterwards (`git status` clean, rebuilt at head):
- Dropping `.WithSeams(halves, true)` in `FaceSplit.cs:592` fails all three cases of the seam-cut theory.
- Leaving the corner backing unclipped fails the 256 real-font test on exactly 442 pixels, and fails the 256 case of the clip theory. The 512 and 1024 cases still pass, so those sizes are unchanged.
- Never passing face labels to `UvLabels` fails the labels-drawn test ("no text in the face id's box").
- Dropping `!edge.IsManifold` fails the mis-wound test.

**Each RV-43 answer:**
1. Seam split by plane_cut, loop_cut or knife untested: **holds** (planted break fails all three cases).
2. Shared-edge overlap never reaches the clip: **holds**. The diagonal pair's bounds overlap, so the pair reaches the clip, and the case 0.01 apart is warned of.
3. One-seam cube refused as "no extent": **holds**. The too-few-seams refusal is used when the island has 3D area, and no-extent is kept for the collinear triangle. Both are pinned exactly, and unwrap's description names both.
4. Unwrap refusals in machine culture: **holds**. `UvArguments.Number` and `MeshEdits.Invariant` are used throughout, the de-DE test pins the margin and into texts, and float widening is pinned as 0.1.
5. project_uvs accepts a seam Core throws on: **holds**. There is one double `UvProjection.SeamAlongAxis`, and [1e-6,1,0] is refused by name. The old float test would accept it, so that row would fail. [2e-6,1,0] is mapped.
6. Ruled island cuts and refusals untested: **holds**. Mis-wound (planted), 3-face, did-not-converge (exact prefix and suffix) and no-extent are all tested. The outcome notes honestly that `Count != 2` is redundant with `IsManifold`.
7. Cylindrical shared scale untested: **holds**. The v span is pinned to height/(2π·mean r) × u span at 1e-6 relative, and v is asserted short of into's height, so a v fitted on its own fails.
8. Checker example transposed (looks reviewer): **holds**. Worked by hand on face 1 [4,5,6,7]: the upright list puts (0,0) at vertex 7 (top-left), and the mirrored list puts white at the top-right. The test reads both lists from the description and runs `check`.
9. Uvs-look labels not checked as drawn: **holds** (planted break fails).
10. Corner-label fit at 256: **holds**. The backing is clipped by `LabelLayout.Outside` and the text is not moved. The real-font test fails on 442 pixels when the clip is undone, and only labels that reach the square get more than one piece.
11. Checker example winding (whole-diff reviewer): **holds**. It is the same line and the same test as 8.
12. set_uvs throws on a negative face id: **holds**. `faces[-1]` and `clear[-1]` are refused by name, and the theory checks that the mesh and the undo step are unchanged.
13. Server suites miss CheckerColours.cs: **holds**. It is an input of both server-unit and server-regression.
14. CLAUDE.md samples row leaves out barrel.carve: **holds**. The row now names it, and its claims (version 5, `UnwrapSampleTests`, the scenario-uvs compare) were checked.
15. Face and into errors worded per tool: **does not hold in full**. The face check, the into shape and the into area are now one `UvArguments` in Core. Core stays engine-free (`System.Globalization` only). The wording reached set_uvs, project_uvs, unwrap and `MeshUvs.Check`, the file reader keeps its own text as ruled, and no pinned text was loosened (`MeshUvsTests` and `UnwrapToolTests` were tightened). But the into element range check is still worded per tool; see the first finding.
16. mark_edges "drawn in export": **holds**. The description now matches the code: `FaceSplit` carries hard edges as it carries seams.

**Read across the fix diff.** Nothing else new was found:
- set_uvs now returns its face problems before checking UVs, as project_uvs and unwrap already did.
- `OverlayLabel.BackingPieces` is drawn only for the uvs corner labels; every other label's backing is drawn as before.

The As built texts of T-55 and T-56 were not updated with the wording changes; see the second finding.

**Conclusion.** 15 of the 16 answers hold. One holds only in part (15): the into element range check is still worded per tool. There are two new findings, both small: that remaining wording split, and stale As built error lists in T-55 and T-56. The code fixes are sound, and each test does what its outcome claims. Where a break was planted, the test failed.
