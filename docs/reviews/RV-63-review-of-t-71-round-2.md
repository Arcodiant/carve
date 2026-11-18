---
description: >-
  Review of T-71, round 2
---
# RV-63 — Review of T-71, round 2

**Status:** Answered

**Reviews:** T-71

**Round:** 2

**Previous:** RV-62

**Diff:** aae3a7986b3db5a988ed2eb30e7b6b225004d86a against 7b4cd6e

**Reviewer:** claude-opus-5-5 (a fresh agent)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

No defects found. Checked every RV-62 fix in `59c1815`, and they are real and complete. For the `-0` fix: subdivide's `next` and `previous` stencils sum from their first corner exactly as the old `((double)a.X + b.X) / 2` did, in the same order. Subdivide's mean (`new Stencil(n)`), `FaceSplit`'s edge lerps (`Stencil.Lerp`) and barycentric points (`new Stencil()`) still sum from 0. These and `StandardLayers.Uvs` are the only producers of corner stencils and callers of `LayerRules.Lerp`. Position stencils (`Stencil.Apply`, now divided by a divisor of 1, which is exact) and the weights blend (`BlendWeights`, the same expression and order as the old `Stencil.Blend`) also match. A scratch test, since deleted, compared subdivide's carried UVs with the base formula bit for bit on all six faces of a cube, using `±0`, `±1e-30`, `±float.Epsilon` and ordinary values; they agreed. The new subdivide test fails without the flag (child 0's corner 1 would be `+0`). The deletion test now deletes a vertex and asserts `Deleted.Vertices` is `[0]`. Making `Stencil.Add` and the constructor internal breaks no caller: the build is clean and 1053 of 1053 unit tests pass. The read-only provenance views keep the `SortedDictionary` order. The copying init accessors run on `with` too. The new null checks cover only delegates and origins: no default value is checked, so the colour and material layers' `null!` defaults are still accepted. No standard layer passes a null rule. Across `git diff 7b4cd6e...HEAD -- src tests`, I also re-checked extrude (ring as offset, unweighted; cap by position; walls unmapped), bridge, merge, `FaceSplit` (reshaped and piece faces, weights, hard and seam halves) and subdivide (colour, material, groups, weights with an unweighted old vertex left unweighted, hard edges, seams). The carried values match the base code. Dropping empty groups in the groups writer cannot change output, because `define_group` and the file reader refuse an empty group.

## Summary
