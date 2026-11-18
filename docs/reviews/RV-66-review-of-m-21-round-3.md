---
description: >-
  Review of M-21, round 3
---
# RV-66 — Review of M-21, round 3

**Status:** Answered

**Reviews:** M-21

**Round:** 3

**Previous:** RV-65

**Diff:** 4590f1e1f6a3382d85a5d74ff5d387bd0b677a3b against 802e486

**Reviewer:** claude-opus-5-5 (one reviewer, checking RV-65's answers)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-65's answers (claude-opus-5-5)

### With an unreadable `.glb`, `render_glb` drops a bad weight from its refusal, and with a type error it lists entries out of `render`'s order

- **Where:** `src/Carve.Server/GlbScene.cs:66-73` (the `loader.Parse` catch returns before the targets loop, so `weightRefusals` is never added) and `src/Carve.Server/RenderGlbTool.cs:91-95`, `:117`; `docs/tasks/T-108-*.md:80` (rubric 1, 7; RV-65 finding "`render_glb` still differs from `render`/`check` when one call has several bad targets, and a bad weight now comes after a path error")
- **What is wrong:**
  - **Unreadable file.** The Fix and T-108's As built say a type, non-finite or out-of-range weight is told before a path error, and that one call tells every bad entry. That holds for a missing path, a folder, an IO error and no engine, because those branches go through `known()`. It does not hold when the bytes are read but the engine's loader cannot parse them: `Prepare` adds only `path: the engine's loader could not read the file: ...` and returns. On the real engine, with 8 junk bytes in `bad.glb`, `{"Bulge":-0.5}` gives only the path line, and `{"Bulge":2,"Other":"half"}` gives Other's type line and the path line but not Bulge's weight.
  - **Order.** `render` and `check` tell entries in object order. `render_glb` puts every type error before every weight or name line. For `{"Bulge":2,"Other":"half"}`, `render` and `check` say Bulge's weight, then Other's type; `render_glb` says Other's type, then Bulge's weight. Each line's text matches.
- **Consequence:** For an unreadable file the agent fixes the file and only then learns its weight was refused, the extra round trip RV-65's finding meant to remove, and against the As built's stated order. The order difference is cosmetic. Nothing is drawn in either case.
- **Severity:** Nit

**Outcome:** skipped: the person ruled (2026-11-18) to file it rather than run another fix round; it is T-131 (off-milestone). Nothing is drawn in either case, only the refusal text and its order differ.

### The retrospective says RV-65 was "left Open for a round 3"; RV-65 is Answered

- **Where:** `docs/milestones/M-21-morph-targets.md:243` (rubric 7; RV-65 finding "The retrospective's last cited verify is older than the tools-section fixes, which changed product code")
- **What is wrong:** The line was written at 968bb67, before e41eb91 set RV-65's `**Status:** Answered`. It now reads as if RV-65 itself is Open.
- **Consequence:** A reader of the retrospective gets RV-65's state wrong.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** the retrospective's round-2 line now says RV-65 was set Answered and checked in round 3, its 14ff762 verify line no longer calls round 3 open, and a round-3 line records RV-66 and T-131 (`docs/milestones/M-21-morph-targets.md`, commit "RV-66: answered").

## Summary

- **Round 3, one reviewer (claude-opus-5-5),** checking RV-65's six answers against the code, with mutations in a scratch copy and the real engine on Xellotath.
  - RV-65 findings 1, 3, 4, 5 and 6 hold. Dropping `weightRefusals` fails the new regression test, and dropping the NaN hand-off as well fails both cases. Mutating `CheckExport`'s no-warnings branch fails a `core-unit` test. core-unit (1145) and server-unit (812) passed. No product commit follows the retrospective's last cited verify (14ff762).
  - The reviewer judged the "not a finite number" fallback sound: shipped code cannot reach it, and it keeps anything from being drawn.
  - RV-65 finding 2 partly holds (the first finding above).
- **Count:** 2 findings, both Nits.
- **Escalated to the person:** the first finding continues the `render_glb` refusal-order thread found in RV-64 and RV-65, so it has now survived two rounds.
