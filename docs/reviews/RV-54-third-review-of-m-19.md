---
description: >-
  Third review of M-19
---
# RV-54 — Third review of M-19

**Status:** Answered

**Reviews:** M-19

**Round:** 3

**Previous:** RV-53

**Diff:** c42be19287854d691b19f8e636d99e18ec8a213b against 720a6fa

**Reviewer:** one Opus 5.5 subagent in a fresh context

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

## Summary

Reviewed by Opus 5.5 (claude-opus-5-5), in a fresh context that wrote none of this. The scope was `git diff d32a72e 45e95df` and RV-53's answers. I found nothing to report.

**RV-53's answers that hold: all 6.**
- **1, silhouette (e881a57).** `Silhouette.Read` skips a null face, keeps a null vertex slot and reads `{loop, holes}`. The slot index is the id, as `GetMeshTool.cs` writes it, so face ids index `at` correctly. `Predict` frames on live vertices only. A holed face is filled even-odd over its outer loop's box, and a plain face uses the fan as before. Moving `Foreground` into `AcceptanceScenario` (through `using static CrateBuild`) changes nothing. `SilhouetteTests` fails if the hole is filled. One small point, not a finding: the test's comment says it checks that the framing ignores the deleted slot. It cannot, because the origin lies inside the live box anyway. The `Vector3?` type is what enforces that rule.
- **2, render fixes (3c16e98).** The RenderGlb test checks the caption and a rest-frame difference past 0.5 %. The ShadingFile test checks skinned corners, flat faces in the normals look, and a case where skinned and own normals differ. I did not undo the fixes to watch the tests fail; I relied on the answer's report.
- **3, NotPlanarFold (1092901).** I traced both new failing cases against `NotPlanarFold.cs`. The shifted ids pass the count overload and fail `SequenceEqual(lowest)`. The late face reaches `foldSeen` with the counts still adding up to 13.
- **4, OrcwardLoad (ca47b0a).** `swapped-colours` is required, `orcward/3` has 3 such rows, and both comments are now true.
- **5 and 6, docs (45e95df).** D-6's and T-48's cells now read "not drawn". T-49 gets the dated luma line, line 47 is reworded, and its cross-references resolve. RV-52's change only adds blank lines before outcomes. RV-53's finding text is unchanged, with an outcome and a `**How:**` line on each.

**New problems: none found.**
- `tests.json` adds the two linked files to `server-unit` and `server-regression`, matching the csproj links.
- The new tests sit in the right tiers: `SilhouetteTests` is unit, the RenderGlb test is regression, the ShadingFile test is core unit, and the NotPlanarFold tests are unit.

**Ran:**
- `dotnet build Carve.slnx`: 0 warnings, 0 errors.
- `Carve.Core.Tests`: 1030 passed.
- `SilhouetteTests`: 2 passed.
- `NotPlanarFoldCheckTests`: 9 passed.
- `RenderGlbTests.With_no_clip_a_file_with_a_wrong_skin_draws_wrong_at_rest` (GPU): passed.
- `Carve.OrcwardLoad.Tests` with `CARVE_ORCWARD_RUN` set to `artifacts/scenarios/orcward/3`: 7 of 7 passed.
- `cairn lint`.

**Not checked:**
- the new tests failing with their fixes reverted;
- any scenario or acceptance run;
- the rest of `server-unit` and `server-regression`;
- the T-49 numbers against the copy's `differences.tsv`;
- `cairn milestone M-19 verify`.
