---
description: >-
  Third review of M-4
---
# RV-22 — Third review of M-4

**Status:** Answered

**Reviews:** M-4

**Round:** 3

**Previous:** RV-21

**Diff:** 17b1f71f3583686f07c714d5d482f9d3773a70da against origin/main

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

### The Retrospective says the looks are pixel-checked at every size, but only the wireframe is; normals is never drawn at 1024 (Minor)

`docs/milestones/M-4-ask-the-mesh.md:212` (rubric point 7, tracker hygiene) now reads "`The_looks_are_drawn_at_every_size` checks the looks' edge and highlight pixels at 256, 512 and 1024", and the line it replaced, "No look was rendered at 1024", is gone. The test (`tests/Carve.Server.Tests/RenderTests.cs:870-888`) renders only `mode: wireframe` with one highlighted edge, and counts light grey and orange pixels. The normals look is not in it. No scenario renders normals at 256 or 1024 (the shed's and crate's 1024 and 256 frames are shaded or labelled), and the one normals render at 256 (`RenderTests.cs:607`) is a caption test. So "No look was rendered at 1024" is still true of normals, and the Retrospective now claims it covered. T-25's As built (line 168) words the same fact correctly: "counts the wireframe's light grey edges and an orange highlighted edge". The Retrospective line should say the same: the wireframe and an edge highlight at all three sizes, and normals still at 512 only.

**Outcome:** fixed

*Fix* (orchestrator): the Retrospective says the test renders the wireframe with one highlighted edge at the three sizes, and that the normals look is never rendered at 1024 nor pixel-checked at 256.

### "each new check shown failing" claims more than RV-20's answers record (Nit)

The Retrospective's review paragraph (`docs/milestones/M-4-ask-the-mesh.md:240`) says the fixers added each new check "shown failing". RV-20's *Fix* notes do say this for most fixes, but not for all of them. The parser's two-errors-at-one-character tests ("both cases are tests"), the -0 test ("Test added"), the four-case `views`-with-nulls theory and the `PutLook` failed-`CreateMesh` unit test have no recorded break. T-22, T-23, T-25 and T-26's *Since RV-20* notes record none either. Either drop "each" or name the fixes that were shown failing.

**Outcome:** fixed

*Fix* (orchestrator): the claim is true but was not recorded. The product fixer's break table shows each of the four failing (a repeat dropped on position alone; raw-bit comparison; refusal by key presence; no destroy in the catch); RV-20's four *Fix* notes now say so.

## Summary

Round 3, on `git diff 3fe6f20..HEAD`: code in 9968ee3 (`src/Carve.Server/RenderTool.cs:127` and `:139`, `src/Carve.Core/MeshHealth.cs:11`), docs in f3c583f (the milestone's plan bullet, *Never verified* list and review paragraph, and T-25's As built line 165). The build is clean (0 errors). No GPU suite was run.

RV-21's three answers hold.

- **The `render` sheet retry (Nit).** The new error-list entry in `RenderTool.cs:139` quotes `SheetVersions.cs:23` character for character, with `attempts` = `SheetAttempts` = 3 (`RenderTool.cs:302`). It is the same string `SheetOnOneMeshTests.cs:45` pins. The sentence at `:127` ("up to 3 tries in all, and if the mesh changed on each of them it is a tool error") matches the loop and T-26's As built ("up to three tries, then a tool error"). README and D-1 do not mention the retry, so nothing else had to change.
- **The `inside-out` threshold (Nit).** The `MeshShell` comment's "below -1e-6 x the shell's bounding-box diagonal cubed" matches `IsInsideOut` (`volume < -InsideOutFraction * diagonal^3`, `InsideOutFraction = 1e-6`, with the diagonal taken over the shell's own vertices). The milestone's plan bullet now carries the amendment, with a marker. The threshold reads the same in T-23's Scope and Notes, in `check`'s description and in the code. No other place says "negative means inside-out".
- **The stale lists (Minor).** All five false statements RV-21 named are corrected, and T-25:165 now says the rewritten test is shown failing. The stray vertex has its own "cannot run" line, which matches RV-20's skip.

Nothing outside those lines changed: the non-docs diff is the 3 lines above.

I checked the Retrospective against RV-20 as well. The counts are right: 23 findings (3 Major, 15 Minor, 5 Nit), 22 fixed and 1 skipped, the three rulings and the 63 frames (20 + 33 + 10). Two claims go beyond the record, and both are findings above. "The looks' edge and highlight pixels at 256, 512 and 1024" covers the wireframe only, so normals is still unrendered at 1024 (Minor). "Each new check shown failing" is not recorded for four of the fixes (Nit). 2 findings: 1 Minor, 1 Nit.
