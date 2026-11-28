---
description: >-
  Review of M-41, round 2
---
# RV-101 — Review of M-41, round 2

**Status:** Answered

**Reviews:** M-41

**Round:** 2

**Previous:** RV-100

**Diff:** d49c200bd157498e80f1e3918b4f0362eb09e6e1 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: round 1's answers and the fixes (claude-opus-5-5)

### `Predicted` under-counts narrow faces by up to about 1.85x, so a call far over the budget gets through

`src/Carve.Core/Tessellation.cs:38-54`, checked at `:65-68`. Rubric point 1 (the person's ruling 2 of 2026-11-27).

`Predicted` counts each edge's points plus the face area over 0.866 max_edge². It does not count the points `cdt.Refine` adds in the strip along the loops, and on a face a few `max_edge` wide that strip is most of the face. Release, measured from HEAD:

| Face | `max_edge` | Predicted | Made | Time |
|---|---|---|---|---|
| unit quad | 0.0025 | 186,348 | 191,085 | 2.9 s |
| strip 40 x 0.01 | 0.0025 | 105,905 | 196,325 | 6.1 s |
| strip 125 x 0.004 | 0.0025 | 192,376 | 268,339 | 21.8 s |
| strip 75 x 0.01 | 0.0025 | 198,568 | 368,134 | 13.8 s |

The last two pass the check. The only test of `Predicted` is on the unit quad.

**Outcome:** fixed

Within the person's ruling 2. `Predicted` now adds `Tessellation.StripPoints`, 3.5 points per `max_edge` of each selected face's loops (measured at 0.8 to 3.0). On the table: unit quad 191,948 predicted (191,085 made), 40 x 0.01 217,933 (196,325), 125 x 0.004 542,387 (268,339), 75 x 0.01 408,596 (368,134); made counts are this table's, the algorithm being unchanged, and the last three are now refused (the 40 x 0.01 strip, under the budget when made, is refused by the margin). A hard stop also refuses, with the budget error and before anything is committed, a call whose made vertices would pass the budget (after the edge points, after the lattice, or in `ConstrainedDelaunay.Refine`'s new point cap). Tests: `The_prediction_is_not_under_the_vertices_made` on scaled-down copies (a 0.25 quad, strips 2 x 0.01, 2 x 0.004 and 3 x 0.01 at 0.0025, each the table's width in `max_edge`), and `A_call_that_makes_more_than_the_hard_stop_is_refused_and_changes_nothing`, with the stop set under the made count through an internal overload. In 9f5722ad (claude-opus-5-5).

The margin stands by Waystone's filter's answer of 2026-11-27 (for the person to confirm): it refuses some calls that would have fit, such as the 40 x 0.01 strip (196,325 made, about 218,000 predicted), refused at once with no work done; the hard stop is only a backstop. Pinned by `The_40_by_0_01_strip_that_would_fit_is_refused_at_once_by_the_margin`.

### `split` at summary still grows with the number of faces, so a whole-mesh tessellate breaks the 2,000-character budget

`src/Carve.Server/MeshToolSupport.cs:161-162`, `src/Carve.Server/TessellateTool.cs:77`. Rubric point 1 (M-23's ruling 5).

`split` has one entry per face cut, and no selection means every face. Failing case: 40 unit quads, every face at 0.3, gives a summary `split` of 3,463 characters across 40 entries. README:160-163 says every summary result is within the budget, and `DenseMeshBudgetTests` does not call tessellate. The ruling covered `pieces` only.

**Outcome:** fixed

By Waystone's filter's answer of 2026-11-27 (for the person to confirm): at summary `split` is `{count, first}`, the number of faces cut and the first 10 entries `{face, pieces:{count, first}}`, through the same `MeshToolSupport.Split`/`Moved`; the whole list at `full`. `A_whole_mesh_of_40_faces_at_summary_is_within_the_result_budget` (40 unit quads at 0.3, every face) holds the result to 2,000 characters. The description, README, `TessellateToolTests` and `VerbosityToolTests` updated; the Cuts `TessellateStage` already asks for `full` and is unchanged. In 9f5722ad (claude-opus-5-5).

### "cannot be tessellated" can be triggered, so round 1's reason for leaving it untested does not hold

`src/Carve.Core/ConstrainedDelaunay.cs:78`, `:90`, `:321`, reported at `Tessellation.cs:119`. Rubric points 1 and 2.

Thin strips not aligned to the face's axes are refused. Each is a single quad in z = 0 at `max_edge` 0.002:

| Strip | Angle | Message |
|---|---|---|
| 20 x 0.002 | 10° or 30° | "the triangles are not linked" |
| 20 x 0.002 | 45° | "a constraint could not be recovered" |
| 7 x 0.002 | 10° | "a constraint could not be recovered" |
| 10 x 0.002 | 10° | refused the same way |

The same strips axis-aligned, or 1 to 5 m long, succeed, so the failure starts at about 3,500:1 on a rotated face. The likely cause, not traced, is the tolerance `_eps = 1e-12 * size²` scaled to the face's box. The refusal changes nothing, but its message is internal.

**Outcome:** fixed

By Waystone's filter's answer of 2026-11-27 (for the person to confirm): the refusal is now `faces[N]: is too thin for max_edge M; give a larger max_edge, or cut the face across first`, a tool error that changes nothing, pinned by `A_thin_strip_turned_off_the_axes_is_refused_plainly` (20 x 0.002 at 10°, `max_edge` 0.002). The triangulation's tolerance is unchanged; the cause is filed off-milestone as B-15. In 9f5722ad (claude-opus-5-5).

## Summary

One reviewer, claude-opus-5-5, who wrote none of the code and did not review round 1. Its verdicts on RV-100's nine answers:

- **All nine hold** as far as each answer goes.
- **Answer 1 and 9**, the budget: holds as a refusal. The bound is too loose on narrow faces, which is the first finding above.
- **Answer 5**, short `split`: holds for `pieces`. The number of faces is still unbounded, which is the second finding above.
- **Answer 8**, the description's errors: the reason given for not testing "cannot be tessellated" does not hold, which is the third finding above.
- **The new filter** keeps the same points by construction: the same row y, the same x sequence, the same crossing expression and the same `SegmentDistance` calls.
- **Nothing was loosened** beyond what was ruled.
- **The test-only `InternalsVisibleTo`** is within CLAUDE.md's rules, which forbid internals only to extensions.

It ran the Core Tessellate and Displace tests (30) and 126 server tests. It did not run the scenarios or the full suites, did not profile the 21.8 s strip call, and did not trace the cause of the triangulation failure.
