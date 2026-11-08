---
description: >-
  Fourth review of M-4
---
# RV-23 — Fourth review of M-4

**Status:** Answered

**Reviews:** M-4

**Round:** 4

**Previous:** RV-22

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

## Summary

Round 4, on cce10c6 (docs only). `git diff 9968ee3..HEAD -- . ':(exclude)docs'` is empty, so no code changed since RV-22. No build or test was run; each break below was checked by reading the test against the code.

RV-22's two answers hold.

- **The looks at every size (Minor).** `The_looks_are_drawn_at_every_size` (`tests/Carve.Server.Tests/RenderTests.cs:870`) renders `mode: wireframe` with `highlight.edges [[4,5]]` at 256, 512 and 1024 and counts light grey and orange pixels, as the Retrospective line now says. Normals is rendered nowhere at 1024 (not in `RenderTests`, `CrateLooks.cs` or `ShedLooks.cs`), and its only 256 render (`RenderTests.cs:607`) asserts the caption alone, so "never rendered at 1024 nor pixel-checked at 256" is true. The scenarios' normals and wireframe frames are at 512 perspective.
- **The four breaks (Nit).** Each named break fails its test:
  - Parser: with `Fail` (`src/Carve.Core/MeshQuery.cs`) dropping a repeat on position alone, the `expected ')'` at character 15 or 5 is dropped, and `A_missing_bracket_is_listed_after_another_error_at_the_same_character` expects both errors.
  - -0: with `SingleToInt32Bits` on the raw float in place of `f + 0f` (`src/Carve.Core/MeshHealth.cs:180`), `(-0, 0, -0)` keys apart from vertex 0's `(0, 0, 0)`. `Minus_zero_and_zero_are_the_same_position` expects the one `coincident-vertices` problem, and nothing in `PolygonMesh.Create` or `TestMeshes.Make` normalises -0.
  - `views`: with the refusal back to `ContainsKey` (`src/Carve.Server/RenderTool.cs:170-175`), each of the four cases of `Views_with_null_single_view_arguments_or_zoom_1_is_a_sheet` has a `view` or `zoom` key, so each is refused and its caption check fails.
  - `PutLook`: the cleanup is now `LookHolder.Put`'s catch, and `RenderSystem` makes its meshes through it (`RenderSystem.cs:112-114`). With no destroy there, `A_failed_make_destroys_what_it_made_and_leaves_the_scene_as_it_was` leaves `fill#2` (and, at part 2, `edges#3`) in `Live`, against `["fill#1 Shaded"]`. Both cases fail.

No findings.
