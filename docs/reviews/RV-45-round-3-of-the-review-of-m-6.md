---
description: >-
  Round 3 of the review of M-6
---
# RV-45 — Round 3 of the review of M-6

**Status:** Open

**Reviews:** M-6

**Round:** 3

**Previous:** RV-44

**Diff:** 0aeb76524474b140c167643b02d94dee8960cec7 against origin/main

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

### T-54's As built still quotes set_uvs's old deleted-face text
**Where:** `docs/tasks/T-54-set-uvs-seams-on-mark-edges-and-get-mesh.md:70`
**Rubric:** 7 Tracker hygiene (As built matches what the diff does)
T-54's set_uvs error list gives `faces[5]: face 5 is deleted, ...`. That was the old `MeshUvs.Check` text, `face 5 is deleted, and a deleted face holds no UVs`. 47559b6 removed it (`git diff 91ececc^ HEAD -- src/Carve.Core/MeshUvs.cs`). set_uvs now words the face through `UvArguments.FaceProblem` (`SetUvsTool.cs:100`), and the whole text is `faces[5]: face 5 is deleted`. `UvToolTests.cs:159` pins it with `Assert.Equal`. The `, ...` tells a dependant that a clause follows, but none does. RV-44 called this list "still fits", and 6a10430 left T-54 alone. T-54 has not changed since `6f3adce cairn: finish T-54`. Found by grepping the code for each face text quoted in the T-53 to T-62 As builts. This is the only stale quote I found. T-53:236's `1e39 is not finite ...` belongs to the Carve file reader, which keeps its own text as ruled.
**Outcome:** —

### UvNumber's summary still names unwrap's into
**Where:** `src/Carve.Server/MeshToolSupport.cs:328`
**Rubric:** 1 Correctness (anything the change breaks elsewhere)
The summary reads `One number of a UV (set_uvs, unwrap's into)`. Since da43862, unwrap's `into` goes through `IntoNumber`. `UvNumber`'s only caller is now `SetUvsTool.cs:154`, a UV pair element. Someone who believes the comment and changes `UvNumber`'s text expecting unwrap to follow would split the into wording again. Found by grepping for `UvNumber(` callers.
**Outcome:** —

## Summary

Round 3 of the review of M-6. It checks RV-44's two answers against the fix commits `da43862` and `6a10430` and the person's ruling (2026-11-14) that the UV tools' error wording is unified in Core. I did not reopen the ruling.

**What was checked.**
- Read both fix diffs.
- Grepped `src/` for every helper that words an `into` element, and for every error text quoted in T-55's and T-56's As built.
- Grepped `docs/tasks`, the tool descriptions and `CLAUDE.md` for the texts RV-43 and RV-44 replaced: `so it cannot be mapped`, `four numbers, got`, `give [u0, v0, u1, v1] with u1 > u0`, `1e39 is not finite`, `a UV must lie` for into, `drawn in export`, and `a deleted face holds no UVs`.
- Ran the tests at head; all pass. Server UvToolTests, ProjectUvsToolTests and UnwrapToolTests: 71. Core UV, unwrap and engine-free tests: 102.
- `git status` was clean after the run.

**Each RV-44 answer:**
1. The into element check worded per tool: **holds**.
   - `UvArguments.IntoElementRange` in Core is the one text. `MeshToolSupport.IntoNumber` is its one caller, used by `ProjectUvsTool.cs:82` and `UnwrapTool.cs:100`.
   - The new `The_uv_tools_word_an_into_element_out_of_range_alike` gives both tools `[0,0,1e99,1]` and asserts the same full text with `Assert.Equal`.
   - Both moved pins are tighter, not looser. They were `Assert.Contains` on a prefix and are now `Assert.Contains` on the full text. Unwrap's pin still uses 1e39, the float-only overflow.
   - No other check was lost. The non-number text is unchanged. Margin and the other numbers keep `Number`, and set_uvs keeps `UvNumber`, each for its own argument.
   - Core stays engine-free: `UvArguments` uses `System.Globalization` only.
   - `UvNumber`'s summary was left stale; see the second finding.
2. T-55's and T-56's As built quote old texts: **holds**. Each quoted text matches what the code emits now:
   - the face checks from `UvArguments.FaceProblem`;
   - `IntoShape` and `IntoElementRange`;
   - margin and fit from `Unwrap.cs:53,68`;
   - not-a-disc from `UnwrapIslands.cs:154`;
   - too-few-seams and no-extent from `Unwrap.cs:219,222`;
   - did-not-converge from `UnwrapLscm.cs:155`;
   - project_uvs's own method, view, option, direction and seam texts in `ProjectUvsTool.cs`.

   T-56 now lists too-few-seams. But the same kind of stale quote remains in T-54; see the first finding.

**Read across the fix diffs.** Nothing else new. The tool descriptions paraphrase rather than quote ("an into that is not four finite numbers"), and `CLAUDE.md` quotes no UV error text.

**Conclusion.** Both RV-44 answers hold. The into wording is one text from one place, pinned alike for both tools, and no pin got looser. There are two new findings, both small and documentation-only: T-54's As built still quotes set_uvs's old deleted-face text, and a stale summary on `UvNumber`. Neither changes behaviour.
